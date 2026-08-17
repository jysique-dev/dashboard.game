using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LoopEngine.WfcEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine.EditorTools
{
    /// <summary>
    /// Dibujo compacto de una cara de modulo: etiqueta, color, socket y modificador.
    /// A diferencia del drawer generico de WfcSocketRef, aqui la libreria la impone el
    /// modulo y el desplegable solo ofrece sockets del eje correcto: no se puede asignar
    /// un socket horizontal al techo desde la interfaz.
    /// </summary>
    internal static class WfcSocketFaceGUI
    {
        public static void DrawFaceRow(
            SerializedProperty faceProp,
            WfcDirection direction,
            WfcSocketLibrary library)
        {
            var socketIdProp = faceProp.FindPropertyRelative("socketId");
            var flippedProp = faceProp.FindPropertyRelative("flipped");
            var rotationProp = faceProp.FindPropertyRelative("rotationIndex");

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    $"{WfcDirections.ShortName(direction)}  {FaceName(direction)}",
                    EditorStyles.miniBoldLabel,
                    GUILayout.Width(86f));

                var swatch = GUILayoutUtility.GetRect(12f, 12f, GUILayout.Width(12f));
                swatch.y += 3f;
                EditorGUI.DrawRect(swatch, SwatchColor(library, socketIdProp.intValue));

                DrawSocketPopup(library, direction, socketIdProp);
                DrawModifier(library, socketIdProp.intValue, flippedProp, rotationProp);

                EditorGUILayout.LabelField(
                    Notation(library, socketIdProp.intValue, flippedProp.boolValue, rotationProp.intValue),
                    EditorStyles.miniLabel,
                    GUILayout.Width(44f));
            }
        }

        private static string FaceName(WfcDirection direction)
        {
            switch (direction)
            {
                case WfcDirection.Right: return "derecha";
                case WfcDirection.Left: return "izquierda";
                case WfcDirection.Up: return "techo";
                case WfcDirection.Down: return "suelo";
                case WfcDirection.Forward: return "frente";
                case WfcDirection.Back: return "fondo";
                default: return string.Empty;
            }
        }

        private static Color SwatchColor(WfcSocketLibrary library, int socketId)
        {
            if (socketId == WfcSocketDescriptor.InvalidId) return new Color(0.4f, 0.4f, 0.4f, 0.4f);
            return library != null ? library.GetColor(socketId) : Color.magenta;
        }

        private static void DrawSocketPopup(
            WfcSocketLibrary library,
            WfcDirection direction,
            SerializedProperty socketIdProp)
        {
            int currentId = socketIdProp.intValue;

            var labels = new List<GUIContent> { new GUIContent("(vacio)") };
            var ids = new List<int> { WfcSocketDescriptor.InvalidId };

            if (library != null)
            {
                foreach (var entry in library.Entries)
                {
                    if (entry == null) continue;

                    // Solo sockets del eje correcto para esta cara.
                    if (!WfcSocketKinds.FitsDirection(entry.Kind, direction)) continue;

                    labels.Add(new GUIContent(entry.DisplayName));
                    ids.Add(entry.Id);
                }
            }

            int selected = ids.IndexOf(currentId);

            if (selected < 0)
            {
                // Puede ser un socket borrado o uno del eje equivocado. En ambos casos
                // se muestra tal cual, no se reasigna en silencio.
                bool exists = library != null && library.Contains(currentId);
                labels.Add(new GUIContent(exists
                    ? $"! {library.GetDisplayName(currentId)} (eje incorrecto)"
                    : $"<missing #{currentId}>"));
                ids.Add(currentId);
                selected = ids.Count - 1;
            }

            int next = EditorGUI.Popup(
                GUILayoutUtility.GetRect(60f, 18f, GUILayout.MinWidth(90f)),
                GUIContent.none,
                selected,
                labels.ToArray());

            if (next != selected) socketIdProp.intValue = ids[next];
        }

        private static void DrawModifier(
            WfcSocketLibrary library,
            int socketId,
            SerializedProperty flippedProp,
            SerializedProperty rotationProp)
        {
            if (library == null || !library.TryGetKind(socketId, out var kind))
            {
                GUILayout.Space(76f);
                return;
            }

            if (WfcSocketKinds.UsesFlipped(kind))
            {
                flippedProp.boolValue = EditorGUILayout.ToggleLeft(
                    "flip", flippedProp.boolValue, GUILayout.Width(76f));
                rotationProp.intValue = 0;
                return;
            }

            if (WfcSocketKinds.UsesRotationIndex(kind))
            {
                rotationProp.intValue = EditorGUILayout.IntPopup(
                    rotationProp.intValue,
                    new[] { "rot 0", "rot 1", "rot 2", "rot 3" },
                    new[] { 0, 1, 2, 3 },
                    GUILayout.Width(76f));
                flippedProp.boolValue = false;
                return;
            }

            flippedProp.boolValue = false;
            rotationProp.intValue = 0;
            GUILayout.Space(76f);
        }

        public static string Notation(WfcSocketLibrary library, int socketId, bool flipped, int rotationIndex)
        {
            if (socketId == WfcSocketDescriptor.InvalidId) return "-";
            if (library == null || !library.TryGetKind(socketId, out var kind)) return $"#{socketId}?";

            return new WfcSocketDescriptor(socketId, kind, flipped, rotationIndex).Notation();
        }
    }
}