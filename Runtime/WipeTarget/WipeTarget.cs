using System;
using RiseOn.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

namespace RiseOn.Wipe2D {
    /// <summary>
    /// Shared spine of one wipeable renderer: owns the mask, swaps the renderer's material for the<br/>
    /// masked one, and converts the wiper's world-space strokes into sprite pixels. Concrete targets<br/>
    /// only say where their sprite is and how to reach their renderer.
    /// </summary>
    public abstract class WipeTarget : MonoBehaviourExt, IWipeTarget {
        private static readonly int maskTexId = Shader.PropertyToID("_MaskTex");
        private static readonly int maskStId = Shader.PropertyToID("_MaskST");

        [SerializeField, Required, FoldoutGroup("References")]
        private Shader stampShader;

        [SerializeField, Required, FoldoutGroup("References")]
        private Shader targetShader;

        [SerializeField, FoldoutGroup("Threshold"), PropertyRange(0, 1)]
        private float progressThreshold = .85f;

        // Reaching the threshold fades whatever is left to the state opposite the initial one over this long,
        // then the component retires.
        [SerializeField, FoldoutGroup("Threshold"), MinValue(0)]
        private float fadeDuration = .25f;

        [SerializeField, FoldoutGroup("Threshold")]
        private UnityEvent onProgressThresholdReached;

        [SerializeField, FoldoutGroup("Optimization"), MinValue(0)]
        private int maxMaskSize;

        [SerializeField, FoldoutGroup("Optimization"), MinValue(0)]
        private float readbackInterval = .1f;

        [SerializeField, FoldoutGroup("Optimization"), PropertyRange(0, 5)]
        private int readbackMipOffset = 3;

        // The target only knows how it starts; which way it gets wiped is the Wiper's mode.
        [SerializeField]
        private WipeInitState initState;

        private WipeMask mask;
        private Material material;
        private bool progressThresholdTriggerred;
        private bool fading;
        private float fadeT;

        [ShowInInspector, ReadOnly, ProgressBar(0, 1)]
        public float Progress => ToProgress(mask?.VisibleRatio ?? (initState is WipeInitState.Visible ? 1 : 0));
        public UnityEvent OnProgressThresholdReached => onProgressThresholdReached;
        public event Action<float> OnProgressChanged;

        /// <summary>Name of the shader that draws this renderer with the mask; filled into the inspector by Reset.</summary>
        protected abstract string TargetShaderName { get; }

        protected abstract Sprite Sprite { get; }

        /// <summary>World point to a pixel inside the sprite rect, origin at the rect's bottom-left.</summary>
        protected abstract Vector2 WorldToPixel(Vector2 world);

        protected abstract float WorldToPixelRadius(float worldRadius);

        /// <summary>Inverse of <see cref="WorldToPixel"/>.</summary>
        protected abstract Vector2 PixelToWorld(Vector2 pixel);

        /// <summary>
        /// The mask hiding part of this renderer, as the sprite it draws plus how it cuts; false when<br/>
        /// nothing masks it. A null sprite still clips by the mask's rect alone.
        /// </summary>
        protected abstract bool TryGetClip(out Sprite sprite, out float cutoff, out bool inside);

        /// <summary>World point to a UV inside the mask's rect.</summary>
        protected abstract Vector2 WorldToClipUV(Vector2 world);

        /// <summary>Put the material on the renderer; null restores whatever was there before.</summary>
        protected abstract void ApplyMaterial(Material material);

        protected virtual void Awake() {
            Init(this);

            static void Init(WipeTarget context) {
                context.mask = new WipeMask(context.Sprite, context.stampShader, context.maxMaskSize, context.readbackMipOffset, context.readbackInterval, BuildClip(context));
                context.mask.OnVisibleRatioChanged += context.RelayProgress;

                context.material = new Material(context.targetShader) { hideFlags = HideFlags.HideAndDontSave };
                context.material.SetTexture(maskTexId, context.mask.Texture);
                context.material.SetVector(maskStId, context.mask.MaskST);

                context.ApplyMaterial(context.material);

                context.mask.Clear(context.initState is WipeInitState.Visible);
            }

            // Affine map from this mask's UV to the clip's UV, found by pushing three corners through world
            // space. Captured once: the target and the mask are expected to stay rigid to each other.
            static WipeClip BuildClip(WipeTarget context) {
                if (!context.TryGetClip(out var clipSprite, out var cutoff, out var inside)) return WipeClip.None;

                var size = context.Sprite.rect.size;

                var a = context.WorldToClipUV(context.PixelToWorld(Vector2.zero));
                var b = context.WorldToClipUV(context.PixelToWorld(new Vector2(size.x, 0)));
                var c = context.WorldToClipUV(context.PixelToWorld(new Vector2(0, size.y)));

                var matrix = Matrix4x4.identity;

                matrix.SetColumn(0, new Vector4(b.x - a.x, b.y - a.y, 0, 0));
                matrix.SetColumn(1, new Vector4(c.x - a.x, c.y - a.y, 0, 0));
                matrix.SetColumn(3, new Vector4(a.x, a.y, 0, 1));

                // No sprite leaves the shader's white default, so the mask's rect is the whole of the clip.
                if (clipSprite == null) return new WipeClip(null, new Vector4(0, 0, 1, 1), matrix, cutoff, inside);

                var tex = clipSprite.texture;
                var rect = clipSprite.rect;
                var clipRect = new Vector4(rect.x / tex.width, rect.y / tex.height, rect.width / tex.width, rect.height / tex.height);

                return new WipeClip(tex, clipRect, matrix, cutoff, inside);
            }
        }

        protected virtual void OnDestroy() {
            Release(this);

            static void Release(WipeTarget context) {
                if (context.mask is null) return;

                context.mask.OnVisibleRatioChanged -= context.RelayProgress;
                context.mask.Release();
                context.mask = null;

                context.ApplyMaterial(null);
                WipeMask.SafeDestroy(context.material);
                context.material = null;
            }
        }

        protected virtual void LateUpdate() {
            if (mask is null) return;

            if (fading) TickFade(this, Time.deltaTime);

            mask.Tick(Time.time);

            // Successive lerps with delta = (t - t0) / (1 - t0) land exactly on the target at t = 1,
            // whatever the frame rate; the last frame is a delta of 1.
            static void TickFade(WipeTarget context, float deltaTime) {
                var t = context.fadeDuration > 0 ? Mathf.Clamp01(context.fadeT + deltaTime / context.fadeDuration) : 1;
                var delta = t >= 1 ? 1 : (t - context.fadeT) / (1 - context.fadeT);

                context.mask.Fade(context.initState is WipeInitState.Hidden, delta);
                context.fadeT = t;

                if (t < 1) return;

                // Fully faded: nothing left to wipe or measure, so the component retires.
                context.fading = false;
                context.enabled = false;
            }
        }

        public virtual void Stroke(Vector2 fromWorld, Vector2 toWorld, float worldRadius, float hardness, WipeMode mode) {
            // Past the threshold the target is done: strokes are ignored so nothing can pull against the fade.
            if (progressThresholdTriggerred) return;

            mask.Stroke(WorldToPixel(fromWorld), WorldToPixel(toWorld), WorldToPixelRadius(worldRadius), hardness, mode is WipeMode.Erase);
        }

        private void RelayProgress(float visibleRatio) {
            var progress = ToProgress(visibleRatio);

            OnProgressChanged?.Invoke(progress);
            CheckThreshold(this, progress);

            // Fires once. From here the target only fades out the remainder and then retires.
            static void CheckThreshold(WipeTarget context, float progress) {
                if (context.progressThresholdTriggerred || progress < context.progressThreshold) return;

                context.progressThresholdTriggerred = true;
                context.fading = true;
                context.fadeT = 0;

                context.onProgressThresholdReached?.Invoke();
            }
        }

        // Progress counts away from the initial state, so an erased target and a revealed one both run 0 to 1.
        private float ToProgress(float visibleRatio) {
            return initState is WipeInitState.Visible ? 1 - visibleRatio : visibleRatio;
        }

        protected virtual void Reset() {
            SetupEditor();
        }

        [Button]
        protected virtual void SetupEditor() {
            stampShader ??= Shader.Find(WipeMask.STAMP_SHADER);
            targetShader ??= Shader.Find(TargetShaderName);
        }
    }
}