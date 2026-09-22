using RiseOn.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace RiseOn.Wipe2D {
    /// <summary>
    /// Makes a uGUI Image wipeable. Image Type must be Simple without Preserve Aspect: the mask maps<br/>
    /// the whole rect onto the whole sprite, and Sliced/Tiled/Filled meshes use other UVs.
    /// </summary>
    public class WipeTargetImage : WipeTarget {
        // What the UI shaders clip at once a Mask turns UNITY_UI_ALPHACLIP on.
        private const float UI_ALPHA_CLIP = .001f;

        [SerializeField, Required]
        private Image target;

        private RectTransform RectTF => (RectTransform)transform;

        // The Mask hiding part of this Image. uGUI always keeps the inside, and cuts wherever the mask
        // graphic's alpha drops under the threshold above.
        [SerializeField]
        private Mask mask;

        private Material orgMat;

        protected override string TargetShaderName => "RiseOn/Wipe2D/Image";
        protected override Sprite Sprite => target.sprite;

        // The whole rect maps onto the whole sprite (Image Type Simple).
        protected override Vector2 WorldToPixel(Vector2 world) {
            Vector2 local = RectTF.InverseTransformPoint(world);
            var rect = RectTF.rect;
            var size = Sprite.rect.size;

            return new Vector2(
                (local.x - rect.xMin) / rect.width * size.x
              , (local.y - rect.yMin) / rect.height * size.y);
        }

        protected override float WorldToPixelRadius(float worldRadius) {
            return worldRadius / Mathf.Abs(RectTF.lossyScale.x) / RectTF.rect.width * Sprite.rect.width;
        }

        protected override Vector2 PixelToWorld(Vector2 pixel) {
            var rect = RectTF.rect;
            var size = Sprite.rect.size;

            return RectTF.TransformPoint(new Vector2(
                rect.xMin + pixel.x / size.x * rect.width
              , rect.yMin + pixel.y / size.y * rect.height));
        }

        protected override bool TryGetClip(out Sprite sprite, out float cutoff, out bool inside) {
            var graphic = mask != null ? mask.graphic as Image : null;

            sprite = graphic != null ? graphic.sprite : null;
            cutoff = UI_ALPHA_CLIP;
            inside = true;

            return mask != null && mask.MaskEnabled();
        }

        protected override Vector2 WorldToClipUV(Vector2 world) {
            Vector2 local = mask.rectTransform.InverseTransformPoint(world);
            var rect = mask.rectTransform.rect;

            return new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
        }

        protected override void ApplyMaterial(Material material) {
            if (material != null) {
                orgMat = target.material;
                target.material = material;
            } else {
                target.material = orgMat;
            }
        }

        protected override void SetupEditor() {
            base.SetupEditor();

            if (target == null) {
                UndoUtils.RecordForUndo(this);

                if (null == (target = GetComponent<Image>())) {
                    target = gameObject.AddComponentUndo<Image>();
                }

                UndoUtils.MarkDirty(this);
            }
        }
    }
}