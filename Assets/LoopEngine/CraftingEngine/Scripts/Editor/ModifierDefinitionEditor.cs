using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoopEngine.CraftingEngine.EditorTools
{
    /// <summary>
    /// Inspector for <see cref="ModifierDefinition"/>: the stat entries, the bonus outputs, and
    /// a live preview of what installing one, two or the maximum number actually produces.
    /// </summary>
    /// <remarks>
    /// The preview runs the real <see cref="StatAccumulator"/>, not a copy of the maths, so it
    /// cannot drift from what the machine will do at runtime.
    /// </remarks>
    [CustomEditor(typeof(ModifierDefinition))]
    [CanEditMultipleObjects]
    public sealed class ModifierDefinitionEditor : CraftingDefinitionEditor
    {
        private SerializedProperty _carrierItem;
        private SerializedProperty _entries;
        private SerializedProperty _bonusOutputs;
        private SerializedProperty _allowedCategories;
        private SerializedProperty _maxPerMachine;

        private readonly List<MachineDefinition> _acceptedBy = new List<MachineDefinition>();
        private bool _cacheDirty = true;

        protected override void OnEnable()
        {
            base.OnEnable();

            _carrierItem = serializedObject.FindProperty("_carrierItem");
            _entries = serializedObject.FindProperty("_entries");
            _bonusOutputs = serializedObject.FindProperty("_bonusOutputs");
            _allowedCategories = serializedObject.FindProperty("_allowedCategories");
            _maxPerMachine = serializedObject.FindProperty("_maxPerMachine");

            _cacheDirty = true;
        }

        protected override void DrawContent()
        {
            DrawCarrier();
            DrawEntries();
            DrawPreview();
            DrawBonusOutputs();
            DrawCompatibility();
        }

        protected override void Validate(CraftingIssues issues)
        {
            var modifier = (ModifierDefinition)target;

            bool hasEntries = modifier.Entries.Length > 0;
            bool hasBonus = modifier.BonusOutputs.Length > 0;

            if (!hasEntries && !hasBonus)
            {
                issues.Error(
                    "This modifier changes no stat and adds no output, so it does nothing. "
                    + "It will not be registered.",
                    modifier);
            }

            if (modifier.CarrierItem == null)
            {
                issues.Info(
                    "No carrier item. The modifier can still be installed from code, but the player "
                    + "has no item to place in the slot.",
                    modifier);
            }

            ValidateEntries(modifier, issues);
            ValidateBonusOutputs(modifier, issues);

            RefreshCacheIfNeeded();

            if (_acceptedBy.Count == 0)
            {
                issues.Warning(
                    "No machine accepts this modifier. Either no machine provides the allowed "
                    + "categories, or those machines have no upgrade slots.",
                    modifier);
            }
        }

        // --- Sections -----------------------------------------------------------------

        private void DrawCarrier()
        {
            CraftingEditorGUI.Section("Carrier");

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(
                _carrierItem,
                new GUIContent("Carrier Item", "The item the player installs. Optional for built-in upgrades."));

            if (EditorGUI.EndChangeCheck())
                InvalidateValidation();

            EditorGUILayout.LabelField(
                "Installing a modifier does not consume the item. Remove it from the inventory yourself first.",
                CraftingEditorGUI.Subtle);
        }

        private void DrawEntries()
        {
            CraftingEditorGUI.Section("Stat changes");

            EditorGUILayout.LabelField(
                "Additive entries are summed and applied as (1 + total); multiplicative entries "
                + "are applied afterwards. Two +50% modules give x2.0, not x2.25.",
                CraftingEditorGUI.Subtle);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Entries ({_entries.arraySize})", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(22f)))
                {
                    _entries.InsertArrayElementAtIndex(_entries.arraySize);
                    InitialiseEntry(_entries.GetArrayElementAtIndex(_entries.arraySize - 1));
                    InvalidateValidation();
                }
            }

            int removeIndex = -1;

            for (int i = 0; i < _entries.arraySize; i++)
            {
                SerializedProperty element = _entries.GetArrayElementAtIndex(i);
                SerializedProperty stat = element.FindPropertyRelative("_stat");
                SerializedProperty operation = element.FindPropertyRelative("_operation");
                SerializedProperty value = element.FindPropertyRelative("_value");

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();

                    EditorGUILayout.PropertyField(stat, GUIContent.none, GUILayout.MinWidth(60f));
                    EditorGUILayout.PropertyField(operation, GUIContent.none, GUILayout.MinWidth(90f));
                    value.floatValue = EditorGUILayout.FloatField(value.floatValue, GUILayout.Width(60f));

                    if (EditorGUI.EndChangeCheck())
                        InvalidateValidation();

                    GUILayout.Label(
                        (StatOperation)operation.enumValueIndex == StatOperation.Additive ? "(fraction)" : "(factor)",
                        CraftingEditorGUI.Subtle,
                        GUILayout.Width(60f));

                    if (GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(22f)))
                        removeIndex = i;
                }
            }

            if (_entries.arraySize == 0)
                EditorGUILayout.LabelField("   none", CraftingEditorGUI.Subtle);

            if (removeIndex >= 0)
            {
                _entries.DeleteArrayElementAtIndex(removeIndex);
                InvalidateValidation();
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(
                _maxPerMachine,
                new GUIContent("Max Per Machine", "How many copies one machine may hold."));

            if (EditorGUI.EndChangeCheck())
                InvalidateValidation();
        }

        private static void InitialiseEntry(SerializedProperty element)
        {
            // Inserted elements copy the previous one; a fresh entry should start neutral.
            element.FindPropertyRelative("_stat").enumValueIndex = (int)MachineStat.Speed;
            element.FindPropertyRelative("_operation").enumValueIndex = (int)StatOperation.Additive;
            element.FindPropertyRelative("_value").floatValue = 0.25f;
        }

        private void DrawPreview()
        {
            var modifier = target as ModifierDefinition;
            if (modifier == null || modifier.Entries.Length == 0)
                return;

            CraftingEditorGUI.Section("Effect");

            int max = Mathf.Max(1, _maxPerMachine.intValue);
            for (int copies = 1; copies <= max && copies <= 4; copies++)
            {
                MachineStats stats = Resolve(modifier, copies);

                EditorGUILayout.LabelField(
                    $"x{copies} installed:  speed x{stats.Speed:0.###},  yield x{stats.Yield:0.###}"
                    + $"   ·   a 10s recipe takes {10f / stats.Speed:0.##}s",
                    CraftingEditorGUI.Subtle);
            }

            EditorGUILayout.LabelField(
                "Yield truncates: x1.5 on an output of 1 still produces 1.",
                CraftingEditorGUI.Subtle);
        }

        /// <summary>Runs the real accumulator so the preview cannot disagree with runtime.</summary>
        private static MachineStats Resolve(ModifierDefinition modifier, int copies)
        {
            var accumulator = new StatAccumulator();
            accumulator.Begin();

            for (int i = 0; i < copies; i++)
                accumulator.Add(modifier);

            return accumulator.Resolve();
        }

        private void DrawBonusOutputs()
        {
            CraftingEditorGUI.Section("Bonus outputs");

            EditorGUILayout.LabelField(
                "Extra items produced on every completed craft, on top of the recipe's own outputs. "
                + "The yield stat does not multiply them.",
                CraftingEditorGUI.Subtle);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Outputs ({_bonusOutputs.arraySize})", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(22f)))
                {
                    _bonusOutputs.InsertArrayElementAtIndex(_bonusOutputs.arraySize);
                    SerializedProperty added = _bonusOutputs.GetArrayElementAtIndex(_bonusOutputs.arraySize - 1);
                    added.FindPropertyRelative("_item").objectReferenceValue = null;
                    added.FindPropertyRelative("_amount").intValue = 1;
                    InvalidateValidation();
                }
            }

            int removeIndex = -1;

            for (int i = 0; i < _bonusOutputs.arraySize; i++)
            {
                SerializedProperty element = _bonusOutputs.GetArrayElementAtIndex(i);
                SerializedProperty item = element.FindPropertyRelative("_item");
                SerializedProperty amount = element.FindPropertyRelative("_amount");

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();

                    EditorGUILayout.PropertyField(item, GUIContent.none);
                    GUILayout.Label("x", GUILayout.Width(12f));
                    amount.intValue = Mathf.Max(1, EditorGUILayout.IntField(amount.intValue, GUILayout.Width(50f)));

                    if (EditorGUI.EndChangeCheck())
                        InvalidateValidation();

                    if (GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(22f)))
                        removeIndex = i;
                }
            }

            if (_bonusOutputs.arraySize == 0)
                EditorGUILayout.LabelField("   none", CraftingEditorGUI.Subtle);

            if (removeIndex < 0)
                return;

            SerializedProperty removing = _bonusOutputs.GetArrayElementAtIndex(removeIndex);
            if (removing.FindPropertyRelative("_item").objectReferenceValue != null)
                removing.FindPropertyRelative("_item").objectReferenceValue = null;

            _bonusOutputs.DeleteArrayElementAtIndex(removeIndex);
            InvalidateValidation();
        }

        private void DrawCompatibility()
        {
            CraftingEditorGUI.Section("Compatibility");

            if (CraftingEditorGUI.ObjectArray(
                    _allowedCategories,
                    "Allowed categories",
                    "Machine categories that accept this modifier. Empty means any machine."))
            {
                _cacheDirty = true;
                InvalidateValidation();
            }

            if (_allowedCategories.arraySize == 0)
                EditorGUILayout.LabelField("Empty list means every machine accepts it.", CraftingEditorGUI.Subtle);

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
            CraftingEditorGUI.ReferenceList("Accepted by machines", _acceptedBy);
        }

        // --- Data ---------------------------------------------------------------------

        private void RefreshCacheIfNeeded()
        {
            if (!_cacheDirty)
                return;

            _cacheDirty = false;
            _acceptedBy.Clear();

            var modifier = target as ModifierDefinition;
            if (modifier == null)
                return;

            List<MachineDefinition> machines = CraftingAssetUtility.FindAll<MachineDefinition>();
            for (int i = 0; i < machines.Count; i++)
            {
                MachineDefinition machine = machines[i];

                // A machine with no upgrade slots cannot hold it, however compatible it is.
                if (machine.ModifierSlots > 0 && modifier.AppliesTo(machine))
                    _acceptedBy.Add(machine);
            }
        }

        private static void ValidateEntries(ModifierDefinition modifier, CraftingIssues issues)
        {
            StatModifierEntry[] entries = modifier.Entries;

            for (int i = 0; i < entries.Length; i++)
            {
                StatModifierEntry entry = entries[i];

                if (entry.Operation == StatOperation.Multiplicative && entry.Value <= 0f)
                {
                    issues.Error(
                        $"Entry {i + 1} multiplies {entry.Stat} by {entry.Value}. A factor of zero or less "
                        + "would stop the machine or reverse it; speed is clamped at 0.01 to avoid a freeze.",
                        modifier);
                }

                if (entry.Operation == StatOperation.Additive && entry.Value <= -1f)
                {
                    issues.Error(
                        $"Entry {i + 1} adds {entry.Value} to {entry.Stat}, which drives the result to zero or below.",
                        modifier);
                }

                if (Mathf.Approximately(entry.Value, 0f) && entry.Operation == StatOperation.Additive)
                    issues.Warning($"Entry {i + 1} adds 0 to {entry.Stat}, so it does nothing.", modifier);
            }
        }

        private static void ValidateBonusOutputs(ModifierDefinition modifier, CraftingIssues issues)
        {
            // Baking keeps the array length and turns invalid entries into empty stacks,
            // so an empty slot here means the authored entry has no item or a bad amount.
            ItemStack[] baked = modifier.BonusOutputs;

            for (int i = 0; i < baked.Length; i++)
            {
                if (baked[i].IsEmpty)
                {
                    issues.Error(
                        $"Bonus output {i + 1} has no usable item. The modifier will not be registered.",
                        modifier);
                }
            }
        }
    }
}