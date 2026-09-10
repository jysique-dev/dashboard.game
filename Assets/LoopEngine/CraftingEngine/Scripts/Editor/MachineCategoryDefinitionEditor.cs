using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoopEngine.CraftingEngine.EditorTools
{
    /// <summary>
    /// Inspector for <see cref="MachineCategoryDefinition"/>. The asset itself holds almost no
    /// data, so the useful part is showing both sides of the relationship it exists to connect:
    /// the recipes that require it and the machines that provide it.
    /// </summary>
    [CustomEditor(typeof(MachineCategoryDefinition))]
    [CanEditMultipleObjects]
    public sealed class MachineCategoryDefinitionEditor : CraftingDefinitionEditor
    {
        private SerializedProperty _description;

        private readonly List<RecipeDefinition> _requiredBy = new List<RecipeDefinition>();
        private readonly List<MachineDefinition> _providedBy = new List<MachineDefinition>();

        private bool _cacheDirty = true;

        protected override void OnEnable()
        {
            base.OnEnable();

            _description = serializedObject.FindProperty("_description");
            _cacheDirty = true;
        }

        protected override void DrawContent()
        {
            EditorGUILayout.PropertyField(_description);

            CraftingEditorGUI.Section("Relationships");

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    "A category is the only thing standing between a recipe and a machine.",
                    CraftingEditorGUI.Subtle);

                if (GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.Width(60f)))
                {
                    _cacheDirty = true;
                    InvalidateValidation();
                }
            }

            RefreshCacheIfNeeded();

            CraftingEditorGUI.ReferenceList("Required by recipes", _requiredBy);
            CraftingEditorGUI.ReferenceList("Provided by machines", _providedBy);
        }

        protected override void Validate(CraftingIssues issues)
        {
            var category = (MachineCategoryDefinition)target;

            RefreshCacheIfNeeded();

            if (_requiredBy.Count > 0 && _providedBy.Count == 0)
            {
                issues.Error(
                    $"{_requiredBy.Count} recipe(s) require this category but no machine provides it. "
                    + "Those recipes are unreachable.",
                    category);
            }
            else if (_providedBy.Count == 0)
            {
                issues.Info("No machine provides this category yet.", category);
            }

            if (_requiredBy.Count == 0)
                issues.Info("No recipe requires this category. Harmless, but it does nothing.", category);
        }

        private void RefreshCacheIfNeeded()
        {
            if (!_cacheDirty)
                return;

            _cacheDirty = false;
            _requiredBy.Clear();
            _providedBy.Clear();

            var category = target as MachineCategoryDefinition;
            if (category == null)
                return;

            CraftingId id = category.Id;
            if (!id.IsValid)
                return;

            List<RecipeDefinition> recipes = CraftingAssetUtility.FindAll<RecipeDefinition>();
            for (int i = 0; i < recipes.Count; i++)
            {
                if (recipes[i].RequiredCategoryId.Equals(id))
                    _requiredBy.Add(recipes[i]);
            }

            List<MachineDefinition> machines = CraftingAssetUtility.FindAll<MachineDefinition>();
            for (int i = 0; i < machines.Count; i++)
            {
                if (machines[i].HasCategory(in id))
                    _providedBy.Add(machines[i]);
            }
        }
    }
}