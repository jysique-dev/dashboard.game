namespace LoopEngine.GridEngine
{
    /// <summary>Which offsets count as adjacent when querying neighbours.</summary>
    public enum GridNeighborhood
    {
        /// <summary>The 4 edge-sharing cells.</summary>
        Cardinal,
        /// <summary>The 4 corner-sharing cells.</summary>
        Diagonal,
        /// <summary>All 8 surrounding cells.</summary>
        All
    }
}