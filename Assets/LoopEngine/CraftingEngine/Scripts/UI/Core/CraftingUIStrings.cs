namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>
    /// Every literal the crafting UI puts on screen. Not localisation, just one place to
    /// change a word instead of hunting through six panels.
    /// </summary>
    /// <remarks>
    /// Names and icons of items, recipes and machines do not belong here: those come from
    /// <see cref="ICraftingDisplayProvider"/>, because only the host knows them.
    /// </remarks>
    public static class CraftingUIStrings
    {
        public static string NoMachine = "No machine selected";
        public static string Slots = "Slots";
        public static string Queue = "Queue";
        public static string EmptySlot = "Empty";
        public static string Idle = "Idle";
        public static string Running = "Running";
        public static string Blocked = "Blocked";
        public static string Done = "Done";
        public static string Cancelled = "Cancelled";
        public static string Cancel = "Cancel";
        public static string Release = "Release";
        public static string CancelAll = "Cancel all";
        public static string ReleaseAll = "Clear finished";

        /// <summary>Shown on a cancel button the UI cannot use. See <see cref="MachineUIAdapter.CanCancelSlot"/>.</summary>
        public static string CancelUnavailable = "Started by the queue: cancel all instead";

        public static string Recipes = "Recipes";
        public static string Upgrades = "Upgrades";
        public static string Inventory = "Inventory";
        public static string MachineInput = "Input";
        public static string MachineOutput = "Output";
        public static string InventoryEmpty = "Empty";
        public static string NoMatches = "Nothing matches the filter";
        public static string SortCatalogue = "Default";
        public static string SortName = "A-Z";
        public static string SortMost = "Most";
        public static string SortLeast = "Least";
        public static string ItemTypesSuffix = " types";
        public static string ItemCellsSuffix = " stacks";
        public static string StackOfFormat = "x{0} / {1}";
        public static string HeldFormat = "{0} held";
        public static string EmptyUpgrade = "Empty";
        public static string NotAnUpgrade = "That item is not an upgrade for this machine";
        public static string QueueEmpty = "Nothing queued";
        public static string NoQueue = "This machine has no queue";
        public static string ClearQueue = "Clear";
        public static string MoveUp = "\u25B2";
        public static string MoveDown = "\u25BC";
        public static string Remove = "\u2715";
        public static string PolicyWait = "Wait";
        public static string PolicySkip = "Skip";
        public static string PolicyDrop = "Drop";
        public static string PolicyWaitHint = "Hold the line. A missing ingredient stops everything behind it.";
        public static string PolicySkipHint = "Look further down for something that can start now. Order is not guaranteed.";
        public static string PolicyDropHint = "Discard orders that cannot start. Never stalls, silently loses orders.";
        public static string EmptyQueue = "Queue is empty";
        //public static string NoQueue = "This machine has no queue";
        //public static string ClearQueue = "Clear";
        public static string StallPolicy = "When stalled";
        //public static string MoveUp = "\u25B2";
        //public static string MoveDown = "\u25BC";
        //public static string Remove = "\u2715";
        public static string NoRecipes = "This machine can run nothing";
        public static string NoneAffordable = "Nothing affordable right now";
        public static string ShowAll = "All";
        public static string ShowAffordable = "Affordable";
        public static string Craft = "Craft";
        public static string Enqueue = "Queue";
        public static string Max = "Max";
        public static string Ready = "Ready";

        public static string SlotCountFormat = "{0} / {1} busy";
        public static string RepetitionsFormat = "{0} left";
        public static string CompletedFormat = "{0} made";
        public static string AffordableFormat = "x{0} affordable";
        public static string AmountFormat = "x{0}";
        public static string CraftedFormat = "Started {0}";
        public static string QueuedFormat = "Queued {0}";
        public static string QueueCountFormat = "{0} / {1}";
        public static string PositionFormat = "{0}.";
        public static string InstalledFormat = "Installed {0}";
        public static string RemovedFormat = "Removed {0}";
        public static string QueueFillFormat = "{0} / {1}";
        public static string TicketFormat = "#{0}";
        public static string WaitingFormat = "Waiting: {0}";

        public static string ForPolicy(QueueStallPolicy policy)
        {
            switch (policy)
            {
                case QueueStallPolicy.SkipToNext: return PolicySkip;
                case QueueStallPolicy.Drop: return PolicyDrop;
                default: return PolicyWait;
            }
        }

        public static string HintForPolicy(QueueStallPolicy policy)
        {
            switch (policy)
            {
                case QueueStallPolicy.SkipToNext: return PolicySkipHint;
                case QueueStallPolicy.Drop: return PolicyDropHint;
                default: return PolicyWaitHint;
            }
        }

        public static string ForSort(InventorySort sort)
        {
            switch (sort)
            {
                case InventorySort.Name: return SortName;
                case InventorySort.AmountDescending: return SortMost;
                case InventorySort.AmountAscending: return SortLeast;
                default: return SortCatalogue;
            }
        }

        public static string ForState(JobState state)
        {
            switch (state)
            {
                case JobState.Running: return Running;
                case JobState.Blocked: return Blocked;
                case JobState.Completed: return Done;
                case JobState.Cancelled: return Cancelled;
                default: return Idle;
            }
        }
    }
}