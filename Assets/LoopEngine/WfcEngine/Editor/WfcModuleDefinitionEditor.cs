using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LoopEngine.WfcEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine.EditorTools
{
    /// <summary>
    /// Inspector de modulo: preview del prefab a la izquierda, tabla de las 6 caras a la
    /// derecha, y la validacion debajo. Todo en una pantalla, sin desplegar arrays.
    /// </summary>
    [CustomEditor(typeof(WfcModuleDefinition))]
    [CanEditMultipleObjects]
    public class WfcModuleDefinitionEditor : UnityEditor.Editor
    {
        private SerializedProperty prefabProp;
        private SerializedProperty libraryProp;
        private SerializedProperty facesProp;
        private SerializedProperty weightProp;
        private SerializedProperty rotationsProp;
        private SerializedProperty notesProp;

        private readonly List<WfcValidationIssue> issues = new List<WfcValidationIssue>();

        private void OnEnable()
        {
            prefabProp = serializedObject.FindProperty("prefab");
            libraryProp = serializedObject.FindProperty("library");
            facesProp = serializedObject.FindProperty("faces");
            weightProp = serializedObject.FindProperty("weight");
            rotationsProp = serializedObject.FindProperty("allowedRotations");
            notesProp = serializedObject.FindProperty("notes");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var module = (WfcModuleDefinition)target;

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPreview(module);

                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.PropertyField(prefabProp);
                    EditorGUILayout.PropertyField(libraryProp);
                    EditorGUILayout.PropertyField(weightProp);
                    EditorGUILayout.PropertyField(rotationsProp);
                }
            }

            EditorGUILayout.Space(6);

            var library = libraryProp.objectReferenceValue as WfcSocketLibrary;
            if (library == null)
            {
                EditorGUILayout.HelpBox("Asigna una libreria para editar las caras.", MessageType.Warning);
            }
            else if (facesProp.arraySize == WfcDirections.Count)
            {
                EditorGUILayout.LabelField("Caras", EditorStyles.boldLabel);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    for (int i = 0; i < WfcDirections.Count; i++)
                    {
                        WfcSocketFaceGUI.DrawFaceRow(
                            facesProp.GetArrayElementAtIndex(i), (WfcDirection)i, library);
                    }
                }

                DrawCopyRow();
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(notesProp);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(4);
            DrawValidation(module);
        }

        private void DrawPreview(WfcModuleDefinition module)
        {
            var rect = GUILayoutUtility.GetRect(96f, 96f, GUILayout.Width(96f), GUILayout.Height(96f));

            if (module.Prefab == null)
            {
                EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.25f, 0.5f));
                EditorGUI.LabelField(rect, "AIRE", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            var preview = AssetPreview.GetAssetPreview(module.Prefab);
            if (preview != null)
            {
                GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 0.4f));
                EditorGUI.LabelField(rect, "...", EditorStyles.centeredGreyMiniLabel);
                if (AssetPreview.IsLoadingAssetPreview(module.Prefab.GetInstanceID())) Repaint();
            }
        }

        /// <summary>Atajos frecuentes al autorar sets: paredes con los 4 lados iguales, etc.</summary>
        private void DrawCopyRow()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copiar +X a las 4 horizontales", EditorStyles.miniButton))
                {
                    CopyHorizontal(WfcDirection.Right);
                }

                if (GUILayout.Button("Copiar +Z a las 4 horizontales", EditorStyles.miniButton))
                {
                    CopyHorizontal(WfcDirection.Forward);
                }
            }
        }

        private void CopyHorizontal(WfcDirection source)
        {
            var sourceProp = facesProp.GetArrayElementAtIndex((int)source);
            int id = sourceProp.FindPropertyRelative("socketId").intValue;
            bool flipped = sourceProp.FindPropertyRelative("flipped").boolValue;

            foreach (var direction in WfcDirections.HorizontalRing)
            {
                var target = facesProp.GetArrayElementAtIndex((int)direction);
                target.FindPropertyRelative("socketId").intValue = id;
                target.FindPropertyRelative("flipped").boolValue = flipped;
                target.FindPropertyRelative("rotationIndex").intValue = 0;
            }
        }

        private void DrawValidation(WfcModuleDefinition module)
        {
            issues.Clear();
            WfcModuleValidation.Validate(module, issues);

            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("Modulo coherente.", MessageType.Info);
                return;
            }

            foreach (var issue in issues)
            {
                EditorGUILayout.HelpBox(issue.Message, ToMessageType(issue.Severity));
            }
        }

        internal static MessageType ToMessageType(WfcIssueSeverity severity)
        {
            switch (severity)
            {
                case WfcIssueSeverity.Error: return MessageType.Error;
                case WfcIssueSeverity.Warning: return MessageType.Warning;
                default: return MessageType.Info;
            }
        }
    }
}