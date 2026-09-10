namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// Time source for the crafting tick. This is the ONLY concept in the system that
    /// knows time exists; swapping the implementation gives you a deterministic test
    /// clock, a paused clock, or a time-scaled clock without touching any other file.
    /// </summary>
    public interface ICraftingClock
    {
        /// <summary>
        /// Seconds elapsed since the clock started. Double precision so long-running
        /// sessions do not lose resolution the way a float would.
        /// </summary>
        double Now { get; }

        /// <summary>Seconds elapsed since the previous tick, already scaled and clamped.</summary>
        float DeltaTime { get; }

        /// <summary>
        /// Advances the clock by one frame. Called exactly once per tick by the driver,
        /// before any job is stepped.
        /// </summary>
        void Advance();
    }
}