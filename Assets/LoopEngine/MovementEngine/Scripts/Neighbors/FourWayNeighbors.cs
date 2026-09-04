namespace LoopEngine.GridMovement
{
    /// <summary>
    /// Four-way connectivity: right, forward, left, back. No diagonals.
    ///
    /// Stateless, so a single instance can be shared by every agent and every search in
    /// the game. <see cref="Shared"/> exists for that.
    ///
    /// Uses GridCoord.Cardinals, which the grid engine already defines, rather than a
    /// private copy of the same four offsets: if the coordinate convention ever changes,
    /// it changes in one place.
    /// </summary>
    public sealed class FourWayNeighbors : INeighborProvider
    {
        /// <summary>A reusable instance. Safe to share because this class holds no state.</summary>
        public static readonly FourWayNeighbors Shared = new FourWayNeighbors();

        public int MaxNeighbors => 4;

        public int GetNeighbors(IMovementGrid grid, GridCoord coord, GridCoord[] neighbors, float[] stepCosts = null)
        {
            int count = 0;

            for (int i = 0; i < GridCoord.Cardinals.Length; i++)
            {
                GridCoord candidate = coord + GridCoord.Cardinals[i];
                if (!grid.IsWalkable(candidate)) continue;

                neighbors[count] = candidate;

                // Every cardinal step covers the same distance, so the multiplier is 1 and
                // the whole cost of the move comes from the destination cell.
                if (stepCosts != null) stepCosts[count] = 1f;

                count++;
            }

            return count;
        }
    }
}