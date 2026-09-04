namespace LoopEngine.GridMovement
{
    /// <summary>
    /// Which distance estimate an informed search should use. Exposed as an enum so the
    /// choice can live in the inspector or in a settings asset.
    ///
    /// <see cref="Auto"/> is the value to reach for by default: picking a heuristic that
    /// does not match the movement rules is the single easiest way to make A* return
    /// paths that are not the cheapest, and Auto removes the chance of getting it wrong.
    /// </summary>
    public enum HeuristicType
    {
        /// <summary>Derived from the neighbour provider in use. The safe choice.</summary>
        Auto,

        /// <summary>No estimate. Turns A* into Dijkstra; useful as a reference.</summary>
        Zero,

        /// <summary>For four-way movement.</summary>
        Manhattan,

        /// <summary>For eight-way movement where a diagonal costs the same as a straight step.</summary>
        Chebyshev,

        /// <summary>For eight-way movement where a diagonal costs more.</summary>
        Octile,

        /// <summary>Straight-line distance. Admissible everywhere, looser than the others.</summary>
        Euclidean
    }
}