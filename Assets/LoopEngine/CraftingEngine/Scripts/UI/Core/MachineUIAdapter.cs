using System;
using System.Collections.Generic;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>What changed since the last pump. Panels rebuild only the marked regions.</summary>
    [Flags]
    public enum MachineUIDirty
    {
        None = 0,

        /// <summary>A job started, finished, blocked, resumed or was cancelled.</summary>
        Slots = 1 << 0,

        /// <summary>An order entered or left the queue.</summary>
        Queue = 1 << 1,

        /// <summary>An upgrade was installed or removed and stats were recalculated.</summary>
        Modifiers = 1 << 2,

        /// <summary>What can be crafted right now may have changed.</summary>
        Recipes = 1 << 3,

        /// <summary>A different machine was selected.</summary>
        Machine = 1 << 4,

        All = Slots | Queue | Modifiers | Recipes | Machine
    }

    /// <summary>
    /// The one and only place the crafting UI touches the crafting system. Every panel talks
    /// to this class; none of them holds a <see cref="CraftingSystem"/> or a
    /// <see cref="MachineInstance"/> reference of its own.
    /// </summary>
    /// <remarks>
    /// Two jobs. It coalesces the event stream: a batch tick can raise dozens of
    /// <see cref="CraftingEventType.RepetitionCompleted"/> events, and rebuilding the panel
    /// on each one would be wasteful, so events only set flags and <see cref="Pump"/> reports
    /// them once. And it keeps the job handles the crafting system hands out, which
    /// <see cref="CraftingJobView"/> does not carry, so a slot the UI started stays
    /// cancellable.
    /// </remarks>
    public sealed class MachineUIAdapter : IDisposable
    {
        private readonly CraftingSystem _system;
        private readonly List<RecipeDefinition> _runnableBuffer = new List<RecipeDefinition>(32);
        private readonly List<RecipeDefinition> _craftableBuffer = new List<RecipeDefinition>(32);

        private CraftingJobHandle[] _handles = Array.Empty<CraftingJobHandle>();
        private MachineInstance _machine;
        private MachineUIDirty _dirty;
        private bool _disposed;

        public MachineUIAdapter(CraftingSystem system, ICraftingDisplayProvider display)
        {
            _system = system ?? throw new ArgumentNullException(nameof(system));
            Display = display ?? throw new ArgumentNullException(nameof(display));

            _system.Events += OnCraftingEvent;
        }

        /// <summary>Names, icons and formatting. Handed to every panel through this adapter.</summary>
        public ICraftingDisplayProvider Display { get; }

        /// <summary>The machine the UI is currently showing, or null.</summary>
        public MachineInstance Machine => _machine;

        public bool HasMachine => _machine != null;

        /// <summary>Status of the last event seen for the selected machine.</summary>
        public CraftingStatus LastEventStatus { get; private set; } = CraftingStatus.Success;

        /// <summary>Raised once per <see cref="Pump"/> with everything that changed.</summary>
        public event Action<MachineUIDirty> Changed;

        /// <summary>Raised the moment a selection happens, before any rebuild.</summary>
        public event Action<MachineInstance> MachineChanged;

        /// <summary>
        /// The raw event, un-coalesced, for one-shot reactions: a completion flash, a sound,
        /// a floating "+2 iron". Not for rebuilding layout.
        /// </summary>
        public event CraftingEventHandler Raised;

        // --- Selection ---------------------------------------------------------------

        /// <summary>Points the UI at a machine. Pass null to show nothing.</summary>
        public void Select(MachineInstance machine)
        {
            if (ReferenceEquals(_machine, machine))
                return;

            _machine = machine;

            int slots = machine != null ? machine.SlotCount : 0;
            if (_handles.Length != slots)
                _handles = slots > 0 ? new CraftingJobHandle[slots] : Array.Empty<CraftingJobHandle>();
            else
                Array.Clear(_handles, 0, _handles.Length);

            LastEventStatus = CraftingStatus.Success;
            _dirty = MachineUIDirty.All;

            MachineChanged?.Invoke(machine);
        }

        // --- Change flow -------------------------------------------------------------

        /// <summary>Flags a region as stale from outside, e.g. after an inventory poll.</summary>
        public void MarkDirty(MachineUIDirty flags) => _dirty |= flags;

        /// <summary>
        /// Reports everything that changed since the last call, then clears the flags.
        /// Call once per UI update, not per event.
        /// </summary>
        public void Pump()
        {
            if (_dirty == MachineUIDirty.None)
                return;

            MachineUIDirty flags = _dirty;
            _dirty = MachineUIDirty.None;
            Changed?.Invoke(flags);
        }

        private void OnCraftingEvent(in CraftingEventArgs args)
        {
            if (_machine == null || !ReferenceEquals(args.Machine, _machine))
                return;

            LastEventStatus = args.Status;

            switch (args.Type)
            {
                case CraftingEventType.JobStarted:
                    // A queue-started job takes a slot without ever handing us a handle.
                    ForgetHandle(args.SlotIndex);
                    _dirty |= MachineUIDirty.Slots | MachineUIDirty.Queue | MachineUIDirty.Recipes;
                    break;

                case CraftingEventType.RepetitionCompleted:
                    // Containers moved, so affordability moved with them.
                    _dirty |= MachineUIDirty.Slots | MachineUIDirty.Recipes;
                    break;

                case CraftingEventType.JobFinished:
                case CraftingEventType.JobCancelled:
                    ForgetHandle(args.SlotIndex);
                    _dirty |= MachineUIDirty.Slots | MachineUIDirty.Queue | MachineUIDirty.Recipes;
                    break;

                case CraftingEventType.JobBlocked:
                case CraftingEventType.JobResumed:
                    _dirty |= MachineUIDirty.Slots;
                    break;

                case CraftingEventType.Enqueued:
                case CraftingEventType.Dequeued:
                    _dirty |= MachineUIDirty.Queue;
                    break;

                case CraftingEventType.ModifiersChanged:
                    _dirty |= MachineUIDirty.Modifiers | MachineUIDirty.Slots | MachineUIDirty.Recipes;
                    break;
            }

            Raised?.Invoke(in args);
        }

        // --- Reads -------------------------------------------------------------------

        public MachineDefinition Definition => _machine?.Definition;

        public int SlotCount => _machine != null ? _machine.SlotCount : 0;

        public int ActiveJobs => _machine != null ? _machine.ActiveJobs : 0;

        public bool HasFreeSlot => _machine != null && _machine.HasFreeSlot;

        /// <summary>Live snapshot of a slot. Cheap: sampled per frame for progress bars.</summary>
        public CraftingJobView GetSlot(int index)
            => _machine != null ? _machine.GetSlot(index) : CraftingJobView.Empty;

        public MachineStats Stats => _machine != null ? _machine.Stats : MachineStats.Neutral;

        public int QueuedCount => _machine != null ? _machine.QueuedCount : 0;

        public int QueueCapacity => _machine != null ? _machine.QueueCapacity : 0;

        public bool IsQueueFull => _machine != null && _machine.IsQueueFull;

        /// <summary>Why the queue is not advancing. Success when healthy or empty.</summary>
        public CraftingStatus LastQueueStatus
            => _machine != null ? _machine.LastQueueStatus : CraftingStatus.Success;

        /// <summary>
        /// Pending order at a logical position, 0 being next in line. Only call this when
        /// <see cref="QueuedCount"/> is above zero; it reports 0 with no machine selected.
        /// </summary>
        public QueuedCraft GetQueued(int index) => _machine.GetQueued(index);

        public QueueStallPolicy StallPolicy
        {
            get => _machine != null ? _machine.StallPolicy : QueueStallPolicy.Wait;
            set
            {
                if (_machine == null)
                    return;

                _machine.StallPolicy = value;
                _dirty |= MachineUIDirty.Queue;
            }
        }

        public int ModifierSlots => _machine != null ? _machine.ModifierSlots : 0;

        public int InstalledModifierCount => _machine != null ? _machine.InstalledModifierCount : 0;

        public ModifierDefinition GetModifier(int index)
            => _machine != null ? _machine.GetModifier(index) : null;

        /// <summary>Everything this machine can run, ignoring inventory. Reused buffer.</summary>
        public IReadOnlyList<RecipeDefinition> GetRunnableRecipes()
        {
            _runnableBuffer.Clear();
            if (_machine != null)
                _system.GetRecipesFor(_machine, _runnableBuffer);

            return _runnableBuffer;
        }

        /// <summary>What the machine's input container can afford right now. Reused buffer.</summary>
        public IReadOnlyList<RecipeDefinition> GetCraftableNow()
        {
            _craftableBuffer.Clear();
            if (_machine != null)
                _system.GetCraftableNow(_machine, _machine.Input, _craftableBuffer);

            return _craftableBuffer;
        }

        /// <summary>How many repetitions the input container could pay for.</summary>
        public int GetMaxCraftableCount(in CraftingId recipeId)
            => _machine == null ? 0 : _system.GetMaxCraftableCount(in recipeId, _machine, _machine.Input);

        /// <summary>The upgrade an item would give here, or null.</summary>
        public ModifierDefinition GetModifierFor(in CraftingId itemId)
            => _machine == null ? null : _system.GetModifierFor(in itemId, _machine);

        // --- Commands ----------------------------------------------------------------

        /// <summary>Starts a job immediately in a free slot and remembers its handle.</summary>
        public CraftingStatus StartCraft(in CraftingId recipeId, int count)
        {
            if (_machine == null)
                return CraftingStatus.InvalidRequest;

            CraftingStatus status = _machine.TryStart(in recipeId, count, out CraftingJobHandle handle);
            if (status == CraftingStatus.Success)
                StoreHandle(in handle);

            return status;
        }

        public CraftingStatus Enqueue(in CraftingId recipeId, int count, out int ticket)
        {
            ticket = 0;
            return _machine == null
                ? CraftingStatus.InvalidRequest
                : _machine.Enqueue(in recipeId, count, out ticket);
        }

        public bool CancelQueued(int ticket) => _machine != null && _machine.CancelQueued(ticket);

        public bool MoveQueued(int from, int to)
        {
            if (_machine == null || !_machine.MoveQueued(from, to))
                return false;

            _dirty |= MachineUIDirty.Queue;
            return true;
        }

        public void ClearQueue()
        {
            if (_machine == null)
                return;

            _machine.ClearQueue();
            _dirty |= MachineUIDirty.Queue;
        }

        /// <summary>
        /// True when this slot can be cancelled individually. False for jobs the queue
        /// started, because the system never exposed their handle to us.
        /// </summary>
        public bool CanCancelSlot(int index)
        {
            if (_machine == null || index < 0 || index >= _handles.Length)
                return false;

            if (!_handles[index].IsValid)
                return false;

            return _machine.TryGetJob(in _handles[index], out CraftingJobView view) && view.IsActive;
        }

        /// <summary>
        /// Cancels one slot. Returns <see cref="CraftingStatus.StaleHandle"/> when the job
        /// was started by the queue: use <see cref="CancelAllJobs"/> for those.
        /// </summary>
        public CraftingStatus CancelSlot(int index, bool refund = true)
        {
            if (_machine == null || index < 0 || index >= _handles.Length)
                return CraftingStatus.InvalidRequest;

            if (!_handles[index].IsValid)
                return CraftingStatus.StaleHandle;

            CraftingStatus status = _machine.Cancel(in _handles[index], refund);
            if (status == CraftingStatus.Success)
                ForgetHandle(index);

            return status;
        }

        /// <summary>Cancels every running job, handle or not.</summary>
        public int CancelAllJobs(bool refund = true)
        {
            if (_machine == null)
                return 0;

            int cancelled = _machine.CancelAll(refund);
            Array.Clear(_handles, 0, _handles.Length);
            return cancelled;
        }

        /// <summary>Frees a finished or cancelled slot so it can take new work.</summary>
        public bool ReleaseSlot(int index)
        {
            if (_machine == null || !_machine.ReleaseSlot(index))
                return false;

            ForgetHandle(index);
            _dirty |= MachineUIDirty.Slots;
            return true;
        }

        public int ReleaseFinishedSlots()
        {
            if (_machine == null)
                return 0;

            int released = _machine.ReleaseFinishedSlots();
            if (released > 0)
                _dirty |= MachineUIDirty.Slots;

            return released;
        }

        /// <summary>
        /// Installs an upgrade. The crafting system does not touch containers here, so the
        /// carrier item must already have been removed from the player's inventory.
        /// </summary>
        public CraftingStatus InstallModifier(ModifierDefinition modifier)
            => _machine == null ? CraftingStatus.InvalidRequest : _machine.InstallModifier(modifier);

        public bool RemoveModifier(ModifierDefinition modifier)
            => _machine != null && _machine.RemoveModifier(modifier);

        // --- Handles -----------------------------------------------------------------

        private void StoreHandle(in CraftingJobHandle handle)
        {
            int index = handle.SlotIndex;
            if (handle.IsValid && index >= 0 && index < _handles.Length)
                _handles[index] = handle;
        }

        private void ForgetHandle(int index)
        {
            if (index >= 0 && index < _handles.Length)
                _handles[index] = CraftingJobHandle.None;
        }

        // --- Lifetime ----------------------------------------------------------------

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _system.Events -= OnCraftingEvent;

            Changed = null;
            MachineChanged = null;
            Raised = null;
            _machine = null;
        }
    }
}