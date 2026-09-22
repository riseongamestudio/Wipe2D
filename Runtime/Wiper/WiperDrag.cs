using RiseOn.Serializables;
using RiseOn.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RiseOn.Wipe2D {
    /// <summary>
    /// Drags the wiper with the pointer and feeds its position to <see cref="IWiper.Move"/>.<br/>
    /// Works for a scene object (needs a Collider2D and a raycaster on the camera) and for a canvas<br/>
    /// element (needs a raycast-target Graphic); the two differ only in how the pointer maps to world.<br/>
    /// Single pointer: the finger that pressed first owns the drag until it lifts.
    /// </summary>
    public class WiperDrag
        : MonoBehaviourExt
        , IPointerDownHandler
        , IPointerUpHandler
        , IBeginDragHandler
        , IDragHandler
        , IEndDragHandler {
        [SerializeField, Required]
        private SerObject<IWiper> wiper;

        private bool hasPointer;
        private int pointerId;
        private Vector2 dragPointerOffset;

        void IPointerDownHandler.OnPointerDown(PointerEventData eventData) {
            if (hasPointer) return;

            hasPointer = true;
            pointerId = eventData.pointerId;
        }

        void IPointerUpHandler.OnPointerUp(PointerEventData eventData) {
            if (!IsCurPointer(eventData)) return;

            hasPointer = false;

            wiper.Value.EndMove();
        }

        void IBeginDragHandler.OnBeginDrag(PointerEventData eventData) {
            if (!IsCurPointer(eventData)) return;

            dragPointerOffset = (Vector2)TF.position - PointerToWorld(eventData);
        }

        void IDragHandler.OnDrag(PointerEventData eventData) {
            if (!IsCurPointer(eventData)) return;

            var newWorldPos = PointerToWorld(eventData) + dragPointerOffset;
            TF.SetPositionXY(newWorldPos);
            wiper.Value.Move(newWorldPos);
        }

        void IEndDragHandler.OnEndDrag(PointerEventData eventData) {
            if (!IsCurPointer(eventData)) return;

            wiper.Value.EndMove();
        }

        protected bool IsCurPointer(PointerEventData eventData) {
            return hasPointer && pointerId == eventData.pointerId;
        }

        protected virtual Vector2 PointerToWorld(PointerEventData eventData) {
            if (TF is RectTransform rectTF) {
                var space = rectTF.parent as RectTransform;

                RectTransformUtility.ScreenPointToWorldPointInRectangle(space != null ? space : rectTF, eventData.position, eventData.pressEventCamera, out var world);

                return world;
            }

            return eventData.pressEventCamera.ScreenToWorldPoint(eventData.position);
        }

        protected virtual void Reset() {
            SetupEditor();
        }

        [Button]
        protected virtual void SetupEditor() {
            if (wiper == null) {
                RecordForUndo(this);

                if (null == (wiper = new(GetComponent<IWiper>()))) {
                    wiper = new(gameObject.AddComponentUndo<Wiper>());
                }

                MarkDirty(this);
            }
        }
    }
}