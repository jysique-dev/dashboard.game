using UnityEngine;

namespace LoopEngine.GridMovement
{
    /// <summary>
    /// Eight-way connectivity: the four cardinals plus the four diagonals, with a rule
    /// governing what a diagonal may do when the cells beside it are blocked.
    ///
    /// Cardinals are emitted first. The order does not change which path is optimal, but
    /// it does decide which of several equally cheap paths comes out, and leading with
    /// cardinals gives straighter-looking routes than leading with diagonals.
    ///
    /// The diagonal cost is a multiplier, not a total: the search multiplies it by the
    /// cost of the destination cell. Its geometric value is the square root of two.
    /// </summary>
    public sealed class EightWayNeighbors : INeighborProvider
    {
        private readonly CornerRule cornerRule;
        private readonly float diagonalCost;

        /// <summary>The default: strict corners, square-root-of-two diagonals.</summary>
        public static readonly EightWayNeighbors Shared = new EightWayNeighbors();

        public EightWayNeighbors(CornerRule cornerRule = CornerRule.BlockWhenEitherBlocked, float diagonalCost = 1.41421356f)
        {
            this.cornerRule = cornerRule;

            // A diagonal cheaper than a straight step turns every optimal path into a
            // staircase and breaks the admissibility of the octile heuristic later on.
            this.diagonalCost = Mathf.Max(1f, diagonalCost);
        }

        public CornerRule CornerRule => cornerRule;
        public float DiagonalCost => diagonalCost;

        public int MaxNeighbors => 8;

        public int GetNeighbors(IMovementGrid grid, GridCoord coord, GridCoord[] neighbors, float[] stepCosts = null)
        {
            int count = 0;

            for (int i = 0; i < GridCoord.Cardinals.Length; i++)
            {
                GridCoord candidate = coord + GridCoord.Cardinals[i];
                if (!grid.IsWalkable(candidate)) continue;

                neighbors[count] = candidate;
                if (stepCosts != null) stepCosts[count] = 1f;
                count++;
            }

            for (int i = 0; i < GridCoord.Diagonals.Length; i++)
            {
                GridCoord offset = GridCoord.Diagonals[i];
                GridCoord candidate = coord + offset;

                if (!grid.IsWalkable(candidate)) continue;
                if (!IsCornerAllowed(grid, coord, offset)) continue;

                neighbors[count] = candidate;
                if (stepCosts != null) stepCosts[count] = diagonalCost;
                count++;
            }

            return count;
        }

        /// <summary>
        /// Decides whether a diagonal move may pass between the two cells that flank it.
        ///
        /// A diagonal from (0,0) to (1,1) squeezes past (1,0) and (0,1). Allowing it when
        /// both are walls is what makes an agent slip through the seam where two walls
        /// meet; allowing it when only one is a wall is what makes an agent clip the
        /// corner of a building. Which of those is acceptable depends on the game, which
        /// is why it is a setting and not a hard-coded check.
        /// </summary>
        private bool IsCornerAllowed(IMovementGrid grid, GridCoord from, GridCoord offset)
        {
            if (cornerRule == CornerRule.Allow) return true;

            bool alongX = grid.IsWalkable(from + new GridCoord(offset.X, 0));
            bool alongY = grid.IsWalkable(from + new GridCoord(0, offset.Y));

            return cornerRule == CornerRule.BlockWhenBothBlocked
                ? alongX || alongY
                : alongX && alongY;
        }
    }
}