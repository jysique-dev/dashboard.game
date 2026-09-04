using UnityEngine;

namespace LoopEngine.GridMovement
{
    /// <summary>
    /// Shared configuration for the movement system, kept as an asset so several agents
    /// can be tuned at once and so the values survive outside a scene.
    ///
    /// Session 1 covers connectivity only. Search and follow options are added in the
    /// sessions that introduce them, rather than reserving fields for features that do
    /// not exist yet.
    ///
    /// Following the pattern already used by IsometricCameraSettings, nothing here is
    /// live-reloaded: a consumer reads these values when it builds its providers, and
    /// re-reads them only when asked to.
    /// </summary>
    [CreateAssetMenu(fileName = "MovementSettings", menuName = LoopRoutes.MovementRoute + "/Movement Settings")]
    public class MovementSettings : ScriptableObject
    {
        [Header("Connectivity")]
        [Tooltip("Which directions count as a single step.")]
        [SerializeField] private NeighborhoodMode neighborhood = NeighborhoodMode.FourWay;

        [Tooltip("What a diagonal may do when the cells beside it are blocked. Ignored in Four Way.")]
        [SerializeField] private CornerRule cornerRule = CornerRule.BlockWhenEitherBlocked;

        [Tooltip("Cost multiplier of a diagonal step. The geometric value is the square root of two, about 1.41421356.")]
        [SerializeField, Min(1f)] private float diagonalStepCost = 1.41421356f;

        public NeighborhoodMode Neighborhood => neighborhood;
        public CornerRule CornerRule => cornerRule;
        public float DiagonalStepCost => diagonalStepCost;

        /// <summary>The straight-step multiplier. Constant by definition; exposed so callers stop hard-coding 1.</summary>
        public float StraightStepCost => 1f;

        private void OnValidate()
        {
            // A diagonal that costs less than a straight step makes every optimal path a
            // staircase, and breaks the admissibility of the heuristics added in session 3.
            if (diagonalStepCost < 1f) diagonalStepCost = 1f;
        }
    }
}