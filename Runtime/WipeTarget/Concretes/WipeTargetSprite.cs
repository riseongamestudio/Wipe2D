using RiseOn.Utils;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RiseOn.Wipe2D {
    /// <summary>
    /// Makes a SpriteRenderer wipeable. No collider needed: wipers reach it by reference.<br/>
    /// The sprite texture needs no Read/Write flag; atlas sub-rects and flipX/flipY are handled;<br/>
    /// non-uniform scale is approximated using the X axis.
    /// </summary>
    public class WipeTargetSprite : WipeTarget {
        [SerializeField, Required]
        private SpriteRenderer target;

        // The SpriteMask hiding part of this renderer. Whether it masks at all and which side survives
        // is the renderer's own Mask Interaction, so only the shape and the cutoff are read from here.
        [SerializeField]
        private SpriteMask mask;

        private Material orgMat;

        protected override string TargetShaderName => "RiseOn/Wipe2D/Sprite";
        protected override Sprite Sprite => target.sprite;

        protected override Vector2 WorldToPixel(Vector2 world) {
            Vector2 local = TF.InverseTransformPoint(world);

            if (target.flipX) local.x = -local.x;
            if (target.flipY) local.y = -local.y;

            return local * Sprite.pixelsPerUnit + Sprite.pivot;
        }

        protected override float WorldToPixelRadius(float worldRadius) {
            return worldRadius / Mathf.Abs(TF.lossyScale.x) * Sprite.pixelsPerUnit;
        }

        protected override Vector2 PixelToWorld(Vector2 pixel) {
            var local = (pixel - Sprite.pivot) / Sprite.pixelsPerUnit;

            if (target.flipX) local.x = -local.x;
            if (target.flipY) local.y = -local.y;

            return TF.TransformPoint(local);
        }

        protected override bool TryGetClip(out Sprite sprite, out float cutoff, out bool inside) {
            sprite = null;
            cutoff = 0;
            inside = target.maskInteraction is SpriteMaskInteraction.VisibleInsideMask;

            // Every way a SpriteMask ends up not clipping this renderer: the renderer opts out, the mask is
            // switched off, or the renderer is sorted outside the mask's custom range.
            if (mask == null || target.maskInteraction is SpriteMaskInteraction.None) return false;
            if (!mask.enabled || !mask.gameObject.activeInHierarchy) return false;
            if (!InRange(mask, target)) return false;

            sprite = ClipSprite(mask, out _);
            cutoff = mask.alphaCutoff;

            return sprite != null;

            // With a custom range on, the mask only reaches renderers sorted between its back and front.
            static bool InRange(SpriteMask mask, SpriteRenderer renderer) {
                if (!mask.isCustomRangeActive) return true;

                var value = SortKey(renderer.sortingLayerID, renderer.sortingOrder);

                return SortKey(mask.backSortingLayerID, mask.backSortingOrder) <= value
                    && value <= SortKey(mask.frontSortingLayerID, mask.frontSortingOrder);

                // Layer first, order second, folded into one comparable number.
                static long SortKey(int layerID, int order) {
                    return (long)SortingLayer.GetLayerValueFromID(layerID) * 4294967296L + order;
                }
            }
        }

        protected override Vector2 WorldToClipUV(Vector2 world) {
            var sprite = ClipSprite(mask, out var renderer);
            Vector2 local = mask.transform.InverseTransformPoint(world);

            // A mask shaped by a renderer flips with it; one shaped by its own sprite never flips.
            if (renderer != null) {
                if (renderer.flipX) local.x = -local.x;
                if (renderer.flipY) local.y = -local.y;
            }

            return (local * sprite.pixelsPerUnit + sprite.pivot) / sprite.rect.size;
        }

        // Where the mask takes its shape from: its own sprite, or the SpriteRenderer sitting beside it.
        private static Sprite ClipSprite(SpriteMask mask, out SpriteRenderer renderer) {
            renderer = null;

            if (mask.maskSource is SpriteMask.MaskSource.Sprite) return mask.sprite;

            renderer = mask.GetComponent<SpriteRenderer>();

            return renderer != null ? renderer.sprite : null;
        }

        protected override void ApplyMaterial(Material material) {
            if (material != null) {
                orgMat = target.sharedMaterial;
                target.sharedMaterial = material;
            } else {
                target.sharedMaterial = orgMat;
            }
        }

        protected override void SetupEditor() {
            base.SetupEditor();

            if (target == null) {
                RecordForUndo(this);

                if (null == (target = GetComponent<SpriteRenderer>())) {
                    target = gameObject.AddComponentUndo<SpriteRenderer>();
                }

                MarkDirty(this);
            }
        }
    }
}