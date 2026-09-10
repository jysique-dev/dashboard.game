using System.Collections.Generic;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// Owns every <see cref="MachineDefinition"/> and resolves the machine side of the
    /// recipe/machine relationship: which machines can run a recipe, and whether a given
    /// pairing is legal.
    /// </summary>
    public sealed class MachineManager : DefinitionRegistry<MachineDefinition>
    {
        private static readonly MachineDefinition[] EmptyMachines = new MachineDefinition[0];

        private readonly Dictionary<CraftingId, List<MachineDefinition>> _byCategory;
        private readonly List<MachineDefinition> _handCraftCapable;

        public MachineManager(int capacity = 32) : base(capacity)
        {
            _byCategory = new Dictionary<CraftingId, List<MachineDefinition>>(16, CraftingIdComparer.Instance);
            _handCraftCapable = new List<MachineDefinition>(4);
        }

        public MachineManager(IReadOnlyList<MachineDefinition> machines, List<MachineDefinition> rejected = null)
            : this(machines?.Count ?? 0)
        {
            AddRange(machines, rejected);
        }

        /// <summary>Machines providing the category, in registration order. Empty, never null.</summary>
        public IReadOnlyList<MachineDefinition> GetByCategory(in CraftingId category)
        {
            if (category.IsValid && _byCategory.TryGetValue(category, out List<MachineDefinition> bucket))
                return bucket;

            return EmptyMachines;
        }

        /// <summary>Machines that accept category-less recipes.</summary>
        public IReadOnlyList<MachineDefinition> HandCraftCapable => _handCraftCapable;

        /// <summary>
        /// Every machine that could run this recipe. Returns the live index bucket, so
        /// no allocation and no copying — do not mutate the result.
        /// </summary>
        public IReadOnlyList<MachineDefinition> GetMachinesFor(RecipeDefinition recipe)
        {
            if (recipe == null)
                return EmptyMachines;

            if (recipe.IsHandCrafted)
                return _handCraftCapable;

            CraftingId category = recipe.RequiredCategoryId;
            return GetByCategory(in category);
        }

        public CraftingStatus Resolve(in CraftingId id, out MachineDefinition machine)
        {
            if (!id.IsValid)
            {
                machine = null;
                return CraftingStatus.InvalidRequest;
            }

            return TryGet(in id, out machine) ? CraftingStatus.Success : CraftingStatus.UnknownMachine;
        }

        /// <summary>
        /// Full capability check for a pairing given by id, with a precise failure code.
        /// Availability of slots, inputs and output space is checked later, in session 5.
        /// </summary>
        public CraftingStatus CanRun(
            in CraftingId machineId,
            in CraftingId recipeId,
            IDefinitionRegistry<RecipeDefinition> recipes)
        {
            if (recipes == null)
                return CraftingStatus.NotInitialized;

            CraftingStatus status = Resolve(in machineId, out MachineDefinition machine);
            if (status != CraftingStatus.Success)
                return status;

            if (!recipes.TryGet(in recipeId, out RecipeDefinition recipe))
                return recipeId.IsValid ? CraftingStatus.UnknownRecipe : CraftingStatus.InvalidRequest;

            return machine.CanRun(recipe) ? CraftingStatus.Success : CraftingStatus.IncompatibleMachine;
        }

        /// <summary>
        /// Fills <paramref name="results"/> with every recipe this machine is allowed to run.
        /// The caller owns the buffer, so repeated calls allocate nothing.
        /// </summary>
        /// <returns>Number of recipes appended.</returns>
        public int GetRunnableRecipes(
            MachineDefinition machine,
            RecipeManager recipes,
            List<RecipeDefinition> results,
            bool clearResults = true)
        {
            if (results == null)
                return 0;

            if (clearResults)
                results.Clear();

            if (machine == null || recipes == null)
                return 0;

            int appended = 0;

            CraftingId[] categories = machine.Categories;
            for (int i = 0; i < categories.Length; i++)
            {
                CraftingId category = categories[i];
                IReadOnlyList<RecipeDefinition> bucket = recipes.GetByCategory(in category);
                for (int r = 0; r < bucket.Count; r++)
                {
                    // A machine can provide two categories; a recipe requires only one,
                    // so duplicates are impossible here. Kept as a guard for future rules.
                    RecipeDefinition recipe = bucket[r];
                    if (!results.Contains(recipe))
                    {
                        results.Add(recipe);
                        appended++;
                    }
                }
            }

            if (machine.AllowsHandCraftedRecipes)
            {
                IReadOnlyList<RecipeDefinition> hand = recipes.HandCrafted;
                for (int r = 0; r < hand.Count; r++)
                {
                    RecipeDefinition recipe = hand[r];
                    if (!results.Contains(recipe))
                    {
                        results.Add(recipe);
                        appended++;
                    }
                }
            }

            return appended;
        }

        /// <summary>
        /// Reports recipes that no registered machine can run. Content authoring safety net:
        /// a recipe requiring a category nobody provides is dead content.
        /// </summary>
        /// <returns>Number of unreachable recipes.</returns>
        public int FindUnreachableRecipes(RecipeManager recipes, List<RecipeDefinition> unreachable = null)
        {
            if (recipes == null)
                return 0;

            int count = 0;
            IReadOnlyList<RecipeDefinition> all = recipes.All;

            for (int i = 0; i < all.Count; i++)
            {
                RecipeDefinition recipe = all[i];

                // Hand crafted recipes are reachable by definition: they need no machine.
                if (recipe.IsHandCrafted)
                    continue;

                if (GetMachinesFor(recipe).Count == 0)
                {
                    count++;
                    unreachable?.Add(recipe);
                }
            }

            return count;
        }

        protected override void OnAdded(MachineDefinition definition, int index)
        {
            CraftingId[] categories = definition.Categories;
            for (int i = 0; i < categories.Length; i++)
            {
                CraftingId category = categories[i];
                if (!category.IsValid)
                    continue;

                if (!_byCategory.TryGetValue(category, out List<MachineDefinition> bucket))
                {
                    bucket = new List<MachineDefinition>(4);
                    _byCategory.Add(category, bucket);
                }

                if (!bucket.Contains(definition))
                    bucket.Add(definition);
            }

            if (definition.AllowsHandCraftedRecipes)
                _handCraftCapable.Add(definition);
        }

        protected override void OnCleared()
        {
            _byCategory.Clear();
            _handCraftCapable.Clear();
        }
    }
}