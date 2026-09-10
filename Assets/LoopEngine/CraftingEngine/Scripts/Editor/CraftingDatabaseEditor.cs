using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoopEngine.CraftingEngine.EditorTools
{
    /// <summary>
    /// Inspector for <see cref="CraftingDatabase"/>. Keeping five arrays in sync by hand is
    /// where content goes missing, so this fills them from the project and reports what changed.
    /// </summary>
    /// <remarks>
    /// Everything is written through <c>SerializedProperty</c> rather than the internal setter,
    /// which means Undo, prefab overrides and dirty marking all work without extra code.
    /// </remarks>
    [CustomEditor(typeof(CraftingDatabase))]
    public sealed class CraftingDatabaseEditor : Editor
    {
        private SerializedProperty _items;
        private SerializedProperty _categories;
        private SerializedProperty _recipes;
        private SerializedProperty _machines;
        private SerializedProperty _modifiers;

        private readonly CraftingIssues _issues = new CraftingIssues();
        private bool _showArrays;
        private bool _dirty = true;

        private void OnEnable()
        {
            _items = serializedObject.FindProperty("_items");
            _categories = serializedObject.FindProperty("_categories");
            _recipes = serializedObject.FindProperty("_recipes");
            _machines = serializedObject.FindProperty("_machines");
            _modifiers = serializedObject.FindProperty("_modifiers");

            _dirty = true;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawContents();
            DrawActions();

            RefreshIssuesIfNeeded();
            CraftingEditorGUI.DrawIssues(_issues);

            CraftingEditorGUI.Separator();
            DrawArrays();

            if (serializedObject.ApplyModifiedProperties())
                _dirty = true;
        }

        // --- Sections ---------------------------------------------------------------------

        private void DrawContents()
        {
            CraftingEditorGUI.Section("Contents");

            DrawSectionRow("Items", _items, typeof(ItemDefinition));
            DrawSectionRow("Categories", _categories, typeof(MachineCategoryDefinition));
            DrawSectionRow("Recipes", _recipes, typeof(RecipeDefinition));
            DrawSectionRow("Machines", _machines, typeof(MachineDefinition));
            DrawSectionRow("Modifiers", _modifiers, typeof(ModifierDefinition));
        }

        private void DrawSectionRow(string label, SerializedProperty list, Type definitionType)
        {
            int inProject = CraftingAssetUtility.FindAll(definitionType).Count;
            int listed = list.arraySize;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(90f));

                string status = listed == inProject
                    ? $"{listed}"
                    : $"{listed}  of {inProject} in project";

                EditorGUILayout.LabelField(status, listed < inProject
                    ? EditorStyles.miniBoldLabel
                    : EditorStyles.miniLabel);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Collect", EditorStyles.miniButton, GUILayout.Width(60f)))
                {
                    CollectSection(list, definitionType);
                    _dirty = true;
                }
            }
        }

        private void DrawActions()
        {
            CraftingEditorGUI.Section("Actions");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Collect Everything"))
                {
                    CollectAll();
                    _dirty = true;
                }

                if (GUILayout.Button("Remove Empty Slots"))
                {
                    int removed = RemoveEmptySlots();
                    Debug.Log($"[Crafting] Removed {removed} empty slot(s) from '{target.name}'.", target);
                    _dirty = true;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Clear All"))
                {
                    if (EditorUtility.DisplayDialog(
                            "Clear database",
                            $"Remove every entry from '{target.name}'?\n\n"
                            + "The assets themselves are not deleted, only their listing here.",
                            "Clear",
                            "Cancel"))
                    {
                        ClearAll();
                        _dirty = true;
                    }
                }

                if (GUILayout.Button("Validate Project"))
                    CraftingValidationMenu.ValidateProject();
            }

            EditorGUILayout.LabelField(
                "Collecting adds every asset of that type found anywhere in the project, replacing "
                + "the current list. A database that is meant to be a subset should be filled by hand.",
                CraftingEditorGUI.Subtle);
        }

        private void DrawArrays()
        {
            _showArrays = EditorGUILayout.Foldout(_showArrays, "Lists", true);
            if (!_showArrays)
                return;

            EditorGUILayout.PropertyField(_items, true);
            EditorGUILayout.PropertyField(_categories, true);
            EditorGUILayout.PropertyField(_recipes, true);
            EditorGUILayout.PropertyField(_machines, true);
            EditorGUILayout.PropertyField(_modifiers, true);
        }

        // --- Operations -------------------------------------------------------------------

        private void CollectAll()
        {
            CollectSection(_items, typeof(ItemDefinition));
            CollectSection(_categories, typeof(MachineCategoryDefinition));
            CollectSection(_recipes, typeof(RecipeDefinition));
            CollectSection(_machines, typeof(MachineDefinition));
            CollectSection(_modifiers, typeof(ModifierDefinition));
        }

        /// <summary>
        /// Replaces a list with every asset of that type in the project, sorted by id.
        /// Assets with no id are skipped: they would never register anyway.
        /// </summary>
        private void CollectSection(SerializedProperty list, Type definitionType)
        {
            List<CraftingDefinition> found = CraftingAssetUtility.FindAll(definitionType);

            var usable = new List<CraftingDefinition>(found.Count);
            int skipped = 0;

            for (int i = 0; i < found.Count; i++)
            {
                if (string.IsNullOrEmpty(found[i].RawId))
                {
                    skipped++;
                    continue;
                }

                usable.Add(found[i]);
            }

            int before = list.arraySize;

            list.ClearArray();
            list.arraySize = usable.Count;

            for (int i = 0; i < usable.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = usable[i];

            serializedObject.ApplyModifiedProperties();

            string message = $"[Crafting] '{target.name}' {definitionType.Name}: {before} -> {usable.Count}.";
            if (skipped > 0)
                message += $" {skipped} asset(s) skipped for having no id.";

            Debug.Log(message, target);
        }

        private int RemoveEmptySlots()
        {
            int removed = 0;

            removed += RemoveEmpty(_items);
            removed += RemoveEmpty(_categories);
            removed += RemoveEmpty(_recipes);
            removed += RemoveEmpty(_machines);
            removed += RemoveEmpty(_modifiers);

            serializedObject.ApplyModifiedProperties();
            return removed;
        }

        private static int RemoveEmpty(SerializedProperty list)
        {
            int removed = 0;

            // Backwards, so removing an entry cannot shift one that has not been checked yet.
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue != null)
                    continue;

                list.DeleteArrayElementAtIndex(i);
                removed++;
            }

            return removed;
        }

        private void ClearAll()
        {
            _items.ClearArray();
            _categories.ClearArray();
            _recipes.ClearArray();
            _machines.ClearArray();
            _modifiers.ClearArray();

            serializedObject.ApplyModifiedProperties();
        }

        // --- Validation -------------------------------------------------------------------

        private void RefreshIssuesIfNeeded()
        {
            if (!_dirty)
                return;

            _dirty = false;
            _issues.Clear();

            var database = (CraftingDatabase)target;

            CheckSection(database.Items, "item", _issues);
            CheckSection(database.Categories, "category", _issues);
            CheckSection(database.Recipes, "recipe", _issues);
            CheckSection(database.Machines, "machine", _issues);
            CheckSection(database.Modifiers, "modifier", _issues);

            if (database.Items.Length == 0 && database.Recipes.Length == 0)
            {
                _issues.Error(
                    "This database lists no items and no recipes, so building a context from it "
                    + "produces an empty system.",
                    database);
            }
        }

        private static void CheckSection<T>(T[] listed, string section, CraftingIssues issues)
            where T : CraftingDefinition
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int empty = 0;

            for (int i = 0; i < listed.Length; i++)
            {
                T entry = listed[i];

                if (entry == null)
                {
                    empty++;
                    continue;
                }

                if (string.IsNullOrEmpty(entry.RawId))
                {
                    issues.Error($"{section} '{entry.name}' has no id and will not register.", entry);
                    continue;
                }

                if (!seen.Add(entry.RawId))
                {
                    issues.Warning(
                        $"{section} '{entry.RawId}' is listed twice. The second one is rejected as a duplicate id.",
                        entry);
                }
            }

            if (empty > 0)
                issues.Warning($"{empty} empty {section} slot(s). Use Remove Empty Slots to clean them up.");
        }
    }
}