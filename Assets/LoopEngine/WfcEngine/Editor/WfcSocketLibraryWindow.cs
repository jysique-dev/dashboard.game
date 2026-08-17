using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LoopEngine.WfcEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine.EditorTools
{
    /// <summary>
    /// Prueba visual de la sesion 1: ventana que lista los sockets de una libreria
    /// con su color, tipo, notacion y — para los asimetricos y rotacionales — todas
    /// las variantes que produce el modificador.
    ///
    /// Window > LoopEngine > WFC > Socket Library
    /// </summary>
    public class WfcSocketLibraryWindow : EditorWindow
    {
        private WfcSocketLibrary library;
        private Vector2 scroll;
        private string filter = string.Empty;
        private bool showVariants = true;

        [MenuItem(LoopRoutes.WFCToolRoute)]
        public static void Open()
        {
            var window = GetWindow<WfcSocketLibraryWindow>();
            window.titleContent = new GUIContent("WFC Sockets");
            window.minSize = new Vector2(360f, 260f);
            window.Show();
        }

        private void OnEnable()
        {
            if (library == null) library = FindFirstLibrary();
        }

        private static WfcSocketLibrary FindFirstLibrary()
        {
            var guids = AssetDatabase.FindAssets("t:WfcSocketLibrary");
            if (guids.Length == 0) return null;

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<WfcSocketLibrary>(path);
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (library == null)
            {
                EditorGUILayout.HelpBox(
                    "Sin libreria. Crea una con Assets > Create > LoopEngine > WFC > Socket Library.",
                    MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);

            int shown = 0;
            foreach (var entry in library.Entries)
            {
                if (entry == null) continue;
                if (!Matches(entry, filter)) continue;

                DrawEntryCard(entry);
                shown++;
            }

            if (shown == 0)
            {
                EditorGUILayout.LabelField("Ningun socket coincide con el filtro.", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndScrollView();

            DrawFooter();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var newLibrary = (WfcSocketLibrary)EditorGUILayout.ObjectField(
                    library, typeof(WfcSocketLibrary), false, GUILayout.Width(200f));

                if (newLibrary != library)
                {
                    library = newLibrary;
                    GUI.FocusControl(null);
                }

                filter = GUILayout.TextField(filter, EditorStyles.toolbarSearchField);

                showVariants = GUILayout.Toggle(
                    showVariants, "Variantes", EditorStyles.toolbarButton, GUILayout.Width(70f));
            }
        }

        private static bool Matches(WfcSocketEntry entry, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return true;

            return entry.DisplayName.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0
                || entry.Id.ToString() == filter.Trim();
        }

        private void DrawEntryCard(WfcSocketEntry entry)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var swatch = GUILayoutUtility.GetRect(14f, 14f, GUILayout.Width(14f));
                    swatch.y += 3f;
                    EditorGUI.DrawRect(swatch, entry.Color);

                    EditorGUILayout.LabelField(entry.DisplayName, EditorStyles.boldLabel, GUILayout.MinWidth(80f));

                    GUILayout.FlexibleSpace();

                    EditorGUILayout.LabelField(
                        WfcSocketKinds.ShortLabel(entry.Kind),
                        EditorStyles.miniLabel,
                        GUILayout.Width(52f));

                    EditorGUILayout.LabelField($"#{entry.Id}", EditorStyles.miniLabel, GUILayout.Width(34f));
                }

                if (showVariants)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(18f);
                        foreach (var variant in VariantsOf(entry))
                        {
                            GUILayout.Label(variant, EditorStyles.miniButton, GUILayout.Width(44f));
                        }

                        GUILayout.FlexibleSpace();
                        GUILayout.Label(PairingText(entry.Kind), EditorStyles.miniLabel);
                    }
                }

                if (!string.IsNullOrWhiteSpace(entry.Notes))
                {
                    EditorGUILayout.LabelField(entry.Notes, EditorStyles.miniLabel);
                }
            }
        }

        /// <summary>Todas las formas concretas que puede tomar este socket en una cara.</summary>
        private static IEnumerable<string> VariantsOf(WfcSocketEntry entry)
        {
            switch (entry.Kind)
            {
                case WfcSocketKind.HorizontalAsymmetric:
                    yield return entry.ToDescriptor(false).Notation();
                    yield return entry.ToDescriptor(true).Notation();
                    break;

                case WfcSocketKind.VerticalRotational:
                    for (int r = 0; r < 4; r++)
                    {
                        yield return entry.ToDescriptor(false, r).Notation();
                    }
                    break;

                default:
                    yield return entry.ToDescriptor().Notation();
                    break;
            }
        }

        private static string PairingText(WfcSocketKind kind)
        {
            switch (kind)
            {
                case WfcSocketKind.HorizontalSymmetric: return "encaja consigo mismo";
                case WfcSocketKind.HorizontalAsymmetric: return "encaja con su espejo";
                case WfcSocketKind.VerticalInvariant: return "encaja en cualquier giro";
                case WfcSocketKind.VerticalRotational: return "encaja con igual indice";
                default: return string.Empty;
            }
        }

        private void DrawFooter()
        {
            int horizontal = 0;
            int vertical = 0;
            int variants = 0;

            foreach (var entry in library.Entries)
            {
                if (entry == null) continue;

                if (entry.IsHorizontal) horizontal++;
                else vertical++;

                switch (entry.Kind)
                {
                    case WfcSocketKind.HorizontalAsymmetric: variants += 2; break;
                    case WfcSocketKind.VerticalRotational: variants += 4; break;
                    default: variants += 1; break;
                }
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(
                    $"{library.Count} sockets · {horizontal} H / {vertical} V · {variants} variantes",
                    EditorStyles.miniLabel);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Seleccionar asset", EditorStyles.toolbarButton, GUILayout.Width(120f)))
                {
                    Selection.activeObject = library;
                    EditorGUIUtility.PingObject(library);
                }
            }
        }
    }
}