namespace LoopEngine.CraftingEngine
{
    /// <summary>Lifecycle of a single crafting job occupying a machine slot.</summary>
    public enum JobState
    {
        /// <summary>The slot is free.</summary>
        Empty = 0,

        /// <summary>Inputs are paid for and work is progressing.</summary>
        Running = 1,

        /// <summary>Work finished but the outputs did not fit. Retried every tick.</summary>
        Blocked = 2,

        /// <summary>All repetitions done, or stopped early because inputs ran out.</summary>
        Completed = 3,

        /// <summary>Stopped by the caller. Inputs for the in-flight repetition were refunded.</summary>
        Cancelled = 4
    }

    /// <summary>
    /// Reference to a job slot on one specific machine instance.
    /// The generation counter makes stale handles detectable: a slot reused by a new job
    /// bumps its generation, so an old handle no longer resolves.
    /// </summary>
    public readonly struct CraftingJobHandle
    {
        public static readonly CraftingJobHandle None = default;

        public readonly int SlotIndex;
        public readonly int Generation;

        public CraftingJobHandle(int slotIndex, int generation)
        {
            SlotIndex = slotIndex;
            Generation = generation;
        }

        /// <summary>Generation 0 is never handed out, so it marks an empty handle.</summary>
        public bool IsValid => Generation != 0;

        public override string ToString() => IsValid ? "slot " + SlotIndex + " gen " + Generation : "<none>";
    }
}