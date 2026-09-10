using LoopEngine.TagInput;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoopEngine.TagInput.EditorTools
{
    /// <summary>
    /// Inspector del KeyInputManager: validación en tiempo de edición y captura de teclas.
    /// Accede a los datos solo por SerializedProperty, así que no necesita acceso interno
    /// a la clase runtime ni la acopla al editor.
    /// </summary>
    [CustomEditor(typeof(KeyInputManager))]
    public class KeyInputManagerEditor : Editor
    {
        private SerializedProperty bindingsProp;
        private SerializedProperty caseSensitiveProp;
        private SerializedProperty persistProp;
        private SerializedProperty logWarningsProp;

        /// <summary>Índice del binding en modo captura, o -1 si ninguno.</summary>
        private int captureIndex = -1;

        private void OnEnable()
        {
            bindingsProp = serializedObject.FindProperty("bindings");
            caseSensitiveProp = serializedObject.FindProperty("caseSensitiveTags");
            persistProp = serializedObject.FindProperty("persistAcrossScenes");
            logWarningsProp = serializedObject.FindProperty("logWarnings");
        }

        private void OnDisable()
        {
            captureIndex = -1;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawEnvironmentStatus();
            EditorGUILayout.Space();

            DrawSettings();
            EditorGUILayout.Space();

            DrawBindings();
            EditorGUILayout.Space();

            DrawValidation();

            serializedObject.ApplyModifiedProperties();

            if (captureIndex >= 0)
            {
                Repaint();
            }
        }

        // ------------------------------------------------------------------
        // Estado del entorno
        // ------------------------------------------------------------------

        private void DrawEnvironmentStatus()
        {
            if (!TagInputDefines.LegacyInputEnabled)
            {
                EditorGUILayout.HelpBox(
                    "El Input Manager legacy está desactivado, así que no se puede leer KeyCode.\n" +
                    "Project Settings > Player > Other Settings > Configuration > Active Input Handling " +
                    "debe estar en \"Input Manager (Old)\" o \"Both\". El editor se reiniciará al cambiarlo.",
                    MessageType.Error);

                if (GUILayout.Button("Abrir Player Settings"))
                {
                    SettingsService.OpenProjectSettings("Project/Player");
                }

                EditorGUILayout.Space();
            }

            bool enabled = TagInputDefines.IsEnabled;

            if (enabled)
            {
                EditorGUILayout.HelpBox(
                    "INPUT_TEST está definido: el sistema está compilado y activo en el editor.",
                    MessageType.Info);

                if (GUILayout.Button("Quitar INPUT_TEST (excluir de compilación)"))
                {
                    TagInputDefines.SetEnabled(false);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "INPUT_TEST no está definido. El componente sigue en la escena y conserva sus datos, " +
                    "pero toda la lógica está excluida de la compilación y la API devuelve false.",
                    MessageType.Warning);

                if (GUILayout.Button("Añadir INPUT_TEST (activar sistema)"))
                {
                    TagInputDefines.SetEnabled(true);
                }
            }
        }

        // ------------------------------------------------------------------
        // Ajustes
        // ------------------------------------------------------------------

        private void DrawSettings()
        {
            EditorGUILayout.LabelField("Ajustes", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(caseSensitiveProp);
            EditorGUILayout.PropertyField(persistProp);
            EditorGUILayout.PropertyField(logWarningsProp);
        }

        // ------------------------------------------------------------------
        // Lista de bindings
        // ------------------------------------------------------------------

        private void DrawBindings()
        {
            EditorGUILayout.LabelField($"Bindings ({bindingsProp.arraySize})", EditorStyles.boldLabel);

            int removeIndex = -1;

            for (int i = 0; i < bindingsProp.arraySize; i++)
            {
                SerializedProperty element = bindingsProp.GetArrayElementAtIndex(i);
                SerializedProperty tagProp = element.FindPropertyRelative("tag");
                SerializedProperty keyProp = element.FindPropertyRelative("key");
                //SerializedProperty descProp = element.FindPropertyRelative("description");

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(tagProp, GUIContent.none, GUILayout.MinWidth(90));

                        if (captureIndex == i)
                        {
                            HandleCapture(keyProp);
                            GUILayout.Label("Pulsa una tecla…  (Esc cancela)",
                                EditorStyles.miniBoldLabel, GUILayout.Width(170));
                        }
                        else
                        {
                            EditorGUILayout.PropertyField(keyProp, GUIContent.none, GUILayout.Width(120));

                            if (GUILayout.Button("Capturar", EditorStyles.miniButton, GUILayout.Width(70)))
                            {
                                captureIndex = i;
                                GUIUtility.keyboardControl = 0;
                                EditorGUIUtility.editingTextField = false;
                            }
                        }

                        if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(22)))
                        {
                            removeIndex = i;
                        }
                    }

                    //EditorGUILayout.PropertyField(descProp, GUIContent.none);
                }
            }

            if (removeIndex >= 0)
            {
                bindingsProp.DeleteArrayElementAtIndex(removeIndex);
                captureIndex = -1;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Añadir binding"))
                {
                    AddBinding();
                }

                if (GUILayout.Button("Ordenar por tag"))
                {
                    SortByTag();
                }
            }
        }

        private void AddBinding()
        {
            int index = bindingsProp.arraySize;
            bindingsProp.InsertArrayElementAtIndex(index);

            SerializedProperty element = bindingsProp.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("tag").stringValue = string.Empty;
            element.FindPropertyRelative("key").intValue = (int)KeyCode.None;
            //element.FindPropertyRelative("description").stringValue = string.Empty;
        }

        private void SortByTag()
        {
            // Ordenación por inserción sobre el array serializado. La lista es corta
            // (decenas de entradas) y MoveArrayElement mantiene la integridad del undo.
            for (int i = 1; i < bindingsProp.arraySize; i++)
            {
                int j = i;
                while (j > 0 && string.Compare(TagAt(j - 1), TagAt(j),
                           System.StringComparison.OrdinalIgnoreCase) > 0)
                {
                    bindingsProp.MoveArrayElement(j, j - 1);
                    j--;
                }
            }

            captureIndex = -1;
        }

        private string TagAt(int index) =>
            bindingsProp.GetArrayElementAtIndex(index).FindPropertyRelative("tag").stringValue ?? string.Empty;

        // ------------------------------------------------------------------
        // Captura de tecla
        // ------------------------------------------------------------------

        private void HandleCapture(SerializedProperty keyProp)
        {
            Event e = Event.current;

            if (e.type != EventType.KeyDown || e.keyCode == KeyCode.None)
            {
                return;
            }

            if (e.keyCode != KeyCode.Escape)
            {
                keyProp.intValue = (int)e.keyCode;
            }

            captureIndex = -1;
            e.Use();
        }

        // ------------------------------------------------------------------
        // Validación
        // ------------------------------------------------------------------

        private void DrawValidation()
        {
            EditorGUILayout.LabelField("Validación", EditorStyles.boldLabel);

            var seenTags = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            var seenKeys = new Dictionary<KeyCode, string>();

            var emptyTags = new List<int>();
            var noKeys = new List<string>();
            var duplicateTags = new List<string>();
            var sharedKeys = new List<string>();

            for (int i = 0; i < bindingsProp.arraySize; i++)
            {
                SerializedProperty element = bindingsProp.GetArrayElementAtIndex(i);
                string tag = (element.FindPropertyRelative("tag").stringValue ?? string.Empty).Trim();
                var key = (KeyCode)element.FindPropertyRelative("key").intValue;

                if (tag.Length == 0)
                {
                    emptyTags.Add(i);
                    continue;
                }

                if (key == KeyCode.None)
                {
                    noKeys.Add(tag);
                }

                if (seenTags.TryGetValue(tag, out int first))
                {
                    duplicateTags.Add($"\"{tag}\" (índices {first} y {i})");
                }
                else
                {
                    seenTags.Add(tag, i);
                }

                if (key != KeyCode.None)
                {
                    if (seenKeys.TryGetValue(key, out string owner))
                    {
                        sharedKeys.Add($"{key}: \"{owner}\" y \"{tag}\"");
                    }
                    else
                    {
                        seenKeys.Add(key, tag);
                    }
                }
            }

            bool clean = true;

            if (emptyTags.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Bindings con tag vacío en los índices: {string.Join(", ", emptyTags)}. Se ignoran al arrancar.",
                    MessageType.Error);
                clean = false;
            }

            if (noKeys.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Tags sin tecla asignada (KeyCode.None): {string.Join(", ", noKeys)}. Se ignoran al arrancar.",
                    MessageType.Error);
                clean = false;
            }

            if (duplicateTags.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "Tags duplicados. Solo la primera aparición entra al mapa:\n" +
                    string.Join("\n", duplicateTags),
                    MessageType.Error);
                clean = false;
            }

            if (sharedKeys.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "Una misma tecla responde a varios tags. Es válido, pero suele ser un error de dedo:\n" +
                    string.Join("\n", sharedKeys),
                    MessageType.Warning);
                clean = false;
            }

            if (clean)
            {
                EditorGUILayout.HelpBox("Sin problemas detectados.", MessageType.Info);
            }
        }
    }
}