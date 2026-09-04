namespace LoopEngine.GridMovement
{
    /// <summary>
    /// How an agent travels the gap between two cell centres.
    ///
    /// This only shapes the motion inside one step. The path, the timing and the events
    /// are identical in every mode, so switching is purely a look-and-feel decision and
    /// never changes where an agent ends up or when it gets there.
    /// </summary>
    public enum MovementInterpolation
    {
        /// <summary>
        /// No motion: the agent appears on the next cell. Right for turn-based games, and
        /// for anything that should not be waited on, such as an off-screen agent.
        /// </summary>
        Instant,

        /// <summary>Constant speed the whole way. The honest default.</summary>
        Linear,

        /// <summary>
        /// Eases in and out of every cell. Looks smooth on a single step and stuttery on a
        /// long path, because the agent slows to a near-stop at each cell boundary.
        /// </summary>
        Smoothed,

        /// <summary>Shaped by a curve you author. For when the two above are not the feel you want.</summary>
        Curve
    }
}