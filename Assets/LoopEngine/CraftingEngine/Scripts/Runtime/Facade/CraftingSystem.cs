using System.Collections.Generic;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// The single entry point of the crafting system. Owns the registries, the clock, the
    /// ticker and every machine instance, and funnels their events into one place.
    /// </summary>
    /// <remarks>
    /// Everything below this class is usable on its own; the facade exists so that game code
    /// has one type to learn instead of twelve. Nothing here adds rules of its own.
    /// </remarks>
    public sealed class CraftingSystem : ICraftingEventSink
    {
        private readonly List<MachineInstance> _machines;
        private readonly List<RecipeDefinition> _queryBuffer;

        public CraftingSystem(CraftingContext context, int machineCapacity = 64)
        {
            Context = context ?? throw new System.ArgumentNullException(nameof(context));
            Ticker = new CraftingTicker(context.Clock, machineCapacity);
            _machines = new List<MachineInstance>(machineCapacity);
            _queryBuffer = new List<RecipeDefinition>(32);
        }

        public CraftingContext Context { get; }

        public CraftingTicker Ticker { get; }

        /// <summary>Every machine created through this facade.</summary>
        public IReadOnlyList<MachineInstance> Machines => _machines;

        /// <summary>
        /// Everything that happens, in one stream. Filter on
        /// <see cref="CraftingEventArgs.Type"/>.
        /// </summary>
        public event CraftingEventHandler Events;

        void ICraftingEventSink.Raise(in CraftingEventArgs args) => Events?.Invoke(in args);

        // --- Lifecycle ---------------------------------------------------------------

        /// <summary>
        /// Creates a machine instance, wires its events and starts ticking it.
        /// </summary>
        public CraftingStatus CreateMachine(
            in CraftingId machineId,
            IItemContainer input,
            IItemContainer output,
            out MachineInstance machine)
        {
            machine = null;

            if (input == null || output == null)
                return CraftingStatus.InvalidRequest;

            CraftingStatus status = Context.ResolveMachine(in machineId, out MachineDefinition definition);
            if (status != CraftingStatus.Success)
                return status;

            machine = new MachineInstance(Context, definition, input, output) { Events = this };
            _machines.Add(machine);
            Ticker.Register(machine);
            return CraftingStatus.Success;
        }

        /// <summary>
        /// Stops ticking a machine and forgets it. Running jobs are cancelled and refunded
        /// unless you ask otherwise.
        /// </summary>
        public bool DestroyMachine(MachineInstance machine, bool refundRunningJobs = true)
        {
            if (machine == null)
                return false;

            int index = _machines.IndexOf(machine);
            if (index < 0)
                return false;

            machine.CancelAll(refundRunningJobs);
            machine.ClearQueue();
            machine.Events = null;

            Ticker.Unregister(machine);
            _machines.RemoveAt(index);
            return true;
        }

        /// <summary>Advances the clock and steps every machine. Call once per frame.</summary>
        public void Tick() => Ticker.Tick();

        /// <summary>Steps with an explicit delta, bypassing the clock. For tests and fixed steps.</summary>
        public void Tick(float deltaSeconds) => Ticker.Tick(deltaSeconds);

        // --- Crafting ----------------------------------------------------------------

        /// <summary>
        /// Crafts by hand, with no machine and no waiting. Only recipes that require no
        /// machine category are allowed.
        /// </summary>
        public CraftingStatus CraftByHand(
            in CraftingId recipeId,
            IItemContainer input,
            IItemContainer output,
            int count = 1,
            List<ItemStack> overflow = null)
        {
            var request = CraftingRequest.ByHand(recipeId, input, output, count);
            return CraftingExecutor.CraftNow(in request, Context, overflow);
        }

        /// <summary>Checks a hand crafting request without performing it.</summary>
        public CraftingStatus CanCraftByHand(
            in CraftingId recipeId,
            IItemContainer input,
            IItemContainer output,
            int count = 1)
        {
            var request = CraftingRequest.ByHand(recipeId, input, output, count);
            return CraftingValidator.Validate(in request, Context, out _);
        }

        // --- Discovery ---------------------------------------------------------------

        /// <summary>Recipes that produce an item.</summary>
        public IReadOnlyList<RecipeDefinition> GetProducersOf(in CraftingId itemId)
            => Context.Recipes.GetProducersOf(in itemId);

        /// <summary>Recipes that consume an item.</summary>
        public IReadOnlyList<RecipeDefinition> GetConsumersOf(in CraftingId itemId)
            => Context.Recipes.GetConsumersOf(in itemId);

        /// <summary>
        /// Recipes a machine is allowed to run, ignoring inventory.
        /// Fills the caller's list; nothing is allocated.
        /// </summary>
        public int GetRecipesFor(MachineInstance machine, List<RecipeDefinition> results)
        {
            if (machine == null)
                return 0;

            return Context.Machines.GetRunnableRecipes(machine.Definition, Context.Recipes, results);
        }

        /// <summary>
        /// Recipes the container can currently afford at least once, restricted to what the
        /// machine can run. Pass a null machine to search hand crafting instead.
        /// </summary>
        /// <returns>Number of recipes appended to <paramref name="results"/>.</returns>
        public int GetCraftableNow(
            MachineInstance machine,
            IItemContainer input,
            List<RecipeDefinition> results)
        {
            if (results == null || input == null)
                return 0;

            results.Clear();

            IReadOnlyList<RecipeDefinition> candidates;
            if (machine != null)
            {
                Context.Machines.GetRunnableRecipes(machine.Definition, Context.Recipes, _queryBuffer);
                candidates = _queryBuffer;
            }
            else
            {
                candidates = Context.Recipes.HandCrafted;
            }

            int appended = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                RecipeDefinition recipe = candidates[i];
                if (ItemTransfer.CheckAvailable(recipe.Inputs, 1, input) != CraftingStatus.Success)
                    continue;

                results.Add(recipe);
                appended++;
            }

            return appended;
        }

        /// <summary>
        /// How many times a recipe could be run with what the container holds.
        /// Zero when the pairing is invalid.
        /// </summary>
        public int GetMaxCraftableCount(
            in CraftingId recipeId,
            MachineInstance machine,
            IItemContainer input)
        {
            CraftingId machineId = machine != null ? machine.Definition.Id : CraftingId.None;
            return CraftingValidator.GetMaxCraftableCount(in recipeId, in machineId, Context, input);
        }

        /// <summary>
        /// The upgrade an item would provide on a machine, or null when it is not an upgrade
        /// or is not accepted there.
        /// </summary>
        public ModifierDefinition GetModifierFor(in CraftingId itemId, MachineInstance machine)
            => machine == null ? null : Context.Modifiers.FindFor(in itemId, machine.Definition);
    }
}