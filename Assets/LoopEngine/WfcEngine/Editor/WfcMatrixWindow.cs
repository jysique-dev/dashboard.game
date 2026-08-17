using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LoopEngine.WfcEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine.EditorTools
{
    /// <summary>
    /// Prueba visual de la sesion 4: heatmap de la matriz de vecindad, una direccion a la
    /// vez, mas el panel de diagnostico.
    ///
    /// Fila = variante en la celda origen, columna = variante permitida en la vecina.
    /// Una fila entera oscura es una variante que no puede colocarse: el fallo mas comun
    /// de autoria y el que mas tiempo hace perder si se descubre ya con el solver escrito.
    ///
    /// Window > LoopEngine > WFC > Neighbor Matrix
    /// </summary>
    public class WfcMatrixWindow : EditorWindow
    {
        private WfcModuleSet set;
        private WfcExclusionSet exclusions;
        private WfcDirection direction = WfcDirection.Right;
        private float cellPixels = 10f;
        private bool highlightDead = true;

        private WfcAdjacency adjacency;
        private Vector2 gridScroll;
        private Vector2 panelScroll;
        private int hoveredRow = -1;
        private int hoveredColumn = -1;

        private readonly HashSet<int> deadVariants = new HashSet<int>();

        private static readonly Color AllowedColor = new Color(0.35f, 0.78f, 0.45f);
        private static readonly Color ForbiddenColor = new Color(0.16f, 0.16f, 0.18f);
        private static readonly Color DeadRowColor = new Color(0.55f, 0.18f, 0.18f);
        private static readonly Color HoverColor = new Color(1f, 1f, 1f, 0.25f);

        [MenuItem(LoopRoutes.WFCNeighbotMatrix)]
        public static void Open()
        {
            var window = GetWindow<WfcMatrixWindow>();
            window.titleContent = new GUIContent("WFC Matrix");
            window.minSize = new Vector2(520f, 380f);
            window.Show();
        }

        private void OnEnable()
        {
            if (set == null)
            {
                var guids = AssetDatabase.FindAssets("t:WfcModuleSet");
                if (guids.Length > 0)
                {
                    set = AssetDatabase.LoadAssetAtPath<WfcModuleSet>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            Rebuild();
        }

        private void Rebuild()
        {
            adjacency = set != null ? WfcAdjacencyBuilder.Build(set, exclusions) : null;

            deadVariants.Clear();
            if (adjacency != null)
            {
                foreach (var dead in adjacency.DeadVariants) deadVariants.Add(dead.VariantIndex);
            }

            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (adjacency == null || adjacency.Matrix == null)
            {
                EditorGUILayout.HelpBox(
                    "Asigna un Module Set con al menos un modulo valido.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawGrid();
                DrawPanel();
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();

                set = (WfcModuleSet)EditorGUILayout.ObjectField(
                    set, typeof(WfcModuleSet), false, GUILayout.Width(160f));

                exclusions = (WfcExclusionSet)EditorGUILayout.ObjectField(
                    exclusions, typeof(WfcExclusionSet), false, GUILayout.Width(160f));

                if (EditorGUI.EndChangeCheck()) Rebuild();

                direction = (WfcDirection)EditorGUILayout.EnumPopup(
                    direction, EditorStyles.toolbarPopup, GUILayout.Width(80f));

                GUILayout.Label(WfcDirections.ShortName(direction), EditorStyles.miniLabel, GUILayout.Width(24f));

                cellPixels = GUILayout.HorizontalSlider(cellPixels, 4f, 24f, GUILayout.Width(70f));

                highlightDead = GUILayout.Toggle(
                    highlightDead, "Marcar muertas", EditorStyles.toolbarButton, GUILayout.Width(105f));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Recalcular", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                {
                    Rebuild();
                }
            }
        }

        private void DrawGrid()
        {
            var matrix = adjacency.Matrix;
            int n = matrix.VariantCount;

            gridScroll = EditorGUILayout.BeginScrollView(
                gridScroll, GUILayout.Width(position.width * 0.62f));

            var area = GUILayoutUtility.GetRect(n * cellPixels, n * cellPixels);

            hoveredRow = -1;
            hoveredColumn = -1;

            var mouse = Event.current.mousePosition;

            for (int row = 0; row < n; row++)
            {
                bool rowIsDead = highlightDead && deadVariants.Contains(row);

                for (int column = 0; column < n; column++)
                {
                    var rect = new Rect(
                        area.x + column * cellPixels,
                        area.y + row * cellPixels,
                        cellPixels - 1f,
                        cellPixels - 1f);

                    bool allowed = matrix.IsAllowed(direction, row, column);

                    Color color;
                    if (allowed) color = AllowedColor;
                    else if (rowIsDead) color = DeadRowColor;
                    else color = ForbiddenColor;

                    EditorGUI.DrawRect(rect, color);

                    if (rect.Contains(mouse))
                    {
                        hoveredRow = row;
                        hoveredColumn = column;
                    }
                }
            }

            if (hoveredRow >= 0)
            {
                EditorGUI.DrawRect(
                    new Rect(area.x, area.y + hoveredRow * cellPixels, n * cellPixels, cellPixels - 1f),
                    HoverColor);

                EditorGUI.DrawRect(
                    new Rect(area.x + hoveredColumn * cellPixels, area.y, cellPixels - 1f, n * cellPixels),
                    HoverColor);

                Repaint();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawPanel()
        {
            panelScroll = EditorGUILayout.BeginScrollView(panelScroll);

            var matrix = adjacency.Matrix;

            EditorGUILayout.LabelField(
                $"{matrix.VariantCount} variantes · densidad {matrix.Density() * 100f:0.0}% · " +
                $"{adjacency.AverageOptionsPerFace():0.0} opciones por cara",
                EditorStyles.miniBoldLabel);

            if (adjacency.Filter != null && adjacency.Filter.RuleCount > 0)
            {
                EditorGUILayout.LabelField(
                    $"{adjacency.Filter.RuleCount} regla(s) de exclusion aplicada(s)",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(4);
            DrawHover();

            EditorGUILayout.Space(6);
            DrawUnmatchedSockets();

            EditorGUILayout.Space(6);
            DrawDeadVariants();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHover()
        {
            if (hoveredRow < 0)
            {
                EditorGUILayout.LabelField("Pasa el raton por el heatmap.", EditorStyles.miniLabel);
                return;
            }

            var baked = adjacency.Baked;
            var rowView = baked.GetView(hoveredRow);
            var columnView = baked.GetView(hoveredColumn);

            var socketA = baked.GetSocket(hoveredRow, direction);
            var socketB = baked.GetSocket(hoveredColumn, WfcDirections.Opposite(direction));

            bool allowed = adjacency.Matrix.IsAllowed(direction, hoveredRow, hoveredColumn);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    $"{rowView.DisplayName}  {WfcDirections.ShortName(direction)}  {columnView.DisplayName}",
                    EditorStyles.boldLabel);

                EditorGUILayout.LabelField(
                    $"{socketA.Notation()}  vs  {socketB.Notation()}  ->  " +
                    (allowed ? "permitido" : "prohibido"),
                    EditorStyles.miniLabel);

                EditorGUILayout.LabelField(
                    $"{adjacency.Matrix.CountAllowed(direction, hoveredRow)} vecinos posibles en esta cara",
                    EditorStyles.miniLabel);
            }
        }

        private void DrawUnmatchedSockets()
        {
            if (adjacency.UnmatchedSockets.Count == 0)
            {
                EditorGUILayout.LabelField("Todos los sockets tienen contraparte.", EditorStyles.miniLabel);
                return;
            }

            EditorGUILayout.LabelField(
                $"Sockets sin contraparte ({adjacency.UnmatchedSockets.Count})", EditorStyles.boldLabel);

            var library = set != null ? set.Library : null;

            foreach (var unmatched in adjacency.UnmatchedSockets)
            {
                string name = library != null
                    ? library.GetDisplayName(unmatched.Socket.Id)
                    : $"#{unmatched.Socket.Id}";

                EditorGUILayout.HelpBox(
                    $"{name} [{unmatched.Socket.Notation()}] se usa {unmatched.UsageCount} vez(es), " +
                    $"pero ningun modulo tiene {unmatched.RequiredCounterpart.Notation()} en la cara opuesta.",
                    MessageType.Warning);
            }
        }

        private void DrawDeadVariants()
        {
            if (adjacency.DeadVariants.Count == 0)
            {
                EditorGUILayout.LabelField("Ninguna variante muerta.", EditorStyles.miniLabel);
                return;
            }

            EditorGUILayout.LabelField(
                $"Variantes muertas ({adjacency.DeadVariants.Count})", EditorStyles.boldLabel);

            foreach (var dead in adjacency.DeadVariants)
            {
                var view = adjacency.Baked.GetView(dead.VariantIndex);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox(
                        $"{view.DisplayName}: sin vecinos en {WfcDirections.ShortName(dead.Direction)}.",
                        MessageType.Error);

                    if (view.Module != null &&
                        GUILayout.Button("Ir", EditorStyles.miniButton, GUILayout.Width(30f)))
                    {
                        Selection.activeObject = view.Module;
                        EditorGUIUtility.PingObject(view.Module);
                    }
                }
            }
        }
    }
}