using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LoopEngine.WfcEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine.EditorTools
{
    /// <summary>
    /// Drawer de WfcSocketRef: desplegable de sockets de la libreria y, solo cuando
    /// aplica, el modificador (flipped o indice de rotacion).
    /// Un id que ya no existe se muestra como &lt;missing #N&gt; y no se pierde al redibujar.
    /// </summary>
    [CustomPropertyDrawer(typeof(WfcSocketRef))]
    public class WfcSocketRefDrawer : PropertyDrawer
    {
        private const float Line = 18f;
        private const float Gap = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => Line * 2f + Gap * 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var libraryProp = property.FindPropertyRelative("library");
            var socketIdProp = property.FindPropertyRelative("socketId");
            var flippedProp = property.FindPropertyRelative("flipped");
            var rotationProp = property.FindPropertyRelative("rotationIndex");

            EditorGUI.BeginProperty(position, label, property);

            var topRow = new Rect(position.x, position.y, position.width, Line);
            EditorGUI.PropertyField(topRow, libraryProp, label);

            var bottomRow = new Rect(position.x, position.y + Line + Gap, position.width, Line);
            var indented = EditorGUI.IndentedRect(bottomRow);

            var library = libraryProp.objectReferenceValue as WfcSocketLibrary;
            if (library == null)
            {
                EditorGUI.LabelField(indented, " ", "Asigna una libreria de sockets.");
                EditorGUI.EndProperty();
                return;
            }

            float modifierWidth = 80f;
            var popupRect = new Rect(indented.x, indented.y, indented.width - modifierWidth - Gap, Line);
            var modifierRect = new Rect(popupRect.xMax + Gap, indented.y, modifierWidth, Line);

            DrawSocketPopup(popupRect, library, socketIdProp);
            DrawModifier(modifierRect, library, socketIdProp.intValue, flippedProp, rotationProp);

            EditorGUI.EndProperty();
        }

        private static void DrawSocketPopup(Rect rect, WfcSocketLibrary library, SerializedProperty socketIdProp)
        {
            int currentId = socketIdProp.intValue;

            var labels = new List<GUIContent> { new GUIContent("(vacio)") };
            var ids = new List<int> { WfcSocketDescriptor.InvalidId };

            foreach (var entry in library.Entries)
            {
                if (entry == null) continue;
                labels.Add(new GUIContent($"{entry.DisplayName}  [{WfcSocketKinds.ShortLabel(entry.Kind)}]"));
                ids.Add(entry.Id);
            }

            int selected = ids.IndexOf(currentId);

            // Referencia obsoleta: se anade una opcion visible en vez de reasignar en silencio.
            if (selected < 0)
            {
                labels.Add(new GUIContent($"<missing #{currentId}>"));
                ids.Add(currentId);
                selected = ids.Count - 1;
            }

            int next = EditorGUI.Popup(rect, GUIContent.none, selected, labels.ToArray());
            if (next != selected) socketIdProp.intValue = ids[next];
        }

        private static void DrawModifier(
            Rect rect,
            WfcSocketLibrary library,
            int socketId,
            SerializedProperty flippedProp,
            SerializedProperty rotationProp)
        {
            if (!library.TryGetKind(socketId, out var kind))
            {
                EditorGUI.LabelField(rect, string.Empty);
                return;
            }

            if (WfcSocketKinds.UsesFlipped(kind))
            {
                flippedProp.boolValue = EditorGUI.ToggleLeft(rect, "flipped", flippedProp.boolValue);
                rotationProp.intValue = 0;
                return;
            }

            if (WfcSocketKinds.UsesRotationIndex(kind))
            {
                int value = EditorGUI.IntPopup(
                    rect,
                    rotationProp.intValue,
                    new[] { "rot 0", "rot 1", "rot 2", "rot 3" },
                    new[] { 0, 1, 2, 3 });

                rotationProp.intValue = value;
                flippedProp.boolValue = false;
                return;
            }

            flippedProp.boolValue = false;
            rotationProp.intValue = 0;
            EditorGUI.LabelField(rect, "sin modificador", EditorStyles.miniLabel);
        }
    }
}