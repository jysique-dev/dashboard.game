using UnityEditor;
using UnityEngine;
using LoopEngine.WfcEngine;

namespace LoopEngine.WfcEngine.EditorTools
{
    /// <summary>
    /// Inspector del set de exclusiones: cada regla muestra solo los campos que su tipo
    /// usa, y una linea en castellano con lo que significa.
    /// </summary>
    [CustomEditor(typeof(WfcExclusionSet))]
    public class WfcExclusionSetEditor : UnityEditor.Editor
    {
        private SerializedProperty moduleSetProp;
        private SerializedProperty rulesProp;

        private void OnEnable()
        {
            moduleSetProp = serializedObject.FindProperty("moduleSet");
            rulesProp = serializedObject.FindProperty("rules");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(moduleSetProp);
            EditorGUILayout.Space(4);

            for (int i = 0; i < rulesProp.arraySize; i++)
            {
                if (!DrawRule(rulesProp.GetArrayElementAtIndex(i), i)) break;
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Anadir regla"))
            {
                rulesProp.InsertArrayElementAtIndex(rulesProp.arraySize);
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6);
            if (GUILayout.Button("Ver efecto en la matriz"))
            {
                WfcMatrixWindow.Open();
            }
        }

        private bool DrawRule(SerializedProperty rule, int index)
        {
            var kindProp = rule.FindPropertyRelative("kind");
            var kind = (WfcExclusionKind)kindProp.enumValueIndex;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(kindProp, GUIContent.none);

                    if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(22f)))
                    {
                        rulesProp.DeleteArrayElementAtIndex(index);
                        return false;
                    }
                }

                switch (kind)
                {
                    case WfcExclusionKind.ModulePair:
                        EditorGUILayout.PropertyField(rule.FindPropertyRelative("moduleA"));
                        EditorGUILayout.PropertyField(rule.FindPropertyRelative("moduleB"));
                        break;

                    case WfcExclusionKind.ModuleDirectional:
                        EditorGUILayout.PropertyField(rule.FindPropertyRelative("moduleA"));
                        EditorGUILayout.PropertyField(rule.FindPropertyRelative("moduleB"));
                        EditorGUILayout.PropertyField(rule.FindPropertyRelative("direction"));
                        EditorGUILayout.LabelField(
                            "Direccion de mundo: la variante girada no rota la regla.",
                            EditorStyles.miniLabel);
                        break;

                    case WfcExclusionKind.SocketPair:
                        EditorGUILayout.PropertyField(rule.FindPropertyRelative("socketA"));
                        EditorGUILayout.PropertyField(rule.FindPropertyRelative("socketB"));
                        break;
                }

                EditorGUILayout.PropertyField(rule.FindPropertyRelative("reason"));
            }

            return true;
        }
    }
}