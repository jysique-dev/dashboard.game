using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoopEngine.Core.EditorTools
{
    [CustomPropertyDrawer(typeof(ScriptableSetting), true)]
    public class ScriptableSettingsDrawer : PropertyDrawer
    {
        const float kBar = 3f, kGap = 6f, kToggle = 18f;

        static IEnumerable<SerializedProperty> Hijos(SerializedProperty prop)
        {
            var it = prop.Copy();
            var end = prop.GetEndProperty();
            bool entrar = true;
            while (it.NextVisible(entrar) && !SerializedProperty.EqualContents(it, end))
            {
                entrar = false;
                if (it.name != "enabled") yield return it.Copy();
            }
        }

        static Color Acento(SerializedProperty prop)
        {
            try { return prop.boxedValue is ScriptableSetting s ? s.EditorAccent : Color.gray; }
            catch { return Color.gray; }
        }

        public override float GetPropertyHeight(SerializedProperty prop, GUIContent label)
        {
            float h = EditorGUIUtility.singleLineHeight;
            if (!prop.isExpanded) return h;
            foreach (var hijo in Hijos(prop))
                h += EditorGUI.GetPropertyHeight(hijo, true) + EditorGUIUtility.standardVerticalSpacing;
            return h + EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty prop, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, prop);

            var enabled = prop.FindPropertyRelative("enabled");
            bool on = enabled == null || enabled.boolValue;
            var color = Acento(prop);

            EditorGUI.DrawRect(new Rect(position.x, position.y, kBar, position.height),
                               on ? color : new Color(color.r, color.g, color.b, 0.25f));

            var cuerpo = new Rect(position.x + kBar + kGap, position.y,
                                  position.width - kBar - kGap, position.height);
            var cab = new Rect(cuerpo.x, cuerpo.y, cuerpo.width, EditorGUIUtility.singleLineHeight);

            // ── clave: fuera indentación mientras calculamos rects a mano ──
            int indentPrevio = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var rToggle = new Rect(cab.xMax - kToggle, cab.y, kToggle, cab.height);
            var rFoldout = new Rect(cab.x + indentPrevio * 15f, cab.y,
                                    cab.width - kToggle - 4f - indentPrevio * 15f, cab.height);

            prop.isExpanded = EditorGUI.Foldout(rFoldout, prop.isExpanded, label, true);

            if (enabled != null)
                EditorGUI.PropertyField(rToggle, enabled, GUIContent.none);
            else
                EditorGUI.LabelField(rToggle, new GUIContent("!", "No se encontró el campo 'enabled'"));

            EditorGUI.indentLevel = indentPrevio;   // restaurar SIEMPRE

            if (prop.isExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                using (new EditorGUI.DisabledScope(!on))
                {
                    float y = cab.yMax + EditorGUIUtility.standardVerticalSpacing;
                    foreach (var hijo in Hijos(prop))
                    {
                        float h = EditorGUI.GetPropertyHeight(hijo, true);
                        EditorGUI.PropertyField(new Rect(cuerpo.x, y, cuerpo.width, h), hijo, true);
                        y += h + EditorGUIUtility.standardVerticalSpacing;
                    }
                }
            }

            EditorGUI.EndProperty();
        }
    }
}