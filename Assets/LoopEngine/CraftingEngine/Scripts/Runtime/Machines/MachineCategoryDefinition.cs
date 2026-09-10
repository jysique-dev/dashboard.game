using UnityEngine;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// A capability label that machines provide and recipes require:
    /// "furnace", "oven", "assembler", "grill".
    /// Recipes never point at a concrete machine, only at a category, so you can add a
    /// better furnace later without touching a single recipe.
    /// </summary>
    [CreateAssetMenu(fileName = "Category_", menuName = LoopRoutes.CraftRoute + "/Machine Category", order = 120)]
    public sealed class MachineCategoryDefinition : CraftingDefinition
    {
        [SerializeField, TextArea(2, 4)]
        private string _description = string.Empty;

        public string Description => _description ?? string.Empty;

#if UNITY_EDITOR
        internal void EditorSetContent(string description)
        {
            _description = description ?? string.Empty;
        }
#endif
    }
}