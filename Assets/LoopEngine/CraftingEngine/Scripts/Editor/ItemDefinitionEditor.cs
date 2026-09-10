using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoopEngine.CraftingEngine.EditorTools
{
    /// <summary>
    /// Inspector for <see cref="ItemDefinition"/>: tag editing with reuse of existing tags,
    /// an icon preview, and the recipes and modifiers that reference this item.
    /// </summary>
    [CustomEditor(typeof(ItemDefinition))]
    [CanEditMultipleObjects]
    public sealed class ItemDefinitionEditor : CraftingDefinitionEditor
    {
        private SerializedProperty _description;
        private SerializedProperty _icon;
        private SerializedProperty _maxStack;
        private SerializedProperty _tags;

        private readonly List<RecipeDefinition> _producedBy = new List<RecipeDefinition>();
        private readonly List<RecipeDefinition> _consumedBy = new List<RecipeDefinition>();
        private readonly List<ModifierDefinition> _carriedModifiers = new List<ModifierDefinition>();

        private bool _usageDirty = true;
        private bool _showUsage = true;

        protected override void OnEnable()
        {
            base.OnEnable();

            _description = serializedObject.FindProperty("_description");
            _icon = serializedObject.FindProperty("_icon");
            _maxStack = serializedObject.FindProperty("_maxStack");
            _tags = serializedObject.FindProperty("_tags");

            _usageDirty = true;
        }

        protected override void DrawContent()
        {
            DrawPresentation();
            DrawStacking();
            DrawTags();
            DrawUsage();
        }

        protected override void Validate(CraftingIssues issues)
        {
            var item = (ItemDefinition)target;

            ValidateTags(item, issues);

            RefreshUsageIfNeeded();

            if (_producedBy.Count == 0 && _consumedBy.Count == 0 && _carriedModifiers.Count == 0)
            {
                issues.Info(
                    "No recipe produces or consumes this item, and no modifier is carried by it. "
                    + "That is fine for a raw resource placed in the world, but it is also what an "
                    + "item forgotten mid-design looks like.",
                    item);
            }
        }

        // --- Sections -----------------------------------------------------------------

        private void DrawPresentation()
        {
            CraftingEditorGUI.Section("Presentation");

            EditorGUILayout.PropertyField(_description);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(
                    _icon,
                    new GUIContent("Icon", "For your UI. The crafting system never reads this."));

                DrawIconPreview();
            }
        }

        private void DrawIconPreview()
        {
            var sprite = _icon.objectReferenceValue as Sprite;
            Rect box = GUILayoutUtility.GetRect(48f, 48f, GUILayout.Width(48f), GUILayout.Height(48f));

            EditorGUI.DrawRect(box, EditorGUIUtility.isProSkin
                ? new Color(0f, 0f, 0f, 0.2f)
                : new Color(0f, 0f, 0f, 0.08f));

            if (sprite == null)
                return;

            // Asset previews load asynchronously and return null on the first frames.
            Texture2D preview = AssetPreview.GetAssetPreview(sprite);
            if (preview != null)
                GUI.DrawTexture(box, preview, ScaleMode.ScaleToFit);
            else
                Repaint();
        }

        private void DrawStacking()
        {
            CraftingEditorGUI.Section("Stacking");

            EditorGUILayout.PropertyField(
                _maxStack,
                new GUIContent("Max Stack", "Declarative only. Nothing in the runtime reads it."));

            EditorGUILayout.LabelField(
                _maxStack.intValue == 0
                    ? "0 means no declared limit."
                    : "Declarative only: enforcement belongs to your IItemContainer, not to the crafting system.",
                CraftingEditorGUI.Subtle);
        }

        private void DrawTags()
        {
            CraftingEditorGUI.Section("Tags");

            EditorGUILayout.LabelField(
                "Free-form labels used for filtering: food, metal, perishable, tier2. "
                + "This is how an item becomes 'food' without needing its own type.",
                CraftingEditorGUI.Subtle);

            int removeIndex = -1;

            for (int i = 0; i < _tags.arraySize; i++)
            {
                SerializedProperty element = _tags.GetArrayElementAtIndex(i);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    element.stringValue = EditorGUILayout.TextField(element.stringValue);
                    if (EditorGUI.EndChangeCheck())
                        InvalidateValidation();

                    if (GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(22f)))
                        removeIndex = i;
                }
            }

            if (removeIndex >= 0)
            {
                _tags.DeleteArrayElementAtIndex(removeIndex);
                InvalidateValidation();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Add Tag", EditorStyles.miniButton, GUILayout.Width(80f)))
                    ShowTagMenu();
            }
        }

        /// <summary>
        /// Offers every tag already used in the project, so a typo does not quietly create a
        /// second tag nobody filters on.
        /// </summary>
        private void ShowTagMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("New tag..."), false, () => AddTag(string.Empty));

            List<string> existing = CollectProjectTags();
            if (existing.Count > 0)
            {
                menu.AddSeparator(string.Empty);

                for (int i = 0; i < existing.Count; i++)
                {
                    string tag = existing[i];
                    bool alreadyOnThisItem = HasTag(tag);
                    menu.AddItem(new GUIContent(tag), alreadyOnThisItem, () => AddTag(tag));
                }
            }

            menu.ShowAsContext();
        }

        private void DrawUsage()
        {
            CraftingEditorGUI.Section("Usage");

            using (new EditorGUILayout.HorizontalScope())
            {
                _showUsage = EditorGUILayout.Foldout(_showUsage, "References to this item", true);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.Width(60f)))
                {
                    _usageDirty = true;
                    InvalidateValidation();
                }
            }

            if (!_showUsage)
                return;

            RefreshUsageIfNeeded();

            DrawReferenceList("Produced by", _producedBy);
            DrawReferenceList("Consumed by", _consumedBy);
            DrawReferenceList("Carries modifier", _carriedModifiers);
        }

        private static void DrawReferenceList<T>(string label, List<T> assets) where T : CraftingDefinition
        {
            EditorGUILayout.LabelField($"{label} ({assets.Count})", EditorStyles.miniBoldLabel);

            if (assets.Count == 0)
            {
                EditorGUILayout.LabelField("   none", CraftingEditorGUI.Subtle);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                for (int i = 0; i < assets.Count; i++)
                    EditorGUILayout.ObjectField(assets[i], typeof(T), false);
            }
        }

        // --- Data ---------------------------------------------------------------------

        /// <summary>
        /// Scans every recipe and modifier in the project. Expensive, so it runs on demand and
        /// on selection, never per repaint.
        /// </summary>
        private void RefreshUsageIfNeeded()
        {
            if (!_usageDirty)
                return;

            _usageDirty = false;
            _producedBy.Clear();
            _consumedBy.Clear();
            _carriedModifiers.Clear();

            var item = target as ItemDefinition;
            if (item == null)
                return;

            CraftingId id = item.Id;
            if (!id.IsValid)
                return;

            List<RecipeDefinition> recipes = CraftingAssetUtility.FindAll<RecipeDefinition>();
            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeDefinition recipe = recipes[i];

                if (recipe.Produces(in id))
                    _producedBy.Add(recipe);

                if (recipe.Consumes(in id))
                    _consumedBy.Add(recipe);
            }

            List<ModifierDefinition> modifiers = CraftingAssetUtility.FindAll<ModifierDefinition>();
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i].CarrierItemId.Equals(id))
                    _carriedModifiers.Add(modifiers[i]);
            }
        }

        private List<string> CollectProjectTags()
        {
            var unique = new HashSet<string>(System.StringComparer.Ordinal);
            List<ItemDefinition> items = CraftingAssetUtility.FindAll<ItemDefinition>();

            for (int i = 0; i < items.Count; i++)
            {
                CraftingId[] tags = items[i].Tags;
                for (int t = 0; t < tags.Length; t++)
                {
                    if (tags[t].IsValid)
                        unique.Add(tags[t].Value);
                }
            }

            var sorted = new List<string>(unique);
            sorted.Sort(System.StringComparer.Ordinal);
            return sorted;
        }

        private bool HasTag(string tag)
        {
            for (int i = 0; i < _tags.arraySize; i++)
            {
                if (string.Equals(_tags.GetArrayElementAtIndex(i).stringValue, tag, System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private void AddTag(string tag)
        {
            if (!string.IsNullOrEmpty(tag) && HasTag(tag))
                return;

            serializedObject.Update();

            int index = _tags.arraySize;
            _tags.InsertArrayElementAtIndex(index);
            _tags.GetArrayElementAtIndex(index).stringValue = tag;

            serializedObject.ApplyModifiedProperties();
            InvalidateValidation();
            Repaint();
        }

        private void ValidateTags(ItemDefinition item, CraftingIssues issues)
        {
            var seen = new HashSet<string>(System.StringComparer.Ordinal);

            for (int i = 0; i < _tags.arraySize; i++)
            {
                string tag = _tags.GetArrayElementAtIndex(i).stringValue;

                if (string.IsNullOrWhiteSpace(tag))
                {
                    issues.Warning($"Tag {i + 1} is empty. Empty tags are skipped when the item is baked.", item);
                    continue;
                }

                if (!seen.Add(tag))
                    issues.Warning($"Tag '{tag}' is listed more than once. The duplicate has no effect.", item);

                if (tag != tag.Trim())
                    issues.Warning($"Tag '{tag}' has surrounding whitespace; it is trimmed when baked.", item);
            }
        }
    }
}