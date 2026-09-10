using UnityEditor;
using UnityEngine;

namespace LoopEngine.CraftingEngine.EditorTools
{
    /// <summary>
    /// Base custom inspector for every <see cref="CraftingDefinition"/>.
    /// Draws the identity header, runs validation and then hands over to the concrete editor.
    /// </summary>
    /// <remarks>
    /// Registered for child classes too, so it already applies to every definition type.
    /// Sessions 3 to 5 add more specific editors that override <see cref="DrawContent"/>;
    /// Unity picks the most specific <c>CustomEditor</c> available, so those take over
    /// automatically without this one being removed.
    /// </remarks>
    [CustomEditor(typeof(CraftingDefinition), editorForChildClasses: true)]
    [CanEditMultipleObjects]
    public class CraftingDefinitionEditor : Editor
    {
        private SerializedProperty _idProperty;
        private SerializedProperty _displayNameProperty;

        private readonly CraftingIssues _issues = new CraftingIssues();

        private string _lastValidatedId;
        private bool _revalidate = true;

        /// <summary>The asset being edited, or null in a multi-selection.</summary>
        protected CraftingDefinition Definition => target as CraftingDefinition;

        /// <summary>Issues found by the last validation pass.</summary>
        protected CraftingIssues Issues => _issues;

        protected virtual void OnEnable()
        {
            _idProperty = serializedObject.FindProperty("_id");
            _displayNameProperty = serializedObject.FindProperty("_displayName");
            _revalidate = true;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (targets.Length > 1)
            {
                DrawMultiSelection();
                serializedObject.ApplyModifiedProperties();
                return;
            }

            DrawIdentity();
            RefreshValidationIfNeeded();
            CraftingEditorGUI.DrawIssues(_issues);
            DrawIdentityActions();

            CraftingEditorGUI.Separator();
            DrawContent();

            if (serializedObject.ApplyModifiedProperties())
                _revalidate = true;
        }

        /// <summary>
        /// Everything below the identity header. The default draws the remaining serialized
        /// fields; concrete editors override it to lay their type out properly.
        /// </summary>
        protected virtual void DrawContent()
        {
            DrawPropertiesExcluding(serializedObject, "m_Script", "_id", "_displayName");
        }

        /// <summary>
        /// Type-specific checks. Called after the shared identity rules, with the same
        /// collector, so every issue ends up in one list.
        /// </summary>
        protected virtual void Validate(CraftingIssues issues)
        {
        }

        /// <summary>Forces a revalidation on the next repaint.</summary>
        protected void InvalidateValidation() => _revalidate = true;

        // --- Drawing ------------------------------------------------------------------

        private void DrawIdentity()
        {
            CraftingEditorGUI.Section("Identity");

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(
                _idProperty,
                new GUIContent("Id", "Permanent identifier. Changing it breaks saves and every reference by id."),
                true);

            if (EditorGUI.EndChangeCheck())
                _revalidate = true;

            EditorGUILayout.PropertyField(
                _displayNameProperty,
                new GUIContent("Display Name", "Shown in UI and in this toolkit. Safe to change at any time."));

            if (Definition != null)
            {
                EditorGUILayout.LabelField(
                    $"Hash {Definition.Id.Hash}   ·   asset '{Definition.name}'",
                    CraftingEditorGUI.Subtle);
            }
        }

        private void DrawIdentityActions()
        {
            CraftingDefinition definition = Definition;
            if (definition == null)
                return;

            bool idTaken = CraftingAssetUtility.FindDuplicates(definition).Count > 0;
            string suggestedName = CraftingIdentityValidator.SuggestAssetName(definition);
            bool nameDiffers = !string.IsNullOrEmpty(suggestedName) && suggestedName != definition.name;

            if (!idTaken && !nameDiffers)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (idTaken && GUILayout.Button("Make Id Unique", EditorStyles.miniButton))
                    MakeIdUnique(definition);

                if (nameDiffers && GUILayout.Button($"Rename Asset To '{suggestedName}'", EditorStyles.miniButton))
                {
                    if (CraftingIdentityValidator.RenameAssetToMatchId(definition))
                        _revalidate = true;
                }
            }
        }

        private void DrawMultiSelection()
        {
            EditorGUILayout.HelpBox(
                $"{targets.Length} assets selected. Ids are per-asset and are not shown here, "
                + "because editing them together would produce duplicates.",
                MessageType.Info);

            DrawPropertiesExcluding(serializedObject, "m_Script", "_id", "_displayName");
        }

        // --- Validation ---------------------------------------------------------------

        private void RefreshValidationIfNeeded()
        {
            CraftingDefinition definition = Definition;
            if (definition == null)
                return;

            // Validation walks the asset database, so it runs on change rather than on
            // every repaint. An inspector repaints many times per second.
            string currentId = _idProperty != null ? _idProperty.stringValue : definition.RawId;
            if (!_revalidate && currentId == _lastValidatedId)
                return;

            _issues.Clear();
            CraftingIdentityValidator.Validate(definition, _issues);
            Validate(_issues);

            _lastValidatedId = currentId;
            _revalidate = false;
        }

        private void MakeIdUnique(CraftingDefinition definition)
        {
            string unique = CraftingAssetUtility.MakeUniqueId(definition.GetType(), definition.RawId);

            Undo.RecordObject(definition, "Make Id Unique");
            _idProperty.stringValue = unique;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(definition);

            _revalidate = true;
        }
    }
}