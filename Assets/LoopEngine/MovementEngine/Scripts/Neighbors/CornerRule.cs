namespace LoopEngine.GridMovement
{
    /// <summary>
    /// What a diagonal step is allowed to do when the two cells flanking it are blocked.
    /// Only consulted in <see cref="NeighborhoodMode.EightWay"/>.
    ///
    /// The visual symptom of getting this wrong is an agent slipping through the seam
    /// between two walls that meet at a corner.
    /// </summary>
    public enum CornerRule
    {
        /// <summary>Diagonals are always allowed. Fastest, and lets agents clip corners.</summary>
        Allow,

        /// <summary>Blocked only when both flanking cells are blocked. Squeezing through a gap is fine.</summary>
        BlockWhenBothBlocked,

        /// <summary>Blocked when either flanking cell is blocked. The strictest, and the safest for physical agents.</summary>
        BlockWhenEitherBlocked
    }
}