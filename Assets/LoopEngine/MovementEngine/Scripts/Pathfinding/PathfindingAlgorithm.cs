namespace LoopEngine.GridMovement
{
    /// <summary>
    /// Which search to run. An enum rather than an interface reference so the choice can
    /// be made in the inspector, saved in a settings asset, or flipped at runtime from a
    /// debug menu to compare two algorithms on the same grid.
    ///
    /// Only the values that are actually implemented appear here. Session 3 adds the
    /// heuristic searches; nothing is reserved in advance, because an enum value with no
    /// implementation behind it is a crash waiting in the inspector.
    /// </summary>
    public enum PathfindingAlgorithm
    {
        /// <summary>Fewest steps, terrain weights ignored.</summary>
        BreadthFirst,

        /// <summary>Cheapest path, terrain weights honoured.</summary>
        Dijkstra,

        /// <summary>Cheapest path, guided by a heuristic. The default choice for most agents.</summary>
        AStar,

        /// <summary>Fast, guided, and not optimal. For when a path matters more than the best path.</summary>
        GreedyBestFirst,

        /// <summary>
        /// Optimal and very fast, but only on a uniform-cost eight-way grid with corner
        /// cutting allowed. Refuses to run otherwise rather than answering wrongly.
        /// </summary>
        JumpPoint
    }
}