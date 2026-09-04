namespace LoopEngine.GridMovement
{
    /// <summary>
    /// One question put to a pathfinder. Passed as a single value instead of a parameter
    /// list so that later sessions can add options without changing every call site and
    /// every implementation of <see cref="IPathfinder"/>.
    /// </summary>
    public readonly struct PathRequest
    {
        /// <summary>Cell the agent is standing on.</summary>
        public readonly GridCoord Start;

        /// <summary>Cell the agent wants to reach.</summary>
        public readonly GridCoord Goal;

        /// <summary>
        /// Hard ceiling on how many cells may be expanded. Zero means no limit.
        /// A limit is worth setting on large grids: an unreachable goal makes an
        /// uninformed search flood every reachable cell before giving up.
        /// </summary>
        public readonly int MaxExploredNodes;

        /// <summary>
        /// When true, a search that cannot reach the goal may return the best path it
        /// found towards it, with <see cref="PathStatus.Partial"/>. Ignored by searches
        /// that have no way to rank progress towards the goal.
        /// </summary>
        public readonly bool AllowPartial;

        public PathRequest(GridCoord start, GridCoord goal, int maxExploredNodes = 0, bool allowPartial = false)
        {
            Start = start;
            Goal = goal;
            MaxExploredNodes = maxExploredNodes > 0 ? maxExploredNodes : 0;
            AllowPartial = allowPartial;
        }

        public bool HasNodeLimit => MaxExploredNodes > 0;

        public override string ToString() => $"{Start} -> {Goal}";
    }
}