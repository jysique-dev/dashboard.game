namespace LoopEngine.GridMovement
{
    /// <summary>
    /// Decides which cells are reachable in one step from a given cell, and what that
    /// step costs relative to a straight move.
    ///
    /// This is the single place where connectivity rules live: 4 vs 8 directions,
    /// whether an agent may cut a corner between two walls, and how much a diagonal
    /// costs. Pathfinders never contain that logic, which is what lets the same A*
    /// serve a cardinal-only strategy game and an eight-way roguelike.
    ///
    /// The buffer is filled rather than returned so a search can reuse one array for
    /// its whole run instead of allocating per expanded node.
    /// </summary>
    public interface INeighborProvider
    {
        /// <summary>Largest number of entries <see cref="GetNeighbors"/> can write. Size buffers with this.</summary>
        int MaxNeighbors { get; }

        /// <summary>
        /// Writes the walkable neighbours of <paramref name="coord"/> into the buffers and
        /// returns how many were written. Both buffers must hold at least
        /// <see cref="MaxNeighbors"/> entries; <paramref name="stepCosts"/> may be null when
        /// the caller does not care about step multipliers.
        ///
        /// The value written to <paramref name="stepCosts"/> is a multiplier for the move
        /// itself (1 for a straight step, ~1.41 for a diagonal). It is not the cost of the
        /// destination cell; the pathfinder combines the two.
        /// </summary>
        int GetNeighbors(IMovementGrid grid, GridCoord coord, GridCoord[] neighbors, float[] stepCosts = null);
    }
}