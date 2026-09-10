using UnityEditor;
using UnityEngine;

namespace LoopEngine.ColorEngine.EditorTools
{
    /// <summary>
    /// Dibuja PaletteColorReference como: [muestra de color][desplegable de roles].
    /// Desplegado (flecha del foldout) muestra la paleta y el color de fallback.
    ///
    /// Al elegir un rol se asigna tambien la paleta, tomando la paleta activa de
    /// ColorPaletteWindow si el campo estaba vacio.
    /// </summary>
    [CustomPropertyDrawer(typeof(PaletteColorReference))]
    public sealed class PaletteColorReferenceDrawer : PropertyDrawer
    {
        private const float Pad = 2f;
        private const float SwatchWidth = 16f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            return property.isExpanded ? line * 3f + Pad * 2f : line;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty paletteProperty = property.FindPropertyRelative("palette");
            SerializedProperty roleProperty = property.FindPropertyRelative("roleId");
            SerializedProperty fallbackProperty = property.FindPropertyRelative("fallbackColor");

            float line = EditorGUIUtility.singleLineHeight;
            var palette = paletteProperty.objectReferenceValue as ColorPalette;
            string roleId = roleProperty.stringValue;

            Color roleColor = Color.white;

            bool resolved = palette != null && palette.TryGetColor(roleId, out roleColor);
            Color preview = resolved ? roleColor : fallbackProperty.colorValue;

            // --- Linea principal ---
            var foldRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, line);
            property.isExpanded = EditorGUI.Foldout(foldRect, property.isExpanded, label, true);

            float fieldX = position.x + EditorGUIUtility.labelWidth + Pad;
            float fieldWidth = position.width - EditorGUIUtility.labelWidth - Pad;

            var swatchRect = new Rect(fieldX, position.y + 1f, SwatchWidth, line - 2f);
            EditorGUI.DrawRect(swatchRect, preview);
            EditorGUI.DrawRect(new Rect(swatchRect.x, swatchRect.y, swatchRect.width, 1f), Color.black * 0.35f);

            var buttonRect = new Rect(
                fieldX + SwatchWidth + Pad, position.y,
                Mathf.Max(40f, fieldWidth - SwatchWidth - Pad), line);

            string buttonLabel;
            if (palette == null) buttonLabel = "(sin paleta)";
            else if (string.IsNullOrEmpty(roleId)) buttonLabel = "(ninguno)";
            else if (!resolved) buttonLabel = roleId + "  [no existe]";
            else buttonLabel = roleId;

            if (EditorGUI.DropdownButton(buttonRect, new GUIContent(buttonLabel), FocusType.Keyboard))
                ShowMenu(buttonRect, property, palette, roleId);

            // --- Detalle desplegado ---
            if (property.isExpanded)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    var paletteRect = new Rect(position.x, position.y + line + Pad, position.width, line);
                    EditorGUI.PropertyField(paletteRect, paletteProperty);

                    var fallbackRect = new Rect(position.x, position.y + (line + Pad) * 2f, position.width, line);
                    EditorGUI.PropertyField(fallbackRect, fallbackProperty);
                }
            }

            EditorGUI.EndProperty();
        }

        private static void ShowMenu(Rect rect, SerializedProperty property, ColorPalette palette, string currentRoleId)
        {
            SerializedObject serializedObject = property.serializedObject;
            string palettePath = property.FindPropertyRelative("palette").propertyPath;
            string rolePath = property.FindPropertyRelative("roleId").propertyPath;

            ColorPalette target = palette != null ? palette : ColorPaletteWindow.ActivePalette;
            var menu = new GenericMenu();

            if (target == null)
            {
                menu.AddDisabledItem(new GUIContent("No hay paleta activa"));
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Abrir Color Palette..."), false, ColorPaletteWindow.Open);
                menu.DropDown(rect);
                return;
            }

            menu.AddItem(new GUIContent("(ninguno)"), string.IsNullOrEmpty(currentRoleId),
                () => Assign(serializedObject, palettePath, rolePath, target, string.Empty));
            menu.AddSeparator(string.Empty);

            for (int i = 0; i < target.Roles.Count; i++)
            {
                ColorRole role = target.Roles[i];
                if (!role.IsValid) continue;

                string id = role.Id;
                menu.AddItem(new GUIContent(id), id == currentRoleId,
                    () => Assign(serializedObject, palettePath, rolePath, target, id));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Editar paleta..."), false, ColorPaletteWindow.Open);
            menu.DropDown(rect);
        }

        private static void Assign(SerializedObject serializedObject, string palettePath, string rolePath,
            ColorPalette palette, string roleId)
        {
            // El menu se ejecuta despues del OnGUI: revalidar antes de escribir.
            if (serializedObject == null || serializedObject.targetObject == null) return;

            serializedObject.Update();
            serializedObject.FindProperty(palettePath).objectReferenceValue = palette;
            serializedObject.FindProperty(rolePath).stringValue = roleId;
            serializedObject.ApplyModifiedProperties();
        }
    }
}