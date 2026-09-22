using UnityEngine;

namespace RiseOn.Wipe2D {
    /// <summary>
    /// The brush as its drivers see it. Whoever owns the input, a drag script, an animation, an AI,<br/>
    /// only needs to say where the brush is and when it lifts.
    /// </summary>
    public interface IWiper {
        /// <summary>Brush centre moved here; call every frame while the brush is down.</summary>
        void Move(Vector2 world);

        /// <summary>Brush lifted; the next Move starts a new stroke instead of joining this one.</summary>
        void EndMove();
    }
}