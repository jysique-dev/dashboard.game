using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LoopEngine.WfcEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine.EditorTools
{
    /// <summary>
    /// Prueba visual de la sesion 2: rejilla de modulos con su preview y las 6 caras
    /// etiquetadas alrededor. Un vistazo basta para ver si un modulo tiene una cara
    /// vacia, un socket del eje equivocado o una referencia rota.
    ///
    /// Window > LoopEngine > WFC > Module Gallery
    /// </summary>
    public class WfcModuleGalleryWindow : EditorWindow
    {
        private WfcModuleSet set;
        private Vector2 scroll;
        private string filter = string.Empty;
        private float cardWidth = 210f;
        private bool onlyProblems;

        private readonly List<WfcValidationIssue> issues = new List<WfcValidationIssue>();

        [MenuItem(LoopRoutes.WFCModuleGaleryRoute)]
        public static void Open()
        {
            var window = GetWindow<WfcModuleGalleryWindow>();
            window.titleContent = new GUIContent("WFC Modules");
            window.minSize = new Vector2(420f, 320f);
            window.Show();
        }

        public static void OpenWith(WfcModuleSet target)
        {
            Open();
            var window = GetWindow<WfcModuleGalleryWindow>();
            window.set = target;
            window.Repaint();
        }

        private void OnEnable()
        {
            if (set == null) set = FindFirstSet();
        }

        private static WfcModuleSet FindFirstSet()
        {
            var guids = AssetDatabase.FindAssets("t:WfcModuleSet");
            if (guids.Length == 0) return null;

            return AssetDatabase.LoadAssetAtPath<WfcModuleSet>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (set == null)
            {
                EditorGUILayout.HelpBox(
                    "Sin set. Crea uno con Assets > Create > LoopEngine > WFC > Module Set.",
                    MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);

            int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 24f) / cardWidth));
            int drawn = 0;
            bool rowOpen = false;

            foreach (var module in set.Modules)
            {
                if (module == null) continue;
                if (!Matches(module, filter)) continue;

                issues.Clear();
                WfcModuleValidation.Validate(module, issues);
                var worst = WfcModuleValidation.WorstSeverity(issues);

                if (onlyProblems && worst == WfcIssueSeverity.Info) continue;

                if (drawn % columns == 0)
                {
                    if (rowOpen) EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    rowOpen = true;
                }

                DrawCard(module, worst, issues.Count);
                drawn++;
            }

            if (rowOpen) EditorGUILayout.EndHorizontal();

            if (drawn == 0)
            {
                EditorGUILayout.LabelField("Ningun modulo coincide.", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndScrollView();

            DrawFooter();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var newSet = (WfcModuleSet)EditorGUILayout.ObjectField(
                    set, typeof(WfcModuleSet), false, GUILayout.Width(190f));

                if (newSet != set)
                {
                    set = newSet;
                    GUI.FocusControl(null);
                }

                filter = GUILayout.TextField(filter, EditorStyles.toolbarSearchField);

                onlyProblems = GUILayout.Toggle(
                    onlyProblems, "Solo problemas", EditorStyles.toolbarButton, GUILayout.Width(105f));

                cardWidth = GUILayout.HorizontalSlider(cardWidth, 160f, 320f, GUILayout.Width(80f));
            }
        }

        private static bool Matches(WfcModuleDefinition module, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return true;
            return module.name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawCard(WfcModuleDefinition module, WfcIssueSeverity worst, int issueCount)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(cardWidth - 8f)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawPreview(module, 72f);

                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField(module.name, EditorStyles.boldLabel);

                        EditorGUILayout.LabelField(
                            $"peso {module.Weight:0.##} � {WfcRotationMasks.ShortLabel(module.AllowedRotations)}",
                            EditorStyles.miniLabel);

                        EditorGUILayout.LabelField(
                            module.IsAir ? "modulo de aire" : module.Prefab.name,
                            EditorStyles.miniLabel);

                        if (worst != WfcIssueSeverity.Info)
                        {
                            var color = worst == WfcIssueSeverity.Error
                                ? new Color(0.9f, 0.35f, 0.3f)
                                : new Color(0.95f, 0.75f, 0.25f);

                            var badge = GUILayoutUtility.GetRect(60f, 14f, GUILayout.Width(60f));
                            EditorGUI.DrawRect(badge, color);
                            EditorGUI.LabelField(badge, $" {issueCount} issue(s)", EditorStyles.miniLabel);
                        }

                        if (GUILayout.Button("Seleccionar", EditorStyles.miniButton))
                        {
                            Selection.activeObject = module;
                            EditorGUIUtility.PingObject(module);
                        }
                    }
                }

                EditorGUILayout.Space(2);

                for (int i = 0; i < WfcDirections.Count; i++)
                {
                    DrawFaceChip(module, (WfcDirection)i);
                }
            }
        }

        private void DrawPreview(WfcModuleDefinition module, float size)
        {
            var rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));

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
                return;
            }

            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 0.4f));
            EditorGUI.LabelField(rect, "...", EditorStyles.centeredGreyMiniLabel);

            // Los previews se generan de forma asincrona: repintar hasta que existan.
            if (AssetPreview.IsLoadingAssetPreview(module.Prefab.GetInstanceID())) Repaint();
        }

        private void DrawFaceChip(WfcModuleDefinition module, WfcDirection direction)
        {
            var face = module.GetFace(direction);
            var descriptor = face.Descriptor;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    WfcDirections.ShortName(direction), EditorStyles.miniBoldLabel, GUILayout.Width(24f));

                var swatch = GUILayoutUtility.GetRect(10f, 10f, GUILayout.Width(10f));
                swatch.y += 3f;
                EditorGUI.DrawRect(swatch, face.DisplayColor);

                string label;
                if (!face.IsAssigned) label = "(vacio)";
                else if (!face.IsResolvable) label = $"<missing #{face.socketId}>";
                else if (!WfcSocketKinds.FitsDirection(descriptor.Kind, direction))
                    label = $"! {module.Library.GetDisplayName(face.socketId)} eje incorrecto";
                else
                    label = $"{module.Library.GetDisplayName(face.socketId)}  {descriptor.Notation()}";

                EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
            }
        }

        private void DrawFooter()
        {
            issues.Clear();
            WfcModuleValidation.Validate(set, issues);

            int errors = WfcModuleValidation.CountBySeverity(issues, WfcIssueSeverity.Error);
            int warnings = WfcModuleValidation.CountBySeverity(issues, WfcIssueSeverity.Warning);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(
                    $"{set.Count} modulos � {set.CountBakedVariants()} variantes � " +
                    $"{errors} error(es) � {warnings} aviso(s)",
                    EditorStyles.miniLabel);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Seleccionar set", EditorStyles.toolbarButton, GUILayout.Width(110f)))
                {
                    Selection.activeObject = set;
                    EditorGUIUtility.PingObject(set);
                }
            }
        }
    }
}