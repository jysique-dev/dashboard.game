namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>
    /// Player-facing wording for every <see cref="CraftingStatus"/>. One switch instead of a
    /// dictionary: the compiler turns it into a jump table and nothing is allocated.
    /// </summary>
    /// <remarks>
    /// The three ranges of the enum mean different things to a player. Rule failures are
    /// actionable and get a concrete sentence. Resolution failures mean the content itself is
    /// broken, and caller failures mean the UI asked for something wrong: both are bugs
    /// rather than gameplay states, so they say so plainly instead of pretending otherwise.
    /// </remarks>
    public static class CraftingStatusText
    {
        // --- Rule failures: the player can act on these ---
        public static string IncompatibleMachine = "This machine cannot run that recipe";
        public static string MissingInputs = "Not enough materials";
        public static string OutputBlocked = "Output is full";
        public static string QueueFull = "The queue is full";
        public static string MachineBusy = "The machine is busy";
        public static string ModifierRejected = "That upgrade does not fit here";

        // --- Everything else ---
        public static string Success = "Ready";
        public static string ContentError = "Missing content";
        public static string RequestError = "That request is no longer valid";

        public static string Get(CraftingStatus status)
        {
            switch (status)
            {
                case CraftingStatus.Success:
                    return Success;

                case CraftingStatus.IncompatibleMachine:
                    return IncompatibleMachine;

                case CraftingStatus.MissingInputs:
                    return MissingInputs;

                case CraftingStatus.OutputBlocked:
                    return OutputBlocked;

                case CraftingStatus.QueueFull:
                    return QueueFull;

                case CraftingStatus.MachineBusy:
                    return MachineBusy;

                case CraftingStatus.ModifierRejected:
                    return ModifierRejected;

                case CraftingStatus.UnknownItem:
                case CraftingStatus.UnknownRecipe:
                case CraftingStatus.UnknownMachine:
                case CraftingStatus.UnknownCategory:
                    return ContentError;

                default:
                    return RequestError;
            }
        }

        /// <summary>
        /// True when the status is worth putting in front of a player. Resolution and caller
        /// failures are for the console, not for a label on screen.
        /// </summary>
        public static bool IsPlayerFacing(CraftingStatus status)
            => status >= CraftingStatus.IncompatibleMachine && status <= CraftingStatus.ModifierRejected;
    }
}