using System;
using UnityEngine;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// A crafting station: furnace, oven, assembler, workbench.
    /// This asset is pure declaration — capabilities and capacities. Which job is running
    /// in which slot is runtime state and lives in the machine instance (session 6).
    /// </summary>
    [CreateAssetMenu(fileName = "Machine_", menuName = LoopRoutes.CraftRoute+ "/Machine", order = 130)]
    public sealed class MachineDefinition : CraftingDefinition
    {
        private static readonly CraftingId[] EmptyCategories = new CraftingId[0];

        [SerializeField, Tooltip("Capabilities this machine provides. A recipe runs here if its required category is listed.")]
        private MachineCategoryDefinition[] _categories = Array.Empty<MachineCategoryDefinition>();

        [SerializeField, Min(1), Tooltip("How many jobs can run at the same time.")]
        private int _parallelSlots = 1;

        [SerializeField, Min(0), Tooltip("Jobs that can wait for a free slot. 0 disables queueing.")]
        private int _queueCapacity = 4;

        [SerializeField, Min(0.01f), Tooltip("Base speed. 2 means every recipe takes half its authored time here.")]
        private float _speedMultiplier = 1f;

        [SerializeField, Min(0), Tooltip("How many upgrade modifiers can be installed here. 0 disables upgrades.")]
        private int _modifierSlots;

        [SerializeField, Tooltip("When true, recipes that require no category can also be run on this machine.")]
        private bool _allowHandCraftedRecipes;

        [SerializeField, Tooltip("Optional. The crafting system never reads this; it is here for your UI.")]
        private Sprite _icon;

        [NonSerialized] private CraftingId[] _categoryIds;

        /// <summary>Authored category assets, for the editor and the validator.</summary>
        public MachineCategoryDefinition[] AuthoredCategories
            => _categories ?? Array.Empty<MachineCategoryDefinition>();

        /// <summary>Hashed category ids, built once on first access. Never null.</summary>
        public CraftingId[] Categories
        {
            get
            {
                if (_categoryIds == null)
                    BuildCategories();

                return _categoryIds;
            }
        }

        /// <summary>Concurrent jobs. Always at least 1.</summary>
        public int ParallelSlots => _parallelSlots < 1 ? 1 : _parallelSlots;

        /// <summary>Waiting jobs allowed beyond the running ones. Zero means no queue.</summary>
        public int QueueCapacity => _queueCapacity < 0 ? 0 : _queueCapacity;

        /// <summary>Base speed factor applied to every recipe run here.</summary>
        public float SpeedMultiplier => _speedMultiplier < 0.01f ? 0.01f : _speedMultiplier;

        /// <summary>Upgrade modifiers this machine can hold. Zero means no upgrade slots.</summary>
        public int ModifierSlots => _modifierSlots < 0 ? 0 : _modifierSlots;

        /// <summary>Whether category-less recipes are accepted here.</summary>
        public bool AllowsHandCraftedRecipes => _allowHandCraftedRecipes;

        public Sprite Icon => _icon;

        /// <summary>O(n) over a handful of categories.</summary>
        public bool HasCategory(in CraftingId category)
        {
            if (!category.IsValid)
                return false;

            CraftingId[] categories = Categories;
            for (int i = 0; i < categories.Length; i++)
            {
                if (categories[i].Equals(category))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Whether this machine is allowed to run the recipe. Capability check only:
        /// it says nothing about free slots, available inputs or output space.
        /// </summary>
        public bool CanRun(RecipeDefinition recipe)
        {
            if (recipe == null)
                return false;

            if (recipe.IsHandCrafted)
                return _allowHandCraftedRecipes;

            return HasCategory(recipe.RequiredCategoryId);
        }

        /// <summary>
        /// Effective duration of a recipe on this machine, before per-instance modifiers.
        /// </summary>
        public float GetCraftSeconds(RecipeDefinition recipe)
            => recipe == null ? 0f : recipe.CraftSeconds / SpeedMultiplier;

        /// <summary>A machine with no category and no hand-craft permission can never run anything.</summary>
        protected override bool ValidateContent()
        {
            if (_allowHandCraftedRecipes)
                return true;

            if (_categories == null)
                return false;

            for (int i = 0; i < _categories.Length; i++)
            {
                if (_categories[i] != null && _categories[i].Id.IsValid)
                    return true;
            }

            return false;
        }

        private void BuildCategories()
        {
            if (_categories == null || _categories.Length == 0)
            {
                _categoryIds = EmptyCategories;
                return;
            }

            int valid = 0;
            for (int i = 0; i < _categories.Length; i++)
            {
                if (_categories[i] != null && _categories[i].Id.IsValid)
                    valid++;
            }

            if (valid == 0)
            {
                _categoryIds = EmptyCategories;
                return;
            }

            var built = new CraftingId[valid];
            int cursor = 0;
            for (int i = 0; i < _categories.Length; i++)
            {
                MachineCategoryDefinition category = _categories[i];
                if (category == null || !category.Id.IsValid)
                    continue;

                built[cursor++] = category.Id;
            }

            _categoryIds = built;
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            if (_parallelSlots < 1)
                _parallelSlots = 1;

            if (_queueCapacity < 0)
                _queueCapacity = 0;

            if (_speedMultiplier < 0.01f)
                _speedMultiplier = 0.01f;

            if (_modifierSlots < 0)
                _modifierSlots = 0;

            _categoryIds = null;
        }

#if UNITY_EDITOR
        internal void EditorSetContent(
            MachineCategoryDefinition[] categories,
            int parallelSlots,
            int queueCapacity,
            float speedMultiplier,
            int modifierSlots,
            bool allowHandCraftedRecipes,
            Sprite icon)
        {
            _categories = categories ?? Array.Empty<MachineCategoryDefinition>();
            _parallelSlots = parallelSlots < 1 ? 1 : parallelSlots;
            _queueCapacity = queueCapacity < 0 ? 0 : queueCapacity;
            _speedMultiplier = speedMultiplier < 0.01f ? 0.01f : speedMultiplier;
            _modifierSlots = modifierSlots < 0 ? 0 : modifierSlots;
            _allowHandCraftedRecipes = allowHandCraftedRecipes;
            _icon = icon;
            _categoryIds = null;
        }
#endif
    }
}