using UnityEngine;

namespace LoopEngine.GridMovement.Internal
{
    /// <summary>
    /// Remaps a normalised 0-to-1 progress value according to an interpolation mode.
    ///
    /// Kept apart from the mover so the shaping can be unit-tested without a scene, and so
    /// adding a mode does not mean touching movement logic.
    /// </summary>
    internal static class Easing
    {
        /// <summary>
        /// Shapes progress. The input is clamped, and every mode is required to map 0 to 0
        /// and 1 to 1: a curve that does not would leave an agent short of the cell centre
        /// and drift the whole path off the grid.
        /// </summary>
        public static float Apply(float t, MovementInterpolation mode, AnimationCurve curve)
        {
            t = Mathf.Clamp01(t);

            switch (mode)
            {
                case MovementInterpolation.Instant:
                    return 1f;

                case MovementInterpolation.Smoothed:
                    return Mathf.SmoothStep(0f, 1f, t);

                case MovementInterpolation.Curve:
                    // A missing or empty curve evaluates to zero everywhere, which would
                    // freeze the agent in place rather than fail visibly. Falling back to
                    // linear keeps a half-configured component moving.
                    if (curve == null || curve.length == 0) return t;
                    return curve.Evaluate(t);

                default:
                    return t;
            }
        }
    }
}