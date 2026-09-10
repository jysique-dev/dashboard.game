using System;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// One machine in the world. Owns its slots, its queue, its installed upgrades and its
    /// running jobs. The <see cref="MachineDefinition"/> asset is shared by every instance of
    /// that type; everything that changes over time lives here.
    /// </summary>
    /// <remarks>
    /// Plain C#, no MonoBehaviour. Attach one of these to whatever represents the machine in
    /// your game and register it with a <see cref="CraftingTicker"/>.
    /// </remarks>
    public sealed partial class MachineInstance
    {
        /// <summary>
        /// Upper bound on repetitions resolved in a single tick. Only reachable with
        /// zero-second recipes; keeps an instant 1,000,000-count batch from freezing a frame.
        /// </summary>
        private const int MaxCompletionsPerTick = 64;

        private static readonly ModifierDefinition[] NoModifiers = new ModifierDefinition[0];
        private static readonly ItemStack[] NoStacks = new ItemStack[0];

        private readonly CraftingContext _context;
        private readonly CraftingJob[] _slots;
        private readonly ItemStack[][] _slotOutputs;
        private readonly ItemStack[][] _slotScratch;
        private readonly CraftingQueue _queue;
        private readonly ModifierDefinition[] _modifiers;

        private int _generationCounter;
        private int _ticketCounter;
        private int _activeCount;
        private int _modifierCount;
        private float _manualSpeedBonus = 1f;
        private ItemStack[] _bonusOutputs = NoStacks;

        public MachineInstance(
            CraftingContext context,
            MachineDefinition definition,
            IItemContainer input,
            IItemContainer output)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Input = input ?? throw new ArgumentNullException(nameof(input));
            Output = output ?? throw new ArgumentNullException(nameof(output));

            _slots = new CraftingJob[definition.ParallelSlots];
            _slotOutputs = new ItemStack[definition.ParallelSlots][];
            _slotScratch = new ItemStack[definition.ParallelSlots][];
            _queue = new CraftingQueue(definition.QueueCapacity);

            _modifiers = definition.ModifierSlots > 0
                ? new ModifierDefinition[definition.ModifierSlots]
                : NoModifiers;

            Stats = MachineStats.Neutral;
        }

        public MachineDefinition Definition { get; }

        /// <summary>
        /// Where this machine reports what it does. Set by the facade; leave it null and the
        /// machine simply stays silent, with no cost beyond one null check per event.
        /// </summary>
        public ICraftingEventSink Events { get; set; }

        /// <summary>Where this machine pulls ingredients from.</summary>
        public IItemContainer Input { get; }

        /// <summary>Where this machine delivers results. May be the same object as <see cref="Input"/>.</summary>
        public IItemContainer Output { get; }

        public int SlotCount => _slots.Length;

        /// <summary>Jobs currently running or blocked.</summary>
        public int ActiveJobs => _activeCount;

        public bool HasFreeSlot => _activeCount < _slots.Length;

        /// <summary>Resolved stats after every installed modifier and the manual bonus.</summary>
        public MachineStats Stats { get; private set; }

        /// <summary>
        /// Extra speed factor outside the modifier system, for temporary effects such as a
        /// power surge or a debug switch. Multiplied in with the modifiers.
        /// </summary>
        public float ManualSpeedBonus
        {
            get => _manualSpeedBonus;
            set
            {
                float clamped = value < 0.01f ? 0.01f : value;
                if (Math.Abs(clamped - _manualSpeedBonus) < 0.0001f)
                    return;

                _manualSpeedBonus = clamped;
                RecalculateStats();
            }
        }

        // --- Upgrades ---------------------------------------------------------------

        public int ModifierSlots => _modifiers.Length;

        public int InstalledModifierCount => _modifierCount;

        public bool HasFreeModifierSlot => _modifierCount < _modifiers.Length;

        /// <summary>Installed modifier at a slot, or null.</summary>
        public ModifierDefinition GetModifier(int index)
            => index >= 0 && index < _modifierCount ? _modifiers[index] : null;

        /// <summary>
        /// Installs an upgrade. Does not touch any container: remove the carrier item from the
        /// player's inventory yourself before calling, and give it back after
        /// <see cref="RemoveModifier"/>.
        /// </summary>
        public CraftingStatus InstallModifier(ModifierDefinition modifier)
        {
            if (modifier == null)
                return CraftingStatus.InvalidRequest;

            if (_modifiers.Length == 0 || !HasFreeModifierSlot)
                return CraftingStatus.ModifierRejected;

            if (!modifier.AppliesTo(Definition))
                return CraftingStatus.ModifierRejected;

            if (CountModifier(modifier) >= modifier.MaxPerMachine)
                return CraftingStatus.ModifierRejected;

            _modifiers[_modifierCount++] = modifier;
            RecalculateStats();
            return CraftingStatus.Success;
        }

        /// <summary>Removes one copy of an installed upgrade.</summary>
        public bool RemoveModifier(ModifierDefinition modifier)
        {
            if (modifier == null)
                return false;

            for (int i = 0; i < _modifierCount; i++)
            {
                if (_modifiers[i] != modifier)
                    continue;

                // Compact so installed modifiers always occupy the first slots.
                for (int j = i; j < _modifierCount - 1; j++)
                    _modifiers[j] = _modifiers[j + 1];

                _modifiers[--_modifierCount] = null;
                RecalculateStats();
                return true;
            }

            return false;
        }

        /// <summary>Removes every installed upgrade.</summary>
        public void ClearModifiers()
        {
            if (_modifierCount == 0)
                return;

            for (int i = 0; i < _modifierCount; i++)
                _modifiers[i] = null;

            _modifierCount = 0;
            RecalculateStats();
        }

        public int CountModifier(ModifierDefinition modifier)
        {
            int count = 0;
            for (int i = 0; i < _modifierCount; i++)
            {
                if (_modifiers[i] == modifier)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Rebuilds stats and bonus outputs, then retimes running jobs so an upgrade installed
        /// mid craft takes effect immediately without losing progress.
        /// </summary>
        private void RecalculateStats()
        {
            var accumulator = new StatAccumulator();
            accumulator.Begin();

            for (int i = 0; i < _modifierCount; i++)
                accumulator.Add(_modifiers[i]);

            accumulator.MultiplySpeed(_manualSpeedBonus);
            Stats = accumulator.Resolve();

            RebuildBonusOutputs();
            RetimeActiveJobs();
            RebuildActiveSlotOutputs();

            Raise(CraftingEventType.ModifiersChanged);
        }

        private void RebuildBonusOutputs()
        {
            int total = 0;
            for (int i = 0; i < _modifierCount; i++)
                total += _modifiers[i].BonusOutputs.Length;

            if (total == 0)
            {
                _bonusOutputs = NoStacks;
                return;
            }

            var combined = new ItemStack[total];
            int cursor = 0;
            for (int i = 0; i < _modifierCount; i++)
            {
                ItemStack[] bonus = _modifiers[i].BonusOutputs;
                for (int b = 0; b < bonus.Length; b++)
                    combined[cursor++] = bonus[b];
            }

            _bonusOutputs = combined;
        }

        private void RetimeActiveJobs()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                ref CraftingJob job = ref _slots[i];
                if (!job.IsActive || job.Recipe == null)
                    continue;

                float progress = job.Progress;
                job.SecondsPerCraft = EffectiveSeconds(job.Recipe);
                job.Elapsed = job.SecondsPerCraft * progress;
            }
        }

        private void RebuildActiveSlotOutputs()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsActive && _slots[i].Recipe != null)
                    _slotOutputs[i] = BuildEffectiveOutputs(_slots[i].Recipe, i);
            }
        }

        // --- Queue ------------------------------------------------------------------

        /// <summary>What to do when the front of the queue cannot start. Defaults to waiting.</summary>
        public QueueStallPolicy StallPolicy { get; set; } = QueueStallPolicy.Wait;

        /// <summary>Orders waiting for a slot.</summary>
        public int QueuedCount => _queue.Count;

        /// <summary>Maximum pending orders, taken from the definition.</summary>
        public int QueueCapacity => _queue.Capacity;

        public bool IsQueueFull => _queue.IsFull;

        /// <summary>
        /// Why the queue did not advance on the last pump. Success when it is healthy or empty.
        /// Read this to show "waiting for iron" instead of an idle machine with no explanation.
        /// </summary>
        public CraftingStatus LastQueueStatus { get; private set; } = CraftingStatus.Success;

        /// <summary>Pending order at a logical position, 0 being next in line.</summary>
        public QueuedCraft GetQueued(int index) => _queue.GetAt(index);

        /// <summary>
        /// Places an order in the queue. Only capability is checked now: the recipe must
        /// exist and be runnable here. Ingredients are checked when the order reaches the
        /// front, because they may well arrive in the meantime.
        /// </summary>
        /// <param name="ticket">Identifier for cancelling this specific order. 0 on failure.</param>
        public CraftingStatus Enqueue(in CraftingId recipeId, int count, out int ticket)
        {
            ticket = 0;

            if (_queue.Capacity == 0 || _queue.IsFull)
                return CraftingStatus.QueueFull;

            CraftingStatus status = CraftingValidator.ValidateCapability(in recipeId, Definition.Id, _context);
            if (status != CraftingStatus.Success)
                return status;

            if (!_context.Recipes.TryGet(in recipeId, out RecipeDefinition recipe))
                return CraftingStatus.UnknownRecipe;

            if (count < 1)
                count = 1;
            else if (count > CraftingRequest.MaxCount)
                count = CraftingRequest.MaxCount;

            int newTicket = NextTicket();
            if (!_queue.TryEnqueue(new QueuedCraft(recipe, count, newTicket)))
                return CraftingStatus.QueueFull;

            ticket = newTicket;
            Raise(CraftingEventType.Enqueued, recipe, -1, newTicket);
            return CraftingStatus.Success;
        }

        /// <summary>Removes a pending order. Nothing was consumed, so there is nothing to refund.</summary>
        public bool CancelQueued(int ticket)
        {
            int index = _queue.IndexOfTicket(ticket);
            if (index < 0)
                return false;

            QueuedCraft entry = _queue.GetAt(index);
            if (!_queue.RemoveAt(index))
                return false;

            Raise(CraftingEventType.Dequeued, entry.Recipe, -1, ticket);
            return true;
        }

        /// <summary>Reorders the queue. Positions are logical, 0 being next in line.</summary>
        public bool MoveQueued(int from, int to) => _queue.TryMove(from, to);

        /// <summary>Drops every pending order. Running jobs are untouched.</summary>
        public void ClearQueue() => _queue.Clear();

        // --- Jobs -------------------------------------------------------------------

        /// <summary>Read-only snapshot of a slot, for UI.</summary>
        public CraftingJobView GetSlot(int index)
        {
            if (index < 0 || index >= _slots.Length)
                return CraftingJobView.Empty;

            return new CraftingJobView(in _slots[index]);
        }

        /// <summary>Resolves a handle to a snapshot. Returns false for stale or empty handles.</summary>
        public bool TryGetJob(in CraftingJobHandle handle, out CraftingJobView view)
        {
            if (TryResolve(in handle, out int index))
            {
                view = new CraftingJobView(in _slots[index]);
                return true;
            }

            view = CraftingJobView.Empty;
            return false;
        }

        /// <summary>
        /// Validates and starts a job in the first free slot. Inputs for the first repetition
        /// are consumed immediately, so a started job always owns its materials.
        /// </summary>
        public CraftingStatus TryStart(in CraftingId recipeId, int count, out CraftingJobHandle handle)
        {
            handle = CraftingJobHandle.None;

            int slot = FindFreeSlot();
            if (slot < 0)
                return CraftingStatus.MachineBusy;

            var request = new CraftingRequest(recipeId, Definition.Id, Input, Output, count);
            CraftingStatus status = CraftingValidator.Validate(in request, _context, out CraftingPlan plan);
            if (status != CraftingStatus.Success)
                return status;

            return StartPlan(in plan, slot, out handle);
        }

        /// <summary>
        /// Starts an already validated plan in the first free slot.
        /// Used by the queue, which validated the order a moment earlier.
        /// </summary>
        public CraftingStatus StartPlan(in CraftingPlan plan, out CraftingJobHandle handle)
        {
            handle = CraftingJobHandle.None;

            int slot = FindFreeSlot();
            if (slot < 0)
                return CraftingStatus.MachineBusy;

            return StartPlan(in plan, slot, out handle);
        }

        private CraftingStatus StartPlan(in CraftingPlan plan, int slot, out CraftingJobHandle handle)
        {
            handle = CraftingJobHandle.None;

            if (!plan.IsValid)
                return CraftingStatus.InvalidRequest;

            // Pay for the first repetition up front. If this fails nothing was taken.
            CraftingStatus status = ItemTransfer.ConsumeAll(plan.Recipe.Inputs, 1, Input);
            if (status != CraftingStatus.Success)
                return status;

            // Built once per job, not per tick. Neutral machines reuse the recipe's own array.
            _slotOutputs[slot] = BuildEffectiveOutputs(plan.Recipe, slot);

            ref CraftingJob job = ref _slots[slot];
            job.Recipe = plan.Recipe;
            job.Remaining = plan.Count;
            job.Completed = 0;
            job.SecondsPerCraft = EffectiveSeconds(plan.Recipe);
            job.Elapsed = 0f;
            job.State = JobState.Running;
            job.LastStatus = CraftingStatus.Success;
            job.Generation = NextGeneration();

            _activeCount++;
            handle = new CraftingJobHandle(slot, job.Generation);

            Raise(CraftingEventType.JobStarted, job.Recipe, slot);
            return CraftingStatus.Success;
        }

        /// <summary>
        /// Advances every slot. Called by the ticker, once per frame, with the clock's delta.
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds < 0f)
                return;

            // Fill slots that freed up on the previous tick before stepping, so a job started
            // from the queue makes progress in the same frame it starts.
            PumpQueue();

            if (_activeCount == 0)
                return;

            for (int i = 0; i < _slots.Length; i++)
            {
                ref CraftingJob job = ref _slots[i];
                if (job.State == JobState.Running)
                    StepRunning(ref job, i, deltaSeconds);
                else if (job.State == JobState.Blocked)
                    StepBlocked(ref job, i);
            }
        }

        private void StepRunning(ref CraftingJob job, int slot, float deltaSeconds)
        {
            job.Elapsed += deltaSeconds;

            int completions = 0;

            // A zero-second recipe satisfies this condition every iteration, which is why
            // the completion cap below is not optional.
            while (job.State == JobState.Running && job.Elapsed >= job.SecondsPerCraft)
            {
                if (!TryDeliver(ref job, slot))
                    return;

                // Keep the remainder so long recipes do not drift.
                job.Elapsed -= job.SecondsPerCraft;
                if (job.Elapsed < 0f)
                    job.Elapsed = 0f;

                if (++completions >= MaxCompletionsPerTick)
                    return;
            }
        }

        private void StepBlocked(ref CraftingJob job, int slot)
        {
            RecipeDefinition recipe = job.Recipe;

            if (TryDeliver(ref job, slot))
            {
                job.Elapsed = 0f;
                job.LastStatus = CraftingStatus.Success;
                Raise(CraftingEventType.JobResumed, recipe, slot);
            }
        }

        /// <summary>
        /// Delivers one repetition's outputs and sets up the next one.
        /// Returns false when the job could not move forward this tick.
        /// </summary>
        private bool TryDeliver(ref CraftingJob job, int slot)
        {
            ItemStack[] outputs = _slotOutputs[slot] ?? job.Recipe.Outputs;

            // Check before inserting: a partial insert cannot be undone safely.
            CraftingStatus space = ItemTransfer.CheckSpace(outputs, 1, Output);
            if (space != CraftingStatus.Success)
            {
                bool wasRunning = job.State == JobState.Running;
                job.State = JobState.Blocked;
                job.LastStatus = CraftingStatus.OutputBlocked;
                job.Elapsed = job.SecondsPerCraft;

                // Only report the transition, not every retried tick.
                if (wasRunning)
                    Raise(CraftingEventType.JobBlocked, job.Recipe, slot, 0, CraftingStatus.OutputBlocked);

                return false;
            }

            ItemTransfer.ProduceAll(outputs, 1, Output);
            job.Completed++;
            job.Remaining--;

            Raise(CraftingEventType.RepetitionCompleted, job.Recipe, slot);

            if (job.Remaining <= 0)
            {
                Finish(ref job, slot, JobState.Completed, CraftingStatus.Success);
                return false;
            }

            // Pay for the next repetition now, so progress always means owned materials.
            CraftingStatus paid = ItemTransfer.ConsumeAll(job.Recipe.Inputs, 1, Input);
            if (paid != CraftingStatus.Success)
            {
                // Ingredients ran out mid batch. What was made is kept; the job ends here.
                Finish(ref job, slot, JobState.Completed, CraftingStatus.MissingInputs);
                return false;
            }

            job.State = JobState.Running;
            job.LastStatus = CraftingStatus.Success;
            return true;
        }

        /// <summary>
        /// Stops a job and refunds the repetition currently in progress.
        /// Already delivered repetitions are not undone.
        /// </summary>
        public CraftingStatus Cancel(in CraftingJobHandle handle, bool refund = true)
        {
            if (!TryResolve(in handle, out int index))
                return CraftingStatus.StaleHandle;

            ref CraftingJob job = ref _slots[index];
            if (!job.IsActive)
                return CraftingStatus.JobNotRunning;

            if (refund)
            {
                ItemStack[] inputs = job.Recipe.Inputs;
                ItemTransfer.Rollback(inputs, 1, Input, inputs.Length);
            }

            Finish(ref job, index, JobState.Cancelled, CraftingStatus.Success);
            return CraftingStatus.Success;
        }

        /// <summary>Cancels every active job on this machine.</summary>
        public int CancelAll(bool refund = true)
        {
            int cancelled = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].IsActive)
                    continue;

                var handle = new CraftingJobHandle(i, _slots[i].Generation);
                if (Cancel(in handle, refund) == CraftingStatus.Success)
                    cancelled++;
            }

            return cancelled;
        }

        /// <summary>
        /// Frees a slot that finished or was cancelled, so it can accept new work.
        /// Call it after reading the result, or let the queue do it for you.
        /// </summary>
        public bool ReleaseSlot(int index)
        {
            if (index < 0 || index >= _slots.Length)
                return false;

            ref CraftingJob job = ref _slots[index];
            if (job.State == JobState.Empty || job.IsActive)
                return false;

            job.Clear();
            _slotOutputs[index] = null;
            return true;
        }

        /// <summary>Frees every finished or cancelled slot in one pass.</summary>
        public int ReleaseFinishedSlots()
        {
            int released = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (ReleaseSlot(i))
                    released++;
            }

            return released;
        }

        /// <summary>
        /// Starts as many queued orders as there are free slots.
        /// Runs before the stepping pass and allocates nothing.
        /// </summary>
        public void PumpQueue()
        {
            if (_queue.Count == 0)
            {
                LastQueueStatus = CraftingStatus.Success;
                return;
            }

            while (HasFreeSlot && _queue.Count > 0)
            {
                if (!TryTakeStartable(out CraftingPlan plan, out int index, out CraftingStatus blockedBy))
                {
                    LastQueueStatus = blockedBy;
                    return;
                }

                _queue.RemoveAt(index);

                CraftingStatus started = StartPlan(in plan, out _);
                if (started != CraftingStatus.Success)
                {
                    // Should not happen: the plan validated a moment ago and a slot was free.
                    LastQueueStatus = started;
                    return;
                }

                LastQueueStatus = CraftingStatus.Success;
            }
        }

        /// <summary>
        /// Finds the first queued order that can start right now, honouring the stall policy.
        /// Entries that can never run (deleted recipe, wrong machine) are dropped on sight.
        /// </summary>
        private bool TryTakeStartable(out CraftingPlan plan, out int index, out CraftingStatus blockedBy)
        {
            plan = default;
            index = -1;
            blockedBy = CraftingStatus.Success;

            for (int i = 0; i < _queue.Count; i++)
            {
                QueuedCraft entry = _queue.GetAt(i);
                if (!entry.IsValid)
                {
                    _queue.RemoveAt(i);
                    i--;
                    continue;
                }

                CraftingId recipeId = entry.Recipe.Id;
                var request = new CraftingRequest(recipeId, Definition.Id, Input, Output, entry.Count);
                CraftingStatus status = CraftingValidator.Validate(in request, _context, out CraftingPlan candidate);

                if (status == CraftingStatus.Success)
                {
                    plan = candidate;
                    index = i;
                    return true;
                }

                blockedBy = status;

                // Dead order: what it points at cannot run here, ever. Never worth waiting for.
                if (status.IsResolutionFailure()
                    || status == CraftingStatus.IncompatibleMachine
                    || status == CraftingStatus.InvalidRequest)
                {
                    _queue.RemoveAt(i);
                    i--;
                    Raise(CraftingEventType.Dequeued, entry.Recipe, -1, entry.Ticket, status);
                    continue;
                }

                switch (StallPolicy)
                {
                    case QueueStallPolicy.Wait:
                        return false;

                    case QueueStallPolicy.Drop:
                        _queue.RemoveAt(i);
                        i--;
                        Raise(CraftingEventType.Dequeued, entry.Recipe, -1, entry.Ticket, status);
                        continue;

                    default: // SkipToNext
                        continue;
                }
            }

            return false;
        }

        // --- Internals --------------------------------------------------------------

        /// <summary>
        /// Recipe outputs after the yield stat and the modifiers' bonus outputs.
        /// Returns the recipe's own array untouched when nothing changes it, so an
        /// unmodified machine costs nothing.
        /// </summary>
        private ItemStack[] BuildEffectiveOutputs(RecipeDefinition recipe, int slot)
        {
            ItemStack[] baseOutputs = recipe.Outputs;
            float yield = Stats.Yield;
            bool neutralYield = Math.Abs(yield - 1f) < 0.0001f;

            if (neutralYield && _bonusOutputs.Length == 0)
                return baseOutputs;

            // Per-slot scratch buffer, reused whenever the shape is unchanged. A machine
            // repeating the same recipe allocates once and never again.
            int total = baseOutputs.Length + _bonusOutputs.Length;
            ItemStack[] result = _slotScratch[slot];
            if (result == null || result.Length != total)
            {
                result = new ItemStack[total];
                _slotScratch[slot] = result;
            }

            for (int i = 0; i < baseOutputs.Length; i++)
            {
                ItemStack stack = baseOutputs[i];
                if (neutralYield || stack.IsEmpty)
                {
                    result[i] = stack;
                    continue;
                }

                // Truncated, not rounded: a x1.5 yield on a single item stays at 1.
                // Fractional yields only pay off on output amounts of 2 or more.
                int scaled = (int)(stack.Amount * yield);
                if (scaled < 1)
                    scaled = 1;

                result[i] = stack.WithAmount(scaled);
            }

            // Bonus outputs are flat: the yield stat does not multiply them.
            for (int i = 0; i < _bonusOutputs.Length; i++)
                result[baseOutputs.Length + i] = _bonusOutputs[i];

            return result;
        }

        private float EffectiveSeconds(RecipeDefinition recipe)
        {
            float seconds = Definition.GetCraftSeconds(recipe);
            float speed = Stats.Speed;
            return speed <= 0.01f ? seconds / 0.01f : seconds / speed;
        }

        private void Finish(ref CraftingJob job, int slot, JobState state, CraftingStatus status)
        {
            job.State = state;
            job.LastStatus = status;
            job.Elapsed = 0f;
            _slotOutputs[slot] = null;
            _activeCount--;

            if (_activeCount < 0)
                _activeCount = 0;

            CraftingEventType type = state == JobState.Cancelled
                ? CraftingEventType.JobCancelled
                : CraftingEventType.JobFinished;

            Raise(type, job.Recipe, slot, 0, status);
        }

        private void Raise(
            CraftingEventType type,
            RecipeDefinition recipe = null,
            int slotIndex = -1,
            int ticket = 0,
            CraftingStatus status = CraftingStatus.Success)
        {
            ICraftingEventSink sink = Events;
            if (sink == null)
                return;

            var args = new CraftingEventArgs(type, this, recipe, slotIndex, ticket, status);
            sink.Raise(in args);
        }

        /// <summary>
        /// Prefers a never-used slot over one holding a finished job, so completed results
        /// stay readable for at least one frame before being overwritten.
        /// </summary>
        private int FindFreeSlot()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].State == JobState.Empty)
                    return i;
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].IsActive)
                    return i;
            }

            return -1;
        }

        private bool TryResolve(in CraftingJobHandle handle, out int index)
        {
            index = handle.SlotIndex;

            if (!handle.IsValid || index < 0 || index >= _slots.Length)
                return false;

            return _slots[index].Generation == handle.Generation;
        }

        private int NextTicket()
        {
            // Skip 0: it marks a failed enqueue.
            if (++_ticketCounter == 0)
                _ticketCounter = 1;

            return _ticketCounter;
        }

        private int NextGeneration()
        {
            // Skip 0: it is reserved for CraftingJobHandle.None.
            if (++_generationCounter == 0)
                _generationCounter = 1;

            return _generationCounter;
        }
    }
}