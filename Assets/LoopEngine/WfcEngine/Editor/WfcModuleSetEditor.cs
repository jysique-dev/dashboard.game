using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using LoopEngine.WfcEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine.EditorTools
{
    /// <summary>
    /// Inspector del set: lista con miniaturas, resumen de variantes horneables,
    /// validacion agregada y creacion de modulos a partir de prefabs seleccionados.
    /// </summary>
    [CustomEditor(typeof(WfcModuleSet))]
    public class WfcModuleSetEditor : UnityEditor.Editor
    {
        private SerializedProperty libraryProp;
        private SerializedProperty modulesProp;
        private SerializedProperty cellSizeProp;
        private SerializedProperty notesProp;

        private readonly List<WfcValidationIssue> issues = new List<WfcValidationIssue>();
        private Vector2 issueScroll;

        private void OnEnable()
        {
            libraryProp = serializedObject.FindProperty("library");
            modulesProp = serializedObject.FindProperty("modules");
            cellSizeProp = serializedObject.FindProperty("cellSize");
            notesProp = serializedObject.FindProperty("notes");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var set = (WfcModuleSet)target;

            EditorGUILayout.PropertyField(libraryProp);
            EditorGUILayout.PropertyField(cellSizeProp);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(
                $"{set.Count} modulos · {set.CountBakedVariants()} variantes tras hornear rotaciones",
                EditorStyles.miniBoldLabel);

            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(modulesProp, new GUIContent("Modulos"), true);

            EditorGUILayout.Space(4);
            DrawCreationTools(set);

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(notesProp);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6);
            DrawValidation(set);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Abrir galeria de modulos"))
            {
                WfcModuleGalleryWindow.OpenWith(set);
            }
        }

        private void DrawCreationTools(WfcModuleSet set)
        {
            var prefabs = SelectedPrefabs();

            using (new EditorGUI.DisabledScope(prefabs.Count == 0 || set.Library == null))
            {
                if (GUILayout.Button($"Crear modulos desde {prefabs.Count} prefab(s) seleccionado(s)"))
                {
                    CreateModulesFromPrefabs(set, prefabs);
                }
            }

            if (set.Library == null)
            {
                EditorGUILayout.LabelField(
                    "Asigna una libreria para poder crear modulos.", EditorStyles.miniLabel);
            }
        }

        private static List<GameObject> SelectedPrefabs()
        {
            var result = new List<GameObject>();

            foreach (var obj in Selection.objects)
            {
                if (obj is GameObject go && PrefabUtility.IsPartOfPrefabAsset(go)) result.Add(go);
            }

            return result;
        }

        private void CreateModulesFromPrefabs(WfcModuleSet set, List<GameObject> prefabs)
        {
            string setPath = AssetDatabase.GetAssetPath(set);
            string folder = Path.GetDirectoryName(setPath);

            Undo.RecordObject(set, "Create WFC modules");

            foreach (var prefab in prefabs)
            {
                var module = CreateInstance<WfcModuleDefinition>();
                module.name = "Module_" + prefab.name;

                string path = AssetDatabase.GenerateUniqueAssetPath(
                    Path.Combine(folder, module.name + ".asset"));

                // El asset se crea antes de escribir campos: SerializedObject necesita
                // un objeto ya persistente para que los cambios se guarden.
                AssetDatabase.CreateAsset(module, path);

                var so = new SerializedObject(module);
                so.FindProperty("prefab").objectReferenceValue = prefab;
                so.FindProperty("library").objectReferenceValue = set.Library;
                so.FindProperty("faces").arraySize = WfcDirections.Count;
                so.ApplyModifiedPropertiesWithoutUndo();

                set.Add(module);
            }

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            serializedObject.Update();
        }

        private void DrawValidation(WfcModuleSet set)
        {
            issues.Clear();
            WfcModuleValidation.Validate(set, issues);

            int errors = WfcModuleValidation.CountBySeverity(issues, WfcIssueSeverity.Error);
            int warnings = WfcModuleValidation.CountBySeverity(issues, WfcIssueSeverity.Warning);

            if (errors == 0 && warnings == 0)
            {
                EditorGUILayout.HelpBox("Set coherente.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(
                $"{errors} error(es), {warnings} aviso(s)", EditorStyles.miniBoldLabel);

            issueScroll = EditorGUILayout.BeginScrollView(issueScroll, GUILayout.MaxHeight(180f));

            foreach (var issue in issues)
            {
                if (issue.Severity == WfcIssueSeverity.Info) continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox(
                        issue.Message, WfcModuleDefinitionEditor.ToMessageType(issue.Severity));

                    if (issue.Context != null &&
                        GUILayout.Button("Ir", EditorStyles.miniButton, GUILayout.Width(30f)))
                    {
                        Selection.activeObject = issue.Context;
                        EditorGUIUtility.PingObject(issue.Context);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }
}