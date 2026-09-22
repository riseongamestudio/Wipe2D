using System.Collections.Generic;

namespace RiseOn.Wipe2D {
    /// <summary>
    /// Decides which targets a <see cref="Wiper"/> strokes, kept apart from the wiper so a new way of<br/>
    /// choosing them does not touch the brush.
    /// </summary>
    public interface IWipeTargetProvider {
        IReadOnlyList<IWipeTarget> WipeTargets { get; }
    }
}