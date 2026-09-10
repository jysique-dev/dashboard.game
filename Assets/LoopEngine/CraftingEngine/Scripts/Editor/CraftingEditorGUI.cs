using LoopEngine.CraftingEngine.EditorTools;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoopEngine.CraftingEngine.EditorTools
{
    /// <summary>
    /// Styles and small drawing helpers shared by every inspector in the toolkit.
    /// </summary>
    /// <remarks>
    /// Styles are built lazily. Creating a GUIStyle in a static field initialiser runs before
    /// the editor skin exists and produces a null-skin exception on domain reload.
    /// </remarks>
    public static class CraftingEditorGUI
    {
        private static GUIStyle _sectionHeader;
        private static GUIStyle _idField;
        private static GUIStyle _subtle;

        /// <summary>Bold label used to open a block of related fields.</summary>
        public static GUIStyle SectionHeader
        {
            get
            {
                if (_sectionHeader == null)
                {
                    _sectionHeader = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = EditorStyles.boldLabel.fontSize + 1
                    };
                }

                return _sectionHeader;
            }
        }

        /// <summary>Monospaced-ish field for ids, so typos in long ids are easier to spot.</summary>
        public static GUIStyle IdField
        {
            get
            {
                if (_idField == null)
                {
                    _idField = new GUIStyle(EditorStyles.textField)
                    {
                        fontStyle = FontStyle.Bold
                    };
                }

                return _idField;
            }
        }

        /// <summary>Dimmed label for hints and counts.</summary>
        public static GUIStyle Subtle
        {
            get
            {
                if (_subtle == null)
                {
                    _subtle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        wordWrap = true
                    };
                }

                return _subtle;
            }
        }

        /// <summary>Section title with a little space above it.</summary>
        public static void Section(string title)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(title, SectionHeader);
        }

        /// <summary>Horizontal rule, for separating blocks without a foldout.</summary>
        public static void Separator()
        {
            EditorGUILayout.Space(4f);
            Rect rect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.08f)
                : new Color(0f, 0f, 0f, 0.12f));
            EditorGUILayout.Space(4f);
        }

        /// <summary>Draws every issue as a help box, clickable to ping its asset.</summary>
        public static void DrawIssues(CraftingIssues issues)
        {
            if (issues == null || issues.IsClean)
                return;

            IReadOnlyList<CraftingIssue> all = issues.All;
            for (int i = 0; i < all.Count; i++)
                DrawIssue(all[i]);
        }

        /// <summary>Draws one issue. Clicking it pings the asset it refers to.</summary>
        public static void DrawIssue(in CraftingIssue issue)
        {
            EditorGUILayout.HelpBox(issue.Message, ToMessageType(issue.Severity));

            if (issue.Context == null)
                return;

            Rect rect = GUILayoutUtility.GetLastRect();
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                EditorGUIUtility.PingObject(issue.Context);
                Event.current.Use();
            }
        }

        public static MessageType ToMessageType(CraftingIssueSeverity severity)
        {
            switch (severity)
            {
                case CraftingIssueSeverity.Error: return MessageType.Error;
                case CraftingIssueSeverity.Warning: return MessageType.Warning;
                default: return MessageType.Info;
            }
        }

        /// <summary>
        /// A right-aligned row of small buttons. Returns the index clicked, or -1.
        /// </summary>
        public static int ButtonRow(params string[] labels)
        {
            int clicked = -1;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                for (int i = 0; i < labels.Length; i++)
                {
                    if (GUILayout.Button(labels[i], EditorStyles.miniButton))
                        clicked = i;
                }
            }

            return clicked;
        }

        /// <summary>
        /// Draws an array of object references as compact rows with add and remove buttons.
        /// </summary>
        /// <returns>True when the array was modified this frame.</returns>
        public static bool ObjectArray(SerializedProperty list, string label, string tooltip)
        {
            if (list == null || !list.isArray)
                return false;

            bool changed = false;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent($"{label} ({list.arraySize})", tooltip),
                    EditorStyles.miniBoldLabel);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(22f)))
                {
                    list.InsertArrayElementAtIndex(list.arraySize);
                    // A newly inserted element copies the previous one; an empty slot is the
                    // only sane default here.
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = null;
                    changed = true;
                }
            }

            int removeIndex = -1;

            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(element, GUIContent.none);
                    if (EditorGUI.EndChangeCheck())
                        changed = true;

                    if (GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(22f)))
                        removeIndex = i;
                }
            }

            if (list.arraySize == 0)
                EditorGUILayout.LabelField("   none", Subtle);

            if (removeIndex >= 0)
            {
                // Clearing first, because deleting a slot holding a reference only nulls it.
                list.GetArrayElementAtIndex(removeIndex).objectReferenceValue = null;
                list.DeleteArrayElementAtIndex(removeIndex);
                changed = true;
            }

            return changed;
        }

        /// <summary>Read-only list of assets, each clickable to select it.</summary>
        public static void ReferenceList<T>(string label, IReadOnlyList<T> assets) where T : Object
        {
            EditorGUILayout.LabelField($"{label} ({assets.Count})", EditorStyles.miniBoldLabel);

            if (assets.Count == 0)
            {
                EditorGUILayout.LabelField("   none", Subtle);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                for (int i = 0; i < assets.Count; i++)
                    EditorGUILayout.ObjectField(assets[i], typeof(T), false);
            }
        }
    }
}