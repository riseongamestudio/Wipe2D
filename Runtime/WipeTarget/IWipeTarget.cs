using System;
using UnityEngine;
using UnityEngine.Events;

namespace RiseOn.Wipe2D {
    /// <summary>
    /// Anything a <see cref="Wiper"/> can wipe. Positions are world space: a wiper and its targets<br/>
    /// share one space whether they live in the scene or inside a canvas.
    /// </summary>
    public interface IWipeTarget {
        /// <summary>0 in the initial state, 1 once fully wiped the other way; reads 0 to 1 for both modes. Updated asynchronously.</summary>
        float Progress { get; }

        /// <summary>Raised once, when Progress climbs past the threshold. The rest fades to the opposite state, then the target stops taking strokes.</summary>
        UnityEvent OnProgressThresholdReached { get; }

        event Action<float> OnProgressChanged;

        /// <summary>One continuous capsule from one point to the next, however far apart they are.</summary>
        void Stroke(Vector2 fromWorld, Vector2 toWorld, float worldRadius, float hardness, WipeMode mode);
    }
}