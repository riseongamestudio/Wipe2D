using RiseOn.Serializables;
using RiseOn.Utils;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RiseOn.Wipe2D {
    /// <summary>
    /// The brush. Holds the mode and the shape, and strokes every selected target from one world<br/>
    /// point to the next. No hit test: a stroke that misses a target is culled by that target for free,<br/>
    /// so targets need no collider and the wiper needs no raycast.
    /// </summary>
    public class Wiper : MonoBehaviour, IWiper {
        [SerializeField]
        private WipeMode mode;

        [SerializeField, MinValue(0)]
        private float radius = 1;

        [SerializeField, PropertyRange(0, 1)]
        private float hardness = .8f;

        [SerializeField, Required]
        private SerObject<IWipeTargetProvider> provider;

        private Vector2 lastWorld;
        private bool hasLast;

        public virtual void Move(Vector2 world) {
            // The first point of a stroke has nowhere to come from, so it strokes onto itself: a single dab.
            var from = hasLast ? lastWorld : world;

            foreach (var target in provider.Value.WipeTargets) target.Stroke(from, world, radius, hardness, mode);

            lastWorld = world;
            hasLast = true;
        }

        public virtual void EndMove() {
            hasLast = false;
        }

        protected virtual void OnDisable() {
            EndMove();
        }

        protected virtual void Reset() {
            SetupEditor();
        }

        [Button]
        protected virtual void SetupEditor() {
            if (provider == null) {
                this.RecordForUndo();

                if (null == (provider = new(GetComponent<IWipeTargetProvider>()))) {
                    provider = new(gameObject.AddComponentUndo<WipeTargetProviderRef>());
                }

                this.MarkDirty();
            }
        }
    }
}