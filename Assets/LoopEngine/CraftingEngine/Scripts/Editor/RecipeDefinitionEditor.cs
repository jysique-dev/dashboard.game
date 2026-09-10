using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoopEngine.CraftingEngine.EditorTools
{
    /// <summary>
    /// Inspector for <see cref="RecipeDefinition"/>: the n to m transformation, its timing,
    /// the machine category it needs, and which machines can actually run it.
    /// </summary>
    [CustomEditor(typeof(RecipeDefinition))]
    [CanEditMultipleObjects]
    public sealed class RecipeDefinitionEditor : CraftingDefinitionEditor
    {
        private SerializedProperty _inputs;
        private SerializedProperty _outputs;
        private SerializedProperty _craftSeconds;
        private SerializedProperty _requiredCategory;

        private readonly List<MachineDefinition> _runnableOn = new List<MachineDefinition>();
        private bool _machinesDirty = true;

        protected override void OnEnable()
        {
            base.OnEnable();

            _inputs = serializedObject.FindProperty("_inputs");
            _outputs = serializedObject.FindProperty("_outputs");
            _craftSeconds = serializedObject.FindProperty("_craftSeconds");
            _requiredCategory = serializedObject.FindProperty("_requiredCategory");

            _machinesDirty = true;
        }

        protected override void DrawContent()
        {
            DrawSummary();
            DrawTransformation();
            DrawTiming();
            DrawMachine();
        }

        protected override void Validate(CraftingIssues issues)
        {
            var recipe = (RecipeDefinition)target;

            ValidateAmounts(recipe.AuthoredInputs, "Input", issues, recipe);
            ValidateAmounts(recipe.AuthoredOutputs, "Output", issues, recipe);

            if (recipe.AuthoredOutputs.Length == 0)
            {
                issues.Error(
                    "A recipe with no outputs would consume its inputs forever and produce nothing. "
                    + "It will not be registered.",
                    recipe);
            }

            if (recipe.AuthoredInputs.Length == 0)
            {
                issues.Info(
                    "No inputs: this recipe creates something out of nothing. Legitimate for generators, "
                    + "worth a second look otherwise.",
                    recipe);
            }

            ValidateOverlap(recipe, issues);

            if (recipe.CraftSeconds <= 0f)
            {
                issues.Info(
                    "Duration is zero, so the job completes on the same tick it starts. "
                    + "Batches are capped at 64 repetitions per tick to protect the frame.",
                    recipe);
            }

            ValidateReachability(recipe, issues);
        }

        // --- Sections -----------------------------------------------------------------

        private void DrawSummary()
        {
            var recipe = (RecipeDefinition)target;

            EditorGUILayout.LabelField(BuildSummary(recipe), EditorStyles.wordWrappedLabel);
        }

        private void DrawTransformation()
        {
            CraftingEditorGUI.Section("Transformation");

            EditorGUILayout.LabelField(
                "Any number of inputs to any number of outputs. Amounts are per single craft.",
                CraftingEditorGUI.Subtle);

            DrawAmountList(_inputs, "Inputs", "Consumed when each repetition starts.");
            EditorGUILayout.Space(4f);
            DrawAmountList(_outputs, "Outputs", "Produced when each repetition completes. At least one is required.");
        }

        private void DrawAmountList(SerializedProperty list, string label, string tooltip)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent($"{label} ({list.arraySize})", tooltip), EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(22f)))
                {
                    list.InsertArrayElementAtIndex(list.arraySize);
                    InitialiseAmount(list.GetArrayElementAtIndex(list.arraySize - 1));
                    InvalidateValidation();
                }
            }

            int removeIndex = -1;

            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
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

            if (list.arraySize == 0)
                EditorGUILayout.LabelField("   none", CraftingEditorGUI.Subtle);

            if (removeIndex < 0)
                return;

            // An element holding an object reference needs two deletes in Unity's serialized
            // arrays: the first clears the reference, the second removes the slot.
            SerializedProperty removing = list.GetArrayElementAtIndex(removeIndex);
            if (removing.FindPropertyRelative("_item").objectReferenceValue != null)
                removing.FindPropertyRelative("_item").objectReferenceValue = null;

            list.DeleteArrayElementAtIndex(removeIndex);
            InvalidateValidation();
        }

        private static void InitialiseAmount(SerializedProperty element)
        {
            // A freshly inserted array element copies the previous one, or is zeroed for the
            // first entry. Neither is a usable default.
            element.FindPropertyRelative("_item").objectReferenceValue = null;
            element.FindPropertyRelative("_amount").intValue = 1;
        }

        private void DrawTiming()
        {
            CraftingEditorGUI.Section("Timing");

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(
                _craftSeconds,
                new GUIContent("Craft Seconds", "Base duration at 1x speed, before machine speed and modifiers."));

            if (EditorGUI.EndChangeCheck())
                InvalidateValidation();

            var recipe = (RecipeDefinition)target;
            MachineDefinition machine = _requiredCategory.objectReferenceValue != null
                ? FindFastestMachine(recipe)
                : null;

            if (machine != null)
            {
                EditorGUILayout.LabelField(
                    $"On '{machine.DisplayName}' (speed x{machine.SpeedMultiplier:0.##}): "
                    + $"{machine.GetCraftSeconds(recipe):0.##}s per craft, before upgrades.",
                    CraftingEditorGUI.Subtle);
            }
        }

        private void DrawMachine()
        {
            CraftingEditorGUI.Section("Machine");

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(
                _requiredCategory,
                new GUIContent("Required Category", "Leave empty for hand crafting, with no machine involved."));

            if (EditorGUI.EndChangeCheck())
            {
                _machinesDirty = true;
                InvalidateValidation();
            }

            if (_requiredCategory.objectReferenceValue == null)
            {
                EditorGUILayout.LabelField(
                    "Hand crafted. Only machines with 'Allow Hand Crafted Recipes' can also run it.",
                    CraftingEditorGUI.Subtle);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Runs on", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.Width(60f)))
                {
                    _machinesDirty = true;
                    InvalidateValidation();
                }
            }

            RefreshMachinesIfNeeded();

            if (_runnableOn.Count == 0)
            {
                EditorGUILayout.LabelField("   no machine can run this recipe", CraftingEditorGUI.Subtle);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                for (int i = 0; i < _runnableOn.Count; i++)
                    EditorGUILayout.ObjectField(_runnableOn[i], typeof(MachineDefinition), false);
            }
        }

        // --- Data ---------------------------------------------------------------------

        private void RefreshMachinesIfNeeded()
        {
            if (!_machinesDirty)
                return;

            _machinesDirty = false;
            _runnableOn.Clear();

            var recipe = target as RecipeDefinition;
            if (recipe == null)
                return;

            List<MachineDefinition> machines = CraftingAssetUtility.FindAll<MachineDefinition>();
            for (int i = 0; i < machines.Count; i++)
            {
                if (machines[i].CanRun(recipe))
                    _runnableOn.Add(machines[i]);
            }
        }

        private MachineDefinition FindFastestMachine(RecipeDefinition recipe)
        {
            RefreshMachinesIfNeeded();

            MachineDefinition best = null;
            for (int i = 0; i < _runnableOn.Count; i++)
            {
                if (best == null || _runnableOn[i].SpeedMultiplier > best.SpeedMultiplier)
                    best = _runnableOn[i];
            }

            return best;
        }

        private static string BuildSummary(RecipeDefinition recipe)
        {
            var builder = new System.Text.StringBuilder(96);

            AppendSide(builder, recipe.AuthoredInputs, "nothing");
            builder.Append("  ->  ");
            AppendSide(builder, recipe.AuthoredOutputs, "nothing");

            builder.Append("   (").Append(recipe.CraftSeconds.ToString("0.##")).Append("s");

            if (recipe.RequiredCategory != null)
                builder.Append(", ").Append(recipe.RequiredCategory.DisplayName);
            else
                builder.Append(", by hand");

            builder.Append(')');
            return builder.ToString();
        }

        private static void AppendSide(System.Text.StringBuilder builder, ItemAmount[] amounts, string emptyLabel)
        {
            if (amounts == null || amounts.Length == 0)
            {
                builder.Append(emptyLabel);
                return;
            }

            for (int i = 0; i < amounts.Length; i++)
            {
                if (i > 0)
                    builder.Append(" + ");

                ItemAmount amount = amounts[i];
                builder.Append(amount.Amount).Append(' ');
                builder.Append(amount.Item != null ? amount.Item.DisplayName : "<missing>");
            }
        }

        // --- Validation ---------------------------------------------------------------

        private static void ValidateAmounts(ItemAmount[] amounts, string side, CraftingIssues issues, Object context)
        {
            if (amounts == null)
                return;

            var seen = new HashSet<string>(System.StringComparer.Ordinal);

            for (int i = 0; i < amounts.Length; i++)
            {
                ItemAmount amount = amounts[i];

                if (amount.Item == null)
                {
                    issues.Error($"{side} {i + 1} has no item assigned. The recipe will not be registered.", context);
                    continue;
                }

                if (!amount.Item.Id.IsValid)
                {
                    issues.Error($"{side} {i + 1} points at '{amount.Item.name}', whose id is empty.", amount.Item);
                    continue;
                }

                if (amount.Amount < 1)
                    issues.Error($"{side} {i + 1} has an amount of {amount.Amount}.", context);

                if (!seen.Add(amount.Item.RawId))
                {
                    issues.Warning(
                        $"'{amount.Item.DisplayName}' appears twice in {side.ToLowerInvariant()}s. "
                        + "The entries are not merged; they are processed as two separate transfers.",
                        context);
                }
            }
        }

        private static void ValidateOverlap(RecipeDefinition recipe, CraftingIssues issues)
        {
            ItemAmount[] inputs = recipe.AuthoredInputs;
            ItemAmount[] outputs = recipe.AuthoredOutputs;

            for (int i = 0; i < inputs.Length; i++)
            {
                if (inputs[i].Item == null)
                    continue;

                for (int o = 0; o < outputs.Length; o++)
                {
                    if (outputs[o].Item != inputs[i].Item)
                        continue;

                    int net = outputs[o].Amount - inputs[i].Amount;
                    string verdict = net > 0
                        ? $"net gain of {net} per craft"
                        : net < 0 ? $"net loss of {-net} per craft" : "no net change";

                    issues.Warning(
                        $"'{inputs[i].Item.DisplayName}' is both consumed and produced: {verdict}. "
                        + "Fine for catalysts and upgrades, a loop with no purpose otherwise.",
                        recipe);
                }
            }
        }

        private static void ValidateReachability(RecipeDefinition recipe, CraftingIssues issues)
        {
            if (recipe.IsHandCrafted)
                return;

            if (recipe.RequiredCategory == null)
                return;

            List<MachineDefinition> machines = CraftingAssetUtility.FindAll<MachineDefinition>();
            for (int i = 0; i < machines.Count; i++)
            {
                if (machines[i].CanRun(recipe))
                    return;
            }

            issues.Warning(
                $"No machine provides the category '{recipe.RequiredCategory.DisplayName}'. "
                + "This recipe is dead content until one does.",
                recipe);
        }
    }
}