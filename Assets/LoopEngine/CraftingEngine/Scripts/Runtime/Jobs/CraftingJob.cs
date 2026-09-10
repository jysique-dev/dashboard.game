namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// One unit of work occupying a machine slot. A mutable struct on purpose: jobs live
    /// inside a preallocated array on the machine instance and are ticked in place through
    /// <c>ref</c>, so running a thousand machines allocates nothing per frame.
    /// </summary>
    /// <remarks>
    /// Never copy this to a local and expect the copy to be live. Always take it by
    /// <c>ref</c> from the owning array. Read-only consumers should use
    /// <see cref="CraftingJobView"/> instead.
    /// </remarks>
    public struct CraftingJob
    {
        /// <summary>What is being made. Null when the slot is empty.</summary>
        public RecipeDefinition Recipe;

        /// <summary>Repetitions still to do, including the one in progress.</summary>
        public int Remaining;

        /// <summary>Repetitions finished and delivered so far.</summary>
        public int Completed;

        /// <summary>Seconds for one repetition, after machine speed and modifiers.</summary>
        public float SecondsPerCraft;

        /// <summary>Seconds accumulated toward the current repetition.</summary>
        public float Elapsed;

        public JobState State;

        /// <summary>Why the job is blocked, or why it stopped. Success while healthy.</summary>
        public CraftingStatus LastStatus;

        /// <summary>Bumped every time the slot is reused, so old handles go stale.</summary>
        public int Generation;

        /// <summary>True when the slot holds work that is not finished.</summary>
        public bool IsActive => State == JobState.Running || State == JobState.Blocked;

        /// <summary>0 to 1 progress of the current repetition. Instant recipes report 1.</summary>
        public float Progress
        {
            get
            {
                if (SecondsPerCraft <= 0f)
                    return 1f;

                float p = Elapsed / SecondsPerCraft;
                if (p < 0f)
                    return 0f;

                return p > 1f ? 1f : p;
            }
        }

        /// <summary>Seconds left in the current repetition. Zero when blocked or instant.</summary>
        public float RemainingSeconds
        {
            get
            {
                float left = SecondsPerCraft - Elapsed;
                return left > 0f ? left : 0f;
            }
        }

        /// <summary>Clears the slot but keeps the generation counter.</summary>
        public void Clear()
        {
            Recipe = null;
            Remaining = 0;
            Completed = 0;
            SecondsPerCraft = 0f;
            Elapsed = 0f;
            State = JobState.Empty;
            LastStatus = CraftingStatus.Success;
        }
    }

    /// <summary>
    /// Immutable snapshot of a job, safe to hand to UI code without exposing the live struct.
    /// </summary>
    public readonly struct CraftingJobView
    {
        public static readonly CraftingJobView Empty = default;

        public readonly RecipeDefinition Recipe;
        public readonly int Remaining;
        public readonly int Completed;
        public readonly float Progress;
        public readonly float RemainingSeconds;
        public readonly JobState State;
        public readonly CraftingStatus LastStatus;

        internal CraftingJobView(in CraftingJob job)
        {
            Recipe = job.Recipe;
            Remaining = job.Remaining;
            Completed = job.Completed;
            Progress = job.Progress;
            RemainingSeconds = job.RemainingSeconds;
            State = job.State;
            LastStatus = job.LastStatus;
        }

        public bool IsActive => State == JobState.Running || State == JobState.Blocked;
    }
}