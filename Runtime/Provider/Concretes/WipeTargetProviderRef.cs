using System.Collections.Generic;
using RiseOn.Serializables;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RiseOn.Wipe2D {
    /// <summary>The targets are exactly the ones referenced here.</summary>
    public class WipeTargetProviderRef : WipeTargetProvider {
        [SerializeField, Required]
        protected ListSerObject<IWipeTarget> targets = new();

        public override IReadOnlyList<IWipeTarget> WipeTargets => targets;
    }
}