using System;

namespace LoopEngine.GridMovement
{
    /// <summary>
    /// Builds a heuristic that actually matches the movement rules in play.
    ///
    /// This exists because the two ways of losing A*'s optimality guarantee are both
    /// configuration mistakes, not algorithm mistakes:
    ///
    /// First, using a distance the agent cannot achieve. Manhattan on an eight-way grid
    /// overestimates by roughly 40 percent, because it charges two steps for a move the
    /// agent makes in one.
    ///
    /// Second, ignoring cheap terrain. Every heuristic here counts steps and multiplies by
    /// a per-step cost. If some cell on the grid costs 0.5 to enter, a real path can be
    /// cheaper than the step count suggests, and an unscaled heuristic overestimates
    /// again. Passing the minimum cell cost as <c>minCellCost</c> scales the estimate down
    /// so it stays a lower bound.
    /// </summary>
    public static class HeuristicFactory
    {
        /// <summary>
        /// The heuristic that fits a neighbour provider.
        ///
        /// <paramref name="minCellCost"/> is the cheapest entry cost of any walkable cell
        /// on the grid. Leave it at 1 when no cell costs less, which is the usual case;
        /// pass the real minimum when the weight table contains anything below 1.
        /// </summary>
        public static IHeuristic CreateFor(INeighborProvider neighbors, float minCellCost = 1f)
        {
            if (minCellCost <= 0f) return ZeroHeuristic.Shared;

            switch (neighbors)
            {
                case EightWayNeighbors eightWay:
                    // Chebyshev is the tight estimate only when a diagonal is as cheap as a
                    // straight step. Above that, octile is tighter and still a lower bound.
                    return Math.Abs(eightWay.DiagonalCost - 1f) < 0.0001f
                        ? new ChebyshevHeuristic(minCellCost)
                        : new OctileHeuristic(minCellCost, eightWay.DiagonalCost * minCellCost);

                case FourWayNeighbors _:
                    return new ManhattanHeuristic(minCellCost);

                default:
                    // An unknown provider may allow moves this code cannot predict, so fall
                    // back to the estimate that no connectivity can beat.
                    return new EuclideanHeuristic(minCellCost);
            }
        }

        /// <summary>
        /// An explicitly chosen heuristic. <see cref="HeuristicType.Auto"/> defers to
        /// <see cref="CreateFor"/>; the other values are taken at face value, including
        /// when they do not match the connectivity, because an explicit choice is
        /// sometimes exactly what a caller wants for comparison.
        /// </summary>
        public static IHeuristic Create(HeuristicType type, INeighborProvider neighbors, float minCellCost = 1f)
        {
            float diagonalCost = neighbors is EightWayNeighbors eightWay ? eightWay.DiagonalCost : 1.41421356f;

            return type switch
            {
                HeuristicType.Auto => CreateFor(neighbors, minCellCost),
                HeuristicType.Zero => ZeroHeuristic.Shared,
                HeuristicType.Manhattan => new ManhattanHeuristic(minCellCost),
                HeuristicType.Chebyshev => new ChebyshevHeuristic(minCellCost),
                HeuristicType.Octile => new OctileHeuristic(minCellCost, diagonalCost * minCellCost),
                HeuristicType.Euclidean => new EuclideanHeuristic(minCellCost),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, "No heuristic is registered for this type.")
            };
        }
    }
}