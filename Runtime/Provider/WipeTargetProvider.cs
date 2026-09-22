using System.Collections.Generic;
using RiseOn.Utils;

namespace RiseOn.Wipe2D {
    public abstract class WipeTargetProvider : MonoBehaviourExt, IWipeTargetProvider {
        public abstract IReadOnlyList<IWipeTarget> WipeTargets { get; }
    }
}