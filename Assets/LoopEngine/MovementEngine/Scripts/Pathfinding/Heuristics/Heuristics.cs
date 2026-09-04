using UnityEngine;

namespace LoopEngine.GridMovement
{
    /// <summary>
    /// Always estimates zero. This is not a placeholder: an A* with a zero heuristic is
    /// exactly Dijkstra's algorithm, which makes it the reference for checking that a
    /// heuristic is not quietly returning worse paths than it should.
    /// </summary>
    public sealed class ZeroHeuristic : IHeuristic
    {
        public static readonly ZeroHeuristic Shared = new ZeroHeuristic();

        public string Name => "Zero";

        public float Estimate(GridCoord from, GridCoord to) => 0f;
    }

    /// <summary>
    /// Sum of the horizontal and vertical distances. Correct for four-way movement, where
    /// reaching a cell really does mean walking both legs of the L.
    ///
    /// Do not use it with eight-way movement: a diagonal covers both legs in one step, so
    /// this would estimate about 1.41 times the true cost, overestimate, and cost you the
    /// optimality guarantee.
    /// </summary>
    public sealed class ManhattanHeuristic : IHeuristic
    {
        private readonly float stepCost;

        /// <summary>
        /// <paramref name="stepCost"/> is the cheapest a single step can possibly cost on
        /// this grid. It defaults to 1, which is right whenever no cell costs less.
        /// </summary>
        public ManhattanHeuristic(float stepCost = 1f) => this.stepCost = Mathf.Max(0f, stepCost);

        public string Name => "Manhattan";

        public float Estimate(GridCoord from, GridCoord to)
            => GridCoord.ManhattanDistance(from, to) * stepCost;
    }

    /// <summary>
    /// The larger of the horizontal and vertical distances. Correct for eight-way movement
    /// when a diagonal costs the same as a straight step, because then the diagonal legs
    /// come free.
    ///
    /// With a diagonal cost above 1, this underestimates. That is safe, only slower than
    /// <see cref="OctileHeuristic"/>, which is the tighter estimate for that case.
    /// </summary>
    public sealed class ChebyshevHeuristic : IHeuristic
    {
        private readonly float stepCost;

        public ChebyshevHeuristic(float stepCost = 1f) => this.stepCost = Mathf.Max(0f, stepCost);

        public string Name => "Chebyshev";

        public float Estimate(GridCoord from, GridCoord to)
            => GridCoord.ChebyshevDistance(from, to) * stepCost;
    }

    /// <summary>
    /// The exact distance on an empty eight-way grid: take as many diagonals as possible,
    /// then walk the remainder straight.
    ///
    /// This is the right heuristic for eight-way movement with a diagonal cost above 1.
    /// Because it is exact on an open grid, A* expands almost nothing until it meets an
    /// obstacle, which is where the real speed difference against Dijkstra comes from.
    /// </summary>
    public sealed class OctileHeuristic : IHeuristic
    {
        private readonly float straightCost;
        private readonly float diagonalCost;

        /// <summary>
        /// The two costs must match the neighbour provider in use, and both must be scaled
        /// by the cheapest cell cost on the grid. <see cref="HeuristicFactory"/> does that.
        /// </summary>
        public OctileHeuristic(float straightCost = 1f, float diagonalCost = 1.41421356f)
        {
            this.straightCost = Mathf.Max(0f, straightCost);
            this.diagonalCost = Mathf.Max(0f, diagonalCost);
        }

        public string Name => "Octile";

        public float Estimate(GridCoord from, GridCoord to)
        {
            int dx = Mathf.Abs(from.X - to.X);
            int dy = Mathf.Abs(from.Y - to.Y);

            int diagonalSteps = Mathf.Min(dx, dy);
            int straightSteps = Mathf.Max(dx, dy) - diagonalSteps;

            return diagonalSteps * diagonalCost + straightSteps * straightCost;
        }
    }

    /// <summary>
    /// Straight-line distance. Always admissible, on any connectivity, because nothing can
    /// beat a straight line. It is also the loosest estimate of the four, so A* explores
    /// more with it than with a heuristic matched to the movement rules.
    ///
    /// Worth reaching for when the connectivity is unusual, or when the smoother-looking
    /// tie-breaking it produces is preferable to raw speed.
    /// </summary>
    public sealed class EuclideanHeuristic : IHeuristic
    {
        private readonly float stepCost;

        public EuclideanHeuristic(float stepCost = 1f) => this.stepCost = Mathf.Max(0f, stepCost);

        public string Name => "Euclidean";

        public float Estimate(GridCoord from, GridCoord to)
        {
            float dx = from.X - to.X;
            float dy = from.Y - to.Y;
            return Mathf.Sqrt(dx * dx + dy * dy) * stepCost;
        }
    }
}