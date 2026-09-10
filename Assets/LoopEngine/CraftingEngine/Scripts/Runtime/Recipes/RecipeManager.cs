using System.Collections.Generic;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// Owns every <see cref="RecipeDefinition"/> and answers the three questions the
    /// rest of the system actually asks:
    /// "how do I make X", "what can I do with X", "what can this machine run".
    /// All three are backed by inverted indices built at registration, so none of them
    /// ever scans the full recipe list at runtime.
    /// </summary>
    public sealed class RecipeManager : DefinitionRegistry<RecipeDefinition>
    {
        private static readonly RecipeDefinition[] EmptyRecipes = new RecipeDefinition[0];

        private readonly Dictionary<CraftingId, List<RecipeDefinition>> _byOutput;
        private readonly Dictionary<CraftingId, List<RecipeDefinition>> _byInput;
        private readonly Dictionary<CraftingId, List<RecipeDefinition>> _byCategory;
        private readonly List<RecipeDefinition> _handCrafted;

        public RecipeManager(int capacity = 128) : base(capacity)
        {
            _byOutput = new Dictionary<CraftingId, List<RecipeDefinition>>(capacity, CraftingIdComparer.Instance);
            _byInput = new Dictionary<CraftingId, List<RecipeDefinition>>(capacity, CraftingIdComparer.Instance);
            _byCategory = new Dictionary<CraftingId, List<RecipeDefinition>>(16, CraftingIdComparer.Instance);
            _handCrafted = new List<RecipeDefinition>(16);
        }

        public RecipeManager(IReadOnlyList<RecipeDefinition> recipes, List<RecipeDefinition> rejected = null)
            : this(recipes?.Count ?? 0)
        {
            AddRange(recipes, rejected);
        }

        /// <summary>Recipes that produce the item. Empty, never null.</summary>
        public IReadOnlyList<RecipeDefinition> GetProducersOf(in CraftingId item)
            => Lookup(_byOutput, in item);

        /// <summary>Recipes that consume the item. Empty, never null.</summary>
        public IReadOnlyList<RecipeDefinition> GetConsumersOf(in CraftingId item)
            => Lookup(_byInput, in item);

        /// <summary>Recipes a machine of this category can run. Empty, never null.</summary>
        public IReadOnlyList<RecipeDefinition> GetByCategory(in CraftingId category)
            => Lookup(_byCategory, in category);

        /// <summary>Recipes that need no machine.</summary>
        public IReadOnlyList<RecipeDefinition> HandCrafted => _handCrafted;

        /// <summary>
        /// Resolves a recipe id and reports precisely why it failed.
        /// </summary>
        public CraftingStatus Resolve(in CraftingId id, out RecipeDefinition recipe)
        {
            if (!id.IsValid)
            {
                recipe = null;
                return CraftingStatus.InvalidRequest;
            }

            return TryGet(in id, out recipe) ? CraftingStatus.Success : CraftingStatus.UnknownRecipe;
        }

        /// <summary>
        /// Cross-checks every referenced item against the item registry.
        /// A recipe can be structurally valid on its own and still point at an item that
        /// was never registered; this catches that. Call it once after building both managers.
        /// </summary>
        /// <param name="items">The populated item registry.</param>
        /// <param name="broken">Optional collector for recipes with dangling references.</param>
        /// <returns>Number of recipes with at least one unresolved item.</returns>
        public int ValidateAgainstItems(IDefinitionRegistry<ItemDefinition> items, List<RecipeDefinition> broken = null)
        {
            if (items == null)
                return 0;

            int brokenCount = 0;
            IReadOnlyList<RecipeDefinition> all = All;

            for (int i = 0; i < all.Count; i++)
            {
                RecipeDefinition recipe = all[i];
                if (HasUnresolvedItem(recipe.Inputs, items) || HasUnresolvedItem(recipe.Outputs, items))
                {
                    brokenCount++;
                    broken?.Add(recipe);
                }
            }

            return brokenCount;
        }

        private static bool HasUnresolvedItem(ItemStack[] stacks, IDefinitionRegistry<ItemDefinition> items)
        {
            for (int i = 0; i < stacks.Length; i++)
            {
                if (!items.Contains(stacks[i].Item))
                    return true;
            }

            return false;
        }

        protected override void OnAdded(RecipeDefinition definition, int index)
        {
            ItemStack[] outputs = definition.Outputs;
            for (int i = 0; i < outputs.Length; i++)
                Index(_byOutput, outputs[i].Item, definition);

            ItemStack[] inputs = definition.Inputs;
            for (int i = 0; i < inputs.Length; i++)
                Index(_byInput, inputs[i].Item, definition);

            if (definition.IsHandCrafted)
                _handCrafted.Add(definition);
            else
                Index(_byCategory, definition.RequiredCategoryId, definition);
        }

        protected override void OnCleared()
        {
            _byOutput.Clear();
            _byInput.Clear();
            _byCategory.Clear();
            _handCrafted.Clear();
        }

        private static void Index(
            Dictionary<CraftingId, List<RecipeDefinition>> map,
            in CraftingId key,
            RecipeDefinition recipe)
        {
            if (!key.IsValid)
                return;

            if (!map.TryGetValue(key, out List<RecipeDefinition> bucket))
            {
                bucket = new List<RecipeDefinition>(4);
                map.Add(key, bucket);
            }

            // A recipe listing the same item twice must not appear twice in the bucket.
            if (!bucket.Contains(recipe))
                bucket.Add(recipe);
        }

        private static IReadOnlyList<RecipeDefinition> Lookup(
            Dictionary<CraftingId, List<RecipeDefinition>> map,
            in CraftingId key)
        {
            if (key.IsValid && map.TryGetValue(key, out List<RecipeDefinition> bucket))
                return bucket;

            return EmptyRecipes;
        }
    }
}