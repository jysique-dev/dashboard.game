namespace LoopEngine.GridMovement
{
    /// <summary>
    /// How a search ended. A caller that only wants to know whether it may move can read
    /// <see cref="PathResult.HasPath"/>; this enum exists so failures can be told apart,
    /// because "the goal is a wall" and "the search ran out of budget" call for very
    /// different reactions in gameplay code.
    /// </summary>
    public enum PathStatus
    {
        /// <summary>A complete path from start to goal was found.</summary>
        Success,

        /// <summary>
        /// A path towards the goal was found, but it stops short. Only produced by the
        /// heuristic searches, which are the ones able to rank how close a cell is to the
        /// goal. BFS and Dijkstra report NotFound instead.
        /// </summary>
        Partial,

        /// <summary>The whole reachable region was explored and the goal was not in it.</summary>
        NotFound,

        /// <summary>The start cell is outside the grid or not walkable.</summary>
        InvalidStart,

        /// <summary>The goal cell is outside the grid or not walkable.</summary>
        InvalidGoal,

        /// <summary>The node budget ran out before the goal was reached.</summary>
        LimitReached,

        /// <summary>
        /// The search refuses to run against this grid or these connectivity rules. Not a
        /// failure of the search but of the setup: Jump Point Search reports this when the
        /// neighbour provider is not eight-way with corner cutting allowed, because running
        /// anyway would return a path that is wrong without looking wrong.
        /// </summary>
        UnsupportedConfiguration
    }
}