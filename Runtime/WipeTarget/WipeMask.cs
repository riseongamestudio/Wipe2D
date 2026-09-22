using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace RiseOn.Wipe2D {
    /// <summary>
    /// The GPU side of one wipeable sprite: a single-channel mask the size of the sprite rect,<br/>
    /// capsule strokes drawn into it with hardware min/max blending, and an asynchronous readback of<br/>
    /// how much is still visible. Works in sprite pixels; owners convert from their own space.<br/>
    /// The mask starts as the sprite's own alpha, so the ratio is measured against the opaque area.
    /// </summary>
    internal sealed class WipeMask {
        internal const string STAMP_SHADER = "Hidden/RiseOn/Wipe2D/Stamp";

        private const int ERASE_PASS = 0;
        private const int REVEAL_PASS = 1;
        private const int INIT_PASS = 2;
        private const int FADE_PASS = 3;

        private static readonly int spriteTexId = Shader.PropertyToID("_SpriteTex");
        private static readonly int spriteRectId = Shader.PropertyToID("_SpriteRect");
        private static readonly int texSizeId = Shader.PropertyToID("_TexSize");
        private static readonly int segmentId = Shader.PropertyToID("_Segment");
        private static readonly int radiusId = Shader.PropertyToID("_Radius");
        private static readonly int hardnessId = Shader.PropertyToID("_Hardness");
        private static readonly int fadeId = Shader.PropertyToID("_Fade");
        private static readonly int clipTexId = Shader.PropertyToID("_ClipTex");
        private static readonly int clipRectId = Shader.PropertyToID("_ClipRect");
        private static readonly int clipParamsId = Shader.PropertyToID("_ClipParams");
        private static readonly int clipMatrixId = Shader.PropertyToID("_ClipMatrix");

        // Unity-convention matrix on purpose: SetViewProjectionMatrices converts to the graphics API itself.
        // Pre-applying GL.GetGPUProjectionMatrix flips Y twice on D3D and every stroke lands mirrored.
        private static readonly Matrix4x4 projection = Matrix4x4.Ortho(0, 1, 0, 1, -1, 1);

        // Unit quad is 0..1; this recentres it so TRS can rotate it about the segment midpoint.
        private static readonly Matrix4x4 centreQuad = Matrix4x4.Translate(new Vector3(-.5f, -.5f, 0));

        private static Mesh quad;

        private readonly RenderTexture texture;
        private readonly Material stampMaterial;
        private readonly CommandBuffer cmd = new();
        private readonly Matrix4x4 texelToUV;
        private readonly float texelsPerPixel;
        private readonly int bytesPerPixel;
        private readonly int readbackMip;
        private readonly float readbackInterval;
        private readonly float baselineMean;

        // Per-draw parameters. Material values are read when the buffer executes, so anything
        // that varies between draws has to travel in a property block.
        private readonly MaterialPropertyBlock props = new();

        private readonly Action<AsyncGPUReadbackRequest> readbackCallback;

        private float lastReadbackTime;
        private bool readbackPending;
        private bool dirty;
        private bool released;

        public Texture Texture => texture;

        /// <summary>Sprite mesh UV to mask UV: maskUV = uv * xy + zw.</summary>
        public Vector4 MaskST { get; }

        /// <summary>Visible share of the sprite's opaque area, 0..1.</summary>
        public float VisibleRatio { get; private set; } = 1;

        public event Action<float> OnVisibleRatioChanged;

        private static Mesh Quad {
            get {
                return quad != null ? quad : quad = Build();

                static Mesh Build() {
                    var mesh = new Mesh {
                        name = "WipeQuad"
                      , hideFlags = HideFlags.HideAndDontSave
                    };

                    mesh.SetVertices(new[] { new Vector3(0, 0), new Vector3(1, 0), new Vector3(1, 1), new Vector3(0, 1) });
                    mesh.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
                    mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
                    mesh.UploadMeshData(true);

                    return mesh;
                }
            }
        }

        public WipeMask(Sprite sprite, Shader stampShader, int maxSize, int readbackMipOffset, float readbackInterval, in WipeClip clip) {
            this.readbackInterval = readbackInterval;
            readbackCallback = OnReadback;

            var tex = sprite.texture;
            var rect = sprite.rect;

            FitMask(rect, maxSize, out var w, out var h);

            texelsPerPixel = w / rect.width;
            texelToUV = Matrix4x4.Scale(new Vector3(1f / w, 1f / h, 1));
            texture = CreateTexture(w, h, out bytesPerPixel);
            readbackMip = Mathf.Max(0, texture.mipmapCount - 1 - readbackMipOffset);

            // Sprite's sub-rect inside its texture, in texture UV.
            var spriteRect = new Vector4(rect.x / tex.width, rect.y / tex.height, rect.width / tex.width, rect.height / tex.height);

            MaskST = new Vector4(1 / spriteRect.z, 1 / spriteRect.w, -spriteRect.x / spriteRect.z, -spriteRect.y / spriteRect.w);

            stampMaterial = new Material(stampShader) { hideFlags = HideFlags.HideAndDontSave };
            stampMaterial.SetTexture(spriteTexId, tex);
            stampMaterial.SetVector(spriteRectId, spriteRect);
            stampMaterial.SetVector(texSizeId, new Vector4(w, h, 0, 0));

            // A matrix uniform defaults to zero, not identity, so the clip is always written even when there is none.
            stampMaterial.SetTexture(clipTexId, clip.Texture);
            stampMaterial.SetVector(clipRectId, clip.Rect);
            stampMaterial.SetVector(clipParamsId, new Vector4(clip.Cutoff, clip.Inside ? 1 : 0, 0, 0));
            stampMaterial.SetMatrix(clipMatrixId, clip.MaskUVToClipUV);

            baselineMean = MeasureBaseline(this);

            if (baselineMean <= 0) {
                Debug.LogWarning($"{nameof(WipeMask)}: sprite alpha is empty or unreadable, progress will stay at its initial value.");
            }

            // One mask texel per sprite pixel unless the longest side is capped.
            static void FitMask(Rect rect, int maxSize, out int w, out int h) {
                w = Mathf.RoundToInt(rect.width);
                h = Mathf.RoundToInt(rect.height);

                if (maxSize <= 0 || Mathf.Max(w, h) <= maxSize) return;

                var scale = (float)maxSize / Mathf.Max(w, h);

                w = Mathf.Max(1, Mathf.RoundToInt(w * scale));
                h = Mathf.Max(1, Mathf.RoundToInt(h * scale));
            }

            // R8 wherever the device can render to it; mips exist only for the readback and are generated on demand.
            static RenderTexture CreateTexture(int w, int h, out int bytesPerPixel) {
                var format = SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, GraphicsFormatUsage.Render)
                    ? GraphicsFormat.R8_UNorm
                    : GraphicsFormat.R8G8B8A8_UNorm;

                bytesPerPixel = format is GraphicsFormat.R8_UNorm ? 1 : 4;

                var desc = new RenderTextureDescriptor(w, h, format, 0) {
                    useMipMap = true
                  , autoGenerateMips = false
                  , sRGB = false
                };

                var texture = new RenderTexture(desc) {
                    name = "WipeMask"
                  , filterMode = FilterMode.Bilinear
                  , wrapMode = TextureWrapMode.Clamp
                  , hideFlags = HideFlags.HideAndDontSave
                };
                texture.Create();

                return texture;
            }

            // Mean of the sprite's alpha. One synchronous readback at creation, so the ratio can be relative
            // to the opaque area instead of the whole rect.
            static float MeasureBaseline(WipeMask context) {
                context.cmd.Clear();
                context.DrawInit();
                Graphics.ExecuteCommandBuffer(context.cmd);

                context.texture.GenerateMips();

                var request = AsyncGPUReadback.Request(context.texture, context.readbackMip);
                request.WaitForCompletion();

                return request.hasError ? 0 : context.Mean(request.GetData<byte>());
            }
        }

        public void Clear(bool visible) {
            cmd.Clear();

            if (visible) {
                DrawInit();
            } else {
                cmd.SetRenderTarget(texture);
                cmd.ClearRenderTarget(false, true, Color.clear);
            }

            Graphics.ExecuteCommandBuffer(cmd);

            SetVisibleRatio(visible ? 1 : 0);

            dirty = false;
        }

        /// <summary>One capsule covering the whole segment: a single draw, continuous at any stroke speed.</summary>
        public void Stroke(Vector2 fromPixel, Vector2 toPixel, float radiusPixels, float hardness, bool erase) {
            var a = fromPixel * texelsPerPixel;
            var b = toPixel * texelsPerPixel;
            var r = radiusPixels * texelsPerPixel;

            // Entirely outside the mask: nothing to draw.
            var min = Vector2.Min(a, b) - new Vector2(r, r);
            var max = Vector2.Max(a, b) + new Vector2(r, r);

            if (max.x < 0 || max.y < 0 || min.x > texture.width || min.y > texture.height) return;

            props.SetVector(segmentId, new Vector4(a.x, a.y, b.x, b.y));
            props.SetFloat(radiusId, r);
            props.SetFloat(hardnessId, hardness);

            Draw(CapsuleMatrix(texelToUV, a, b, r), erase ? ERASE_PASS : REVEAL_PASS);

            dirty = true;

            // Unit quad -> centred -> capsule box in texels, rotated onto the segment -> mask UV.
            static Matrix4x4 CapsuleMatrix(Matrix4x4 texelToUV, Vector2 a, Vector2 b, float r) {
                var delta = b - a;
                var len = delta.magnitude;
                var angle = len > 1e-4f ? Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg : 0f;

                return texelToUV
                  * Matrix4x4.TRS((a + b) * .5f, Quaternion.Euler(0, 0, angle), new Vector3(len + 2 * r, 2 * r, 1))
                  * centreQuad;
            }
        }

        /// <summary>
        /// Moves every texel a fraction of the way to fully visible or fully hidden.<br/>
        /// A delta of 1 lands exactly on the target, so the ratio is set directly instead of read back.
        /// </summary>
        public void Fade(bool visible, float delta) {
            props.SetVector(fadeId, new Vector4(visible ? 1 : 0, delta, 0, 0));

            Draw(Matrix4x4.identity, FADE_PASS);

            if (delta < 1) {
                dirty = true;
                return;
            }

            SetVisibleRatio(visible ? 1 : 0);

            dirty = false;
        }

        /// <summary>Call once per frame; schedules the asynchronous readback while the mask keeps changing.</summary>
        public void Tick(float now) {
            if (!dirty || readbackPending || now - lastReadbackTime < readbackInterval) return;

            dirty = false;
            readbackPending = true;
            lastReadbackTime = now;

            texture.GenerateMips();

            AsyncGPUReadback.Request(texture, readbackMip, readbackCallback);
        }

        public void Release() {
            released = true;

            cmd.Release();
            texture.Release();

            SafeDestroy(texture);
            SafeDestroy(stampMaterial);
        }

        internal static void SafeDestroy(Object target) {
            if (target == null) return;

            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }

        // One immediate draw of a quad placed in mask UV space, with the per-draw values in props.
        private void Draw(Matrix4x4 matrix, int pass) {
            cmd.Clear();
            cmd.SetRenderTarget(texture);
            cmd.SetViewProjectionMatrices(Matrix4x4.identity, projection);
            cmd.DrawMesh(Quad, matrix, stampMaterial, 0, pass, props);
            Graphics.ExecuteCommandBuffer(cmd);
        }

        // Init goes through the same quad and projection as the strokes so they stay aligned by construction.
        private void DrawInit() {
            cmd.SetRenderTarget(texture);
            cmd.SetViewProjectionMatrices(Matrix4x4.identity, projection);
            cmd.DrawMesh(Quad, Matrix4x4.identity, stampMaterial, 0, INIT_PASS);
        }

        private void OnReadback(AsyncGPUReadbackRequest request) {
            if (released || request.hasError) return;

            readbackPending = false;

            if (baselineMean <= 0) return;

            SetVisibleRatio(Mathf.Clamp01(Mean(request.GetData<byte>()) / baselineMean));
        }

        private void SetVisibleRatio(float value) {
            if (Mathf.Approximately(value, VisibleRatio)) return;

            VisibleRatio = value;
            OnVisibleRatioChanged?.Invoke(value);
        }

        private float Mean(NativeArray<byte> data) {
            var sum = 0L;

            for (var i = 0; i < data.Length; i += bytesPerPixel) sum += data[i];

            return sum / (255f * ((float)data.Length / bytesPerPixel));
        }
    }
}