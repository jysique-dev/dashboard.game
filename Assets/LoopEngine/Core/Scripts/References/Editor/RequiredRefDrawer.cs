using UnityEditor;
using UnityEngine;
using LoopEngine.Core;

namespace LoopEngine.Core.EditorTools
{
    /// <summary>
    /// Pinta en rojo un campo [RequiredRef] vacio y anade una linea explicativa.
    /// Solo cambia el color: no bloquea la edicion. Quien bloquea el play es
    /// LoopSceneValidator.
    /// </summary>
    [CustomPropertyDrawer(typeof(RequiredRefAttribute))]
    public sealed class RequiredRefDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUI.GetPropertyHeight(property, label, true);
            return IsMissing(property)
                ? line + EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight
                : line;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            bool missing = IsMissing(property);

            Rect fieldRect = new Rect(
                position.x, position.y, position.width,
                EditorGUI.GetPropertyHeight(property, label, true));

            Color previousColor = GUI.color;
            if (missing) GUI.color = new Color(1f, 0.55f, 0.55f);

            EditorGUI.PropertyField(fieldRect, property, label, true);

            GUI.color = previousColor;

            if (!missing) return;

            Rect helpRect = new Rect(
                position.x,
                fieldRect.yMax + EditorGUIUtility.standardVerticalSpacing,
                position.width,
                EditorGUIUtility.singleLineHeight);

            EditorGUI.LabelField(helpRect, " ", "Campo obligatorio sin asignar.", EditorStyles.miniLabel);
        }

        private static bool IsMissing(SerializedProperty property)
        {
            return property.propertyType == SerializedPropertyType.ObjectReference
                   && property.objectReferenceValue == null;
        }
    }
}