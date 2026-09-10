using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// The asset lists that make up one crafting database.
    /// Drag every definition in here, or let the edit-mode toolkit populate it.
    /// </summary>
    [CreateAssetMenu(fileName = "CraftingDatabase", menuName = LoopRoutes.CraftRoute + "/Database",
        order = 90)]
    public sealed class CraftingDatabase : ScriptableObject
    {
        [SerializeField] private ItemDefinition[] _items = Array.Empty<ItemDefinition>();
        [SerializeField] private RecipeDefinition[] _recipes = Array.Empty<RecipeDefinition>();
        [SerializeField] private MachineCategoryDefinition[] _categories = Array.Empty<MachineCategoryDefinition>();
        [SerializeField] private MachineDefinition[] _machines = Array.Empty<MachineDefinition>();
        [SerializeField] private ModifierDefinition[] _modifiers = Array.Empty<ModifierDefinition>();

        public ItemDefinition[] Items => _items ?? Array.Empty<ItemDefinition>();

        public RecipeDefinition[] Recipes => _recipes ?? Array.Empty<RecipeDefinition>();

        public MachineCategoryDefinition[] Categories => _categories ?? Array.Empty<MachineCategoryDefinition>();

        public MachineDefinition[] Machines => _machines ?? Array.Empty<MachineDefinition>();

        public ModifierDefinition[] Modifiers => _modifiers ?? Array.Empty<ModifierDefinition>();

        /// <summary>
        /// Builds a ready to use context. Definitions that fail validation or repeat an id are
        /// skipped and collected in <paramref name="report"/> when one is supplied.
        /// </summary>
        public CraftingContext BuildContext(ICraftingClock clock = null, CraftingBuildReport report = null)
        {
            var categories = new MachineCategoryManager(Categories, report?.RejectedCategories);
            var items = new ItemManager(Items, report?.RejectedItems);
            var recipes = new RecipeManager(Recipes, report?.RejectedRecipes);
            var machines = new MachineManager(Machines, report?.RejectedMachines);
            var modifiers = new ModifierManager(Modifiers, report?.RejectedModifiers);

            var context = new CraftingContext(items, recipes, machines, categories, modifiers, clock);

            report?.CollectContentProblems(context);
            return context;
        }

#if UNITY_EDITOR
        internal void EditorSetContent(
            ItemDefinition[] items,
            RecipeDefinition[] recipes,
            MachineCategoryDefinition[] categories,
            MachineDefinition[] machines,
            ModifierDefinition[] modifiers)
        {
            _items = items ?? Array.Empty<ItemDefinition>();
            _recipes = recipes ?? Array.Empty<RecipeDefinition>();
            _categories = categories ?? Array.Empty<MachineCategoryDefinition>();
            _machines = machines ?? Array.Empty<MachineDefinition>();
            _modifiers = modifiers ?? Array.Empty<ModifierDefinition>();
        }
#endif
    }

    /// <summary>
    /// What was thrown away while building a context, and what is wrong with what remained.
    /// Optional: pass one to <see cref="CraftingDatabase.BuildContext"/> when you want to know.
    /// </summary>
    public sealed class CraftingBuildReport
    {
        public readonly List<ItemDefinition> RejectedItems = new List<ItemDefinition>();
        public readonly List<RecipeDefinition> RejectedRecipes = new List<RecipeDefinition>();
        public readonly List<MachineCategoryDefinition> RejectedCategories = new List<MachineCategoryDefinition>();
        public readonly List<MachineDefinition> RejectedMachines = new List<MachineDefinition>();
        public readonly List<ModifierDefinition> RejectedModifiers = new List<ModifierDefinition>();

        /// <summary>Recipes referencing an item that never registered.</summary>
        public readonly List<RecipeDefinition> BrokenReferences = new List<RecipeDefinition>();

        /// <summary>Recipes requiring a category no registered machine provides.</summary>
        public readonly List<RecipeDefinition> UnreachableRecipes = new List<RecipeDefinition>();

        public int RejectedCount
            => RejectedItems.Count
             + RejectedRecipes.Count
             + RejectedCategories.Count
             + RejectedMachines.Count
             + RejectedModifiers.Count;

        public int ProblemCount => RejectedCount + BrokenReferences.Count + UnreachableRecipes.Count;

        public bool IsClean => ProblemCount == 0;

        internal void CollectContentProblems(CraftingContext context)
        {
            context.ValidateContent(BrokenReferences, UnreachableRecipes);
        }

        public void Clear()
        {
            RejectedItems.Clear();
            RejectedRecipes.Clear();
            RejectedCategories.Clear();
            RejectedMachines.Clear();
            RejectedModifiers.Clear();
            BrokenReferences.Clear();
            UnreachableRecipes.Clear();
        }
    }
}