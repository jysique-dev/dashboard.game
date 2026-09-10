namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// Result code for every operation the crafting system exposes.
    /// Operations never throw for expected failures; they return one of these.
    /// </summary>
    public enum CraftingStatus
    {
        /// <summary>Operation completed.</summary>
        Success = 0,

        // --- Resolution failures: something referenced does not exist ---

        /// <summary>The item id is not registered.</summary>
        UnknownItem = 1,

        /// <summary>The recipe id is not registered.</summary>
        UnknownRecipe = 2,

        /// <summary>The machine id is not registered.</summary>
        UnknownMachine = 3,

        /// <summary>The machine category id is not registered.</summary>
        UnknownCategory = 4,

        // --- Rule failures: everything exists but the operation is not allowed ---

        /// <summary>This machine's categories do not cover the recipe's requirement.</summary>
        IncompatibleMachine = 10,

        /// <summary>The input container does not hold every required item.</summary>
        MissingInputs = 11,

        /// <summary>The output container cannot accept the produced items.</summary>
        OutputBlocked = 12,

        /// <summary>The machine's queue has no free slot.</summary>
        QueueFull = 13,

        /// <summary>The machine is already running and does not support queueing.</summary>
        MachineBusy = 14,

        /// <summary>A modifier could not be applied to this machine.</summary>
        ModifierRejected = 15,

        // --- Caller failures: the request itself is malformed or out of order ---

        /// <summary>Null argument, negative count, or otherwise malformed request.</summary>
        InvalidRequest = 20,

        /// <summary>The referenced job is not currently running.</summary>
        JobNotRunning = 21,

        /// <summary>The job handle refers to a job that no longer exists.</summary>
        StaleHandle = 22,

        /// <summary>The system has not been initialised yet.</summary>
        NotInitialized = 23,

        /// <summary>A definition with the same id is already registered.</summary>
        DuplicateId = 24
    }

    public static class CraftingStatusExtensions
    {
        /// <summary>Convenience check so call sites read as intent, not as comparison.</summary>
        public static bool IsSuccess(this CraftingStatus status) => status == CraftingStatus.Success;

        /// <summary>True when the failure comes from missing or wrong content, not from caller misuse.</summary>
        public static bool IsResolutionFailure(this CraftingStatus status)
            => status >= CraftingStatus.UnknownItem && status <= CraftingStatus.UnknownCategory;
    }
}