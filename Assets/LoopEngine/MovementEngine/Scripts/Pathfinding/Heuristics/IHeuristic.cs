namespace LoopEngine.GridMovement
{
    /// <summary>
    /// Estimates the remaining cost from one cell to another. This is the "informed" part
    /// of an informed search: it is what lets A* walk towards the goal instead of flooding
    /// in every direction.
    ///
    /// The estimate must never exceed the true remaining cost. A heuristic that respects
    /// that is called admissible, and admissibility is what makes A* return the optimal
    /// path (Hart, Nilsson and Raphael, 1968). Overestimate and the search still finds a
    /// path, faster, but no longer the cheapest one.
    ///
    /// Two things make a heuristic overestimate on this grid, and both are handled by
    /// <see cref="HeuristicFactory"/> rather than by the individual implementations:
    /// picking a distance that assumes moves the agent cannot make, and ignoring that
    /// cells may cost less than one.
    /// </summary>
    public interface IHeuristic
    {
        /// <summary>Name for logs and debug UI.</summary>
        string Name { get; }

        /// <summary>Estimated cost of travelling from one cell to another. Never negative.</summary>
        float Estimate(GridCoord from, GridCoord to);
    }
}