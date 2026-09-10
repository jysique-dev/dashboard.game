using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoopEngine.CraftingEngine.EditorTools
{
    /// <summary>
    /// Inspector for <see cref="MachineDefinition"/>: which categories it provides, how much
    /// work it can do at once, its upgrade slots, and the recipes that end up runnable here.
    /// </summary>
    [CustomEditor(typeof(MachineDefinition))]
    [CanEditMultipleObjects]
    public sealed class MachineDefinitionEditor : CraftingDefinitionEditor
    {
        private const int MaxListedRecipes = 25;

        private SerializedProperty _categories;
        private SerializedProperty _parallelSlots;
        private SerializedProperty _queueCapacity;
        private SerializedProperty _speedMultiplier;
        private SerializedProperty _modifierSlots;
        private SerializedProperty _allowHandCraftedRecipes;
        private SerializedProperty _icon;

        private readonly List<RecipeDefinition> _runnable = new List<RecipeDefinition>();
        private readonly List<ModifierDefinition> _compatibleModifiers = new List<ModifierDefinition>();

        private bool _cacheDirty = true;

        protected override void OnEnable()
        {
            base.OnEnable();

            _categories = serializedObject.FindProperty("_categories");
            _parallelSlots = serializedObject.FindProperty("_parallelSlots");
            _queueCapacity = serializedObject.FindProperty("_queueCapacity");
            _speedMultiplier = serializedObject.FindProperty("_speedMultiplier");
            _modifierSlots = serializedObject.FindProperty("_modifierSlots");
            _allowHandCraftedRecipes = serializedObject.FindProperty("_allowHandCraftedRecipes");
            _icon = serializedObject.FindProperty("_icon");

            _cacheDirty = true;
        }

        protected override void DrawContent()
        {
            DrawCapabilities();
            DrawThroughput();
            DrawUpgrades();
            DrawPresentation();
            DrawRunnableRecipes();
        }

        protected override void Validate(CraftingIssues issues)
        {
            var machine = (MachineDefinition)target;

            ValidateCategories(machine, issues);

            if (!machine.IsValid)
            {
                issues.Error(
                    "This machine provides no category and does not allow hand crafted recipes, "
                    + "so it can never run anything. It will not be registered.",
                    machine);
            }

            if (machine.QueueCapacity == 0)
            {
                issues.Info(
                    "Queue capacity is 0. Enqueue always fails; only direct TryStart calls work, "
                    + "and only while a slot is free.",
                    machine);
            }

            RefreshCacheIfNeeded();

            if (_runnable.Count == 0 && machine.IsValid)
            {
                issues.Warning(
                    "No recipe can run on this machine. Either no recipe requires its categories, "
                    + "or those recipes have not been authored yet.",
                    machine);
            }

            if (machine.ModifierSlots == 0 && _compatibleModifiers.Count > 0)
            {
                issues.Info(
                    $"{_compatibleModifiers.Count} modifier(s) accept this machine, but it has 0 upgrade slots, "
                    + "so none of them can ever be installed.",
                    machine);
            }
        }

        // --- Sections -----------------------------------------------------------------

        private void DrawCapabilities()
        {
            CraftingEditorGUI.Section("Capabilities");

            EditorGUILayout.LabelField(
                "Recipes ask for a category, never for a machine. Listing a category here is what "
                + "makes every recipe requiring it runnable on this machine.",
                CraftingEditorGUI.Subtle);

            if (CraftingEditorGUI.ObjectArray(_categories, "Categories", "Capabilities this machine provides."))
            {
                _cacheDirty = true;
                InvalidateValidation();
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(
                _allowHandCraftedRecipes,
                new GUIContent("Allow Hand Crafted", "Also run recipes that require no category at all."));

            if (EditorGUI.EndChangeCheck())
            {
                _cacheDirty = true;
                InvalidateValidation();
            }
        }

        private void DrawThroughput()
        {
            CraftingEditorGUI.Section("Throughput");

            EditorGUILayout.PropertyField(
                _parallelSlots,
                new GUIContent("Parallel Slots", "Jobs that can run at the same time."));

            EditorGUILayout.PropertyField(
                _queueCapacity,
                new GUIContent("Queue Capacity", "Orders that can wait for a free slot. 0 disables queueing."));

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(
                _speedMultiplier,
                new GUIContent("Speed Multiplier", "2 means every recipe takes half its authored time here."));

            if (EditorGUI.EndChangeCheck())
                InvalidateValidation();

            float speed = Mathf.Max(0.01f, _speedMultiplier.floatValue);
            EditorGUILayout.LabelField(
                $"A 10s recipe takes {10f / speed:0.##}s here, before upgrades.",
                CraftingEditorGUI.Subtle);
        }

        private void DrawUpgrades()
        {
            CraftingEditorGUI.Section("Upgrades");

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(
                _modifierSlots,
                new GUIContent("Modifier Slots", "How many upgrade modifiers can be installed. 0 disables upgrades."));

            if (EditorGUI.EndChangeCheck())
                InvalidateValidation();

            RefreshCacheIfNeeded();
            CraftingEditorGUI.ReferenceList("Accepted modifiers", _compatibleModifiers);
        }

        private void DrawPresentation()
        {
            CraftingEditorGUI.Section("Presentation");
            EditorGUILayout.PropertyField(
                _icon,
                new GUIContent("Icon", "For your UI. The crafting system never reads this."));
        }

        private void DrawRunnableRecipes()
        {
            CraftingEditorGUI.Section("Recipes");

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.Width(60f)))
                {
                    _cacheDirty = true;
                    InvalidateValidation();
                }
            }

            RefreshCacheIfNeeded();

            if (_runnable.Count <= MaxListedRecipes)
            {
                CraftingEditorGUI.ReferenceList("Runnable here", _runnable);
                return;
            }

            EditorGUILayout.LabelField($"Runnable here ({_runnable.Count})", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(
                $"   showing the first {MaxListedRecipes}",
                CraftingEditorGUI.Subtle);

            using (new EditorGUI.DisabledScope(true))
            {
                for (int i = 0; i < MaxListedRecipes; i++)
                    EditorGUILayout.ObjectField(_runnable[i], typeof(RecipeDefinition), false);
            }
        }

        // --- Data ---------------------------------------------------------------------

        private void RefreshCacheIfNeeded()
        {
            if (!_cacheDirty)
                return;

            _cacheDirty = false;
            _runnable.Clear();
            _compatibleModifiers.Clear();

            var machine = target as MachineDefinition;
            if (machine == null)
                return;

            List<RecipeDefinition> recipes = CraftingAssetUtility.FindAll<RecipeDefinition>();
            for (int i = 0; i < recipes.Count; i++)
            {
                if (machine.CanRun(recipes[i]))
                    _runnable.Add(recipes[i]);
            }

            List<ModifierDefinition> modifiers = CraftingAssetUtility.FindAll<ModifierDefinition>();
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i].AppliesTo(machine))
                    _compatibleModifiers.Add(modifiers[i]);
            }
        }

        private static void ValidateCategories(MachineDefinition machine, CraftingIssues issues)
        {
            MachineCategoryDefinition[] categories = machine.AuthoredCategories;
            var seen = new HashSet<string>(System.StringComparer.Ordinal);

            for (int i = 0; i < categories.Length; i++)
            {
                MachineCategoryDefinition category = categories[i];

                if (category == null)
                {
                    issues.Warning($"Category slot {i + 1} is empty. It is skipped when the machine is baked.", machine);
                    continue;
                }

                if (!category.Id.IsValid)
                {
                    issues.Error($"Category '{category.name}' has an empty id, so it cannot be matched.", category);
                    continue;
                }

                if (!seen.Add(category.RawId))
                    issues.Warning($"Category '{category.DisplayName}' is listed twice. The duplicate has no effect.", machine);
            }
        }
    }
}