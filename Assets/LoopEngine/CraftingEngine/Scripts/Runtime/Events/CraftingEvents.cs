namespace LoopEngine.CraftingEngine
{
    /// <summary>What happened. One enum instead of a dozen separate events.</summary>
    public enum CraftingEventType
    {
        /// <summary>A job took a slot and paid for its first repetition.</summary>
        JobStarted = 0,

        /// <summary>One repetition finished and its outputs were delivered.</summary>
        RepetitionCompleted = 1,

        /// <summary>Work is done and the slot is finished. Check the status for why.</summary>
        JobFinished = 2,

        /// <summary>The job cannot deliver because the output container is full.</summary>
        JobBlocked = 3,

        /// <summary>A blocked job found room and resumed.</summary>
        JobResumed = 4,

        /// <summary>The caller stopped a running job.</summary>
        JobCancelled = 5,

        /// <summary>An order was added to the queue.</summary>
        Enqueued = 6,

        /// <summary>An order left the queue without running: cancelled, dropped or dead.</summary>
        Dequeued = 7,

        /// <summary>A modifier was installed or removed, and stats were recalculated.</summary>
        ModifiersChanged = 8
    }

    /// <summary>
    /// Everything an event carries. A readonly struct passed by <c>in</c>, so raising an
    /// event allocates nothing and boxes nothing.
    /// </summary>
    public readonly struct CraftingEventArgs
    {
        public readonly CraftingEventType Type;

        /// <summary>The machine involved. Never null.</summary>
        public readonly MachineInstance Machine;

        /// <summary>The recipe involved, or null for machine-level events.</summary>
        public readonly RecipeDefinition Recipe;

        /// <summary>Slot index for job events, -1 for queue events.</summary>
        public readonly int SlotIndex;

        /// <summary>Queue ticket for queue events, 0 otherwise.</summary>
        public readonly int Ticket;

        /// <summary>Why it happened. Success for ordinary progress.</summary>
        public readonly CraftingStatus Status;

        public CraftingEventArgs(
            CraftingEventType type,
            MachineInstance machine,
            RecipeDefinition recipe = null,
            int slotIndex = -1,
            int ticket = 0,
            CraftingStatus status = CraftingStatus.Success)
        {
            Type = type;
            Machine = machine;
            Recipe = recipe;
            SlotIndex = slotIndex;
            Ticket = ticket;
            Status = status;
        }
    }

    /// <summary>
    /// Delegate taking the args by reference. A plain <c>Action&lt;T&gt;</c> would copy the
    /// struct on every call and every subscriber.
    /// </summary>
    public delegate void CraftingEventHandler(in CraftingEventArgs args);

    /// <summary>
    /// Where a machine reports what it did. Implemented by the facade; machines only know
    /// this interface, so they never depend on the system that owns them.
    /// </summary>
    public interface ICraftingEventSink
    {
        void Raise(in CraftingEventArgs args);
    }
}