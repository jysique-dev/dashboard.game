namespace LoopEngine.GridMovement
{
    /// <summary>
    /// A search that turns a <see cref="PathRequest"/> into a <see cref="PathResult"/>.
    ///
    /// The grid and the connectivity rules are arguments rather than constructor state,
    /// so one instance can serve many grids and many agents. Implementations are allowed
    /// to keep internal scratch buffers between calls, which is why an instance is not
    /// safe to use from two threads at once: give each worker its own.
    /// </summary>
    public interface IPathfinder
    {
        /// <summary>Name for logs and debug UI.</summary>
        string Name { get; }

        /// <summary>
        /// True when the search accounts for per-cell costs. False means it optimises the
        /// number of steps and treats every walkable cell as equal, which matters when the
        /// grid has terrain weights and the caller expected them to be honoured.
        /// </summary>
        bool RespectsCosts { get; }

        /// <summary>
        /// Runs the search. Never returns null and never throws for an unreachable goal:
        /// failure is reported through <see cref="PathResult.Status"/>.
        /// </summary>
        PathResult FindPath(IMovementGrid grid, INeighborProvider neighbors, PathRequest request);
    }
}