using System.Collections.Generic;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// Saving and loading of a machine's runtime state.
    /// Kept in its own partial file so the crafting logic stays readable.
    /// </summary>
    public sealed partial class MachineInstance
    {
        /// <summary>
        /// Flattens everything that changes over time: installed upgrades, slot progress and
        /// the pending queue. Definitions are stored by id, never by asset reference.
        /// </summary>
        public MachineSaveData CaptureState()
        {
            var data = new MachineSaveData
            {
                machineId = Definition.RawId,
                manualSpeedBonus = _manualSpeedBonus,
                stallPolicy = (int)StallPolicy
            };

            if (_modifierCount > 0)
            {
                data.modifierIds = new string[_modifierCount];
                for (int i = 0; i < _modifierCount; i++)
                    data.modifierIds[i] = _modifiers[i].RawId;
            }

            data.slots = new JobSaveData[_slots.Length];
            for (int i = 0; i < _slots.Length; i++)
            {
                ref CraftingJob job = ref _slots[i];
                var slot = new JobSaveData
                {
                    recipeId = job.Recipe != null ? job.Recipe.RawId : string.Empty,
                    remaining = job.Remaining,
                    completed = job.Completed,
                    elapsed = job.Elapsed,
                    state = (int)job.State
                };

                data.slots[i] = slot;
            }

            int queued = _queue.Count;
            if (queued > 0)
            {
                data.queue = new QueuedSaveData[queued];
                for (int i = 0; i < queued; i++)
                {
                    QueuedCraft entry = _queue.GetAt(i);
                    data.queue[i] = new QueuedSaveData
                    {
                        recipeId = entry.Recipe != null ? entry.Recipe.RawId : string.Empty,
                        count = entry.Count
                    };
                }
            }

            return data;
        }

        /// <summary>
        /// Rebuilds this machine's state from a save.
        /// </summary>
        /// <remarks>
        /// Restored jobs do NOT re-consume their inputs: those were already paid for before
        /// the save was written, and charging again would tax the player for loading.
        /// Craft durations are recomputed from the current definitions and stats, so a
        /// balance patch applies to jobs that were already in flight.
        /// Entries pointing at definitions that no longer exist are dropped silently.
        /// </remarks>
        /// <param name="unresolved">Optional collector for ids that could not be resolved.</param>
        public CraftingStatus RestoreState(MachineSaveData data, List<string> unresolved = null)
        {
            if (data == null)
                return CraftingStatus.InvalidRequest;

            // Wipe first: restoring into a machine that is already running would double up.
            CancelAll(false);
            ClearQueue();
            ClearModifiers();

            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].Clear();
                _slotOutputs[i] = null;
            }

            _activeCount = 0;

            StallPolicy = (QueueStallPolicy)data.stallPolicy;
            _manualSpeedBonus = data.manualSpeedBonus < 0.01f ? 1f : data.manualSpeedBonus;

            RestoreModifiers(data, unresolved);

            // Recalculates stats once, after every modifier is back in place.
            RecalculateStats();

            RestoreSlots(data, unresolved);
            RestoreQueue(data, unresolved);

            return CraftingStatus.Success;
        }

        private void RestoreModifiers(MachineSaveData data, List<string> unresolved)
        {
            if (data.modifierIds == null)
                return;

            for (int i = 0; i < data.modifierIds.Length; i++)
            {
                string rawId = data.modifierIds[i];
                if (string.IsNullOrEmpty(rawId))
                    continue;

                var id = new CraftingId(rawId);
                if (!_context.Modifiers.TryGet(in id, out ModifierDefinition modifier))
                {
                    unresolved?.Add(rawId);
                    continue;
                }

                if (_modifierCount >= _modifiers.Length)
                    break;

                // Written directly instead of through InstallModifier so stats are
                // recalculated once at the end rather than once per upgrade.
                _modifiers[_modifierCount++] = modifier;
            }
        }

        private void RestoreSlots(MachineSaveData data, List<string> unresolved)
        {
            if (data.slots == null)
                return;

            int count = data.slots.Length < _slots.Length ? data.slots.Length : _slots.Length;

            for (int i = 0; i < count; i++)
            {
                JobSaveData saved = data.slots[i];
                if (saved == null || string.IsNullOrEmpty(saved.recipeId))
                    continue;

                var state = (JobState)saved.state;
                if (state != JobState.Running && state != JobState.Blocked)
                    continue;

                var recipeId = new CraftingId(saved.recipeId);
                if (!_context.Recipes.TryGet(in recipeId, out RecipeDefinition recipe))
                {
                    unresolved?.Add(saved.recipeId);
                    continue;
                }

                // A saved job for a recipe this machine can no longer run is dropped rather
                // than resumed illegally.
                if (!Definition.CanRun(recipe))
                {
                    unresolved?.Add(saved.recipeId);
                    continue;
                }

                ref CraftingJob job = ref _slots[i];
                job.Recipe = recipe;
                job.Remaining = saved.remaining < 1 ? 1 : saved.remaining;
                job.Completed = saved.completed < 0 ? 0 : saved.completed;
                job.SecondsPerCraft = EffectiveSeconds(recipe);
                job.Elapsed = ClampElapsed(saved.elapsed, job.SecondsPerCraft);
                job.State = state;
                job.LastStatus = state == JobState.Blocked
                    ? CraftingStatus.OutputBlocked
                    : CraftingStatus.Success;
                job.Generation = NextGeneration();

                _slotOutputs[i] = BuildEffectiveOutputs(recipe, i);
                _activeCount++;
            }
        }

        private void RestoreQueue(MachineSaveData data, List<string> unresolved)
        {
            if (data.queue == null)
                return;

            for (int i = 0; i < data.queue.Length; i++)
            {
                QueuedSaveData saved = data.queue[i];
                if (saved == null || string.IsNullOrEmpty(saved.recipeId))
                    continue;

                var recipeId = new CraftingId(saved.recipeId);
                if (!_context.Recipes.TryGet(in recipeId, out RecipeDefinition recipe))
                {
                    unresolved?.Add(saved.recipeId);
                    continue;
                }

                if (!Definition.CanRun(recipe))
                {
                    unresolved?.Add(saved.recipeId);
                    continue;
                }

                // Tickets are not saved: they are session-scoped handles, so a reload hands
                // out fresh ones and any ticket the caller was holding is correctly stale.
                if (!_queue.TryEnqueue(new QueuedCraft(recipe, saved.count, NextTicket())))
                    break;
            }
        }

        private static float ClampElapsed(float elapsed, float secondsPerCraft)
        {
            if (elapsed < 0f)
                return 0f;

            return elapsed > secondsPerCraft ? secondsPerCraft : elapsed;
        }
    }
}