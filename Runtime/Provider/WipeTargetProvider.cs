using System.Collections.Generic;
using UnityEngine;

namespace RiseOn.Wipe2D {
    public abstract class WipeTargetProvider : MonoBehaviour, IWipeTargetProvider {
        public abstract IReadOnlyList<IWipeTarget> WipeTargets { get; }
    }
}