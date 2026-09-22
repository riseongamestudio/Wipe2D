using UnityEngine;

namespace RiseOn.Wipe2D {
    /// <summary>
    /// A shape baked into the mask at init: only where it passes does the sprite count as wipeable.<br/>
    /// Any sprite placed anywhere works, as long as the owner can map its mask UV into the clip's UV.
    /// </summary>
    internal readonly struct WipeClip {
        public readonly Texture Texture;
        public readonly Vector4 Rect;
        public readonly Matrix4x4 MaskUVToClipUV;
        public readonly float Cutoff;
        public readonly bool Inside;

        // A null texture falls back to the shader's white default, and cutoff 0 passes everything.
        public static WipeClip None => new(null, new Vector4(0, 0, 1, 1), Matrix4x4.identity, 0f, true);

        public WipeClip(Texture texture, Vector4 rect, Matrix4x4 maskUVToClipUV, float cutoff, bool inside) {
            Texture = texture;
            Rect = rect;
            MaskUVToClipUV = maskUVToClipUV;
            Cutoff = cutoff;
            Inside = inside;
        }
    }
}