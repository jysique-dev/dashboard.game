using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LoopEngine.GridEngine;
using LoopEngine.GridEngine.Placement;
using LoopEngine.GridEngine.Builder;

namespace LoopEngine.GridEngine.Builder.EditorTools
{
    /// <summary>
    /// Editor de autoría de ciudades.
    ///  E1: dibuja grid y piezas guardadas.  E2: hover + ghost verde/rojo.
    ///  E3: pintar/borrar/arrastrar/rotar con Undo.
    ///  E4: vista previa de los prefabs reales en modo edición (no se guarda en la escena).
    ///
    /// La ocupación se calcula desde el CityLayout (no hay grilla viva en modo edición).
    /// </summary>
    [CustomEditor(typeof(CityLayoutAuthoring))]
    public class CityLayoutAuthoringEditor : Editor
    {
        private readonly List<Vector2Int> _cellBuffer = new();
        private readonly HashSet<Vector2Int> _occupied = new();
        private Vector2Int? _lastPaintCell;

        private readonly CityLayoutPreview _preview = new();
        private bool _previewDirty;
        private bool _previewBuilt;

        private static readonly Color ValidColor = new Color(0.3f, 1f, 0.4f, 0.35f);
        private static readonly Color InvalidColor = new Color(1f, 0.3f, 0.3f, 0.35f);

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            _preview.Clear(target as CityLayoutAuthoring); // limpia huérfanos tras recompilar
            _previewDirty = true;
            _previewBuilt = false;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            _preview.Clear(target as CityLayoutAuthoring); // no dejar la vista previa al deseleccionar
            _previewBuilt = false;
        }

        private void OnUndoRedo()
        {
            _previewDirty = true;
            SceneView.RepaintAll();
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            // Antes de entrar en Play, quita la vista previa para que no se mezcle con lo real.
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                _preview.Clear(target as CityLayoutAuthoring);
                _previewBuilt = false;
            }
        }

        public override void OnInspectorGUI()
        {
            var authoring = (CityLayoutAuthoring)target;

            DrawDefaultInspector();
            EditorGUILayout.Space();

            // Validaciones
            if (authoring.settings == null)
                EditorGUILayout.HelpBox("Asigna un GridSettings para dibujar y pintar.", MessageType.Warning);
            if (authoring.layout == null)
                EditorGUILayout.HelpBox("Asigna un CityLayout donde guardar las piezas.", MessageType.Warning);

            // Paleta de piezas (botones para elegir la activa)
            /*
            if (authoring.palette != null && authoring.palette.Count > 0)
            {
                EditorGUILayout.LabelField("Paleta", EditorStyles.boldLabel);
                const int columns = 3;
                for (int i = 0; i < authoring.palette.Count; i++)
                {
                    if (i % columns == 0) EditorGUILayout.BeginHorizontal();

                    PlaceableDefinition p = authoring.palette[i];
                    string label = p != null ? (string.IsNullOrEmpty(p.id) ? p.name : p.id) : "(vacío)";
                    bool active = p != null && p == authoring.activePlaceable;

                    GUI.backgroundColor = active ? Color.green : Color.white;
                    if (GUILayout.Button(label, GUILayout.Height(24)))
                    {
                        Undo.RecordObject(authoring, "Seleccionar pieza");
                        authoring.activePlaceable = p;
                        SceneView.RepaintAll();
                    }
                    GUI.backgroundColor = Color.white;

                    if (i % columns == columns - 1 || i == authoring.palette.Count - 1)
                        EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.Space();
            }
            */
            // Rotación
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Rotación: {authoring.currentRotation}", GUILayout.Width(150));
                if (GUILayout.Button("Rotar (R)"))
                {
                    Undo.RecordObject(authoring, "Rotar pieza");
                    authoring.currentRotation = authoring.currentRotation.RotatedCW();
                    SceneView.RepaintAll();
                }
            }

            // Acciones sobre el layout
            if (authoring.layout != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Piezas en el layout: {authoring.layout.entries.Count}");
                if (GUILayout.Button("Abrir Builder Tool"))
                {
                    BuilderWindowEditor.Open();
                }


                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Reconstruir vista previa"))
                    {
                        _previewDirty = true;
                        SceneView.RepaintAll();
                    }
                    if (GUILayout.Button("Guardar asset"))
                    {
                        EditorUtility.SetDirty(authoring.layout);
                        AssetDatabase.SaveAssets();
                    }
                }

                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("Vaciar layout") &&
                    EditorUtility.DisplayDialog("Vaciar layout",
                        "¿Borrar todas las piezas del CityLayout?", "Vaciar", "Cancelar"))
                {
                    Undo.RecordObject(authoring.layout, "Vaciar layout");
                    authoring.layout.Clear();
                    EditorUtility.SetDirty(authoring.layout);
                    _previewDirty = true;
                    SceneView.RepaintAll();
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.HelpBox(
                "En la Scene: clic izq colocar · clic der borrar · arrastrar pintar · R rotar.",
                MessageType.Info);
        }

        private void OnSceneGUI()
        {
            var authoring = (CityLayoutAuthoring)target;
            if (authoring.settings == null) return;

            Event e = Event.current;
            IReadOnlyGrid grid = authoring.settings.CreateGrid<int>();

            RebuildOccupied(authoring);
            DrawGrid(grid, authoring.gridColor);
            DrawOccupied(grid, authoring.authoredColor);

            if (authoring.activePlaceable != null)
            {
                int controlID = GUIUtility.GetControlID(FocusType.Passive);
                HandleUtility.AddDefaultControl(controlID);

                bool hasCell = TryGetHoveredCell(grid, out Vector2Int cell);
                if (hasCell) DrawGhost(authoring, grid, cell);
                HandleInput(authoring, grid, e, hasCell, cell);
            }

            UpdatePreview(authoring, grid, e);
        }

        // --- Vista previa (E4) ---

        private void UpdatePreview(CityLayoutAuthoring authoring, IReadOnlyGrid grid, Event e)
        {
            if (!authoring.previewEnabled)
            {
                if (_previewBuilt) { _preview.Clear(authoring); _previewBuilt = false; }
                return;
            }

            // Reconstruye cuando esté marcada como sucia, pero nunca durante un arrastre
            // (evita destruir/crear toda la ciudad en cada celda pintada).
            bool needBuild = (_previewDirty || !_previewBuilt) && e.type != EventType.MouseDrag;
            if (needBuild)
            {
                _preview.Rebuild(authoring, grid);
                _previewDirty = false;
                _previewBuilt = true;
            }
        }

        // --- Entrada ---

        private void HandleInput(CityLayoutAuthoring authoring, IReadOnlyGrid grid,
                                 Event e, bool hasCell, Vector2Int cell)
        {
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.R)
            {
                Undo.RecordObject(authoring, "Rotar pieza");
                authoring.currentRotation = authoring.currentRotation.RotatedCW();
                e.Use();
                SceneView.RepaintAll();
                return;
            }

            if (e.type == EventType.MouseUp)
            {
                _lastPaintCell = null;
                return;
            }

            if (!hasCell)
            {
                if (e.type == EventType.MouseMove) SceneView.RepaintAll();
                return;
            }

            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
            {
                if (e.type == EventType.MouseDown) _lastPaintCell = null;
                TryPaint(authoring, grid, cell);
                e.Use();
                SceneView.RepaintAll();
                return;
            }

            if (e.type == EventType.MouseDown && e.button == 1)
            {
                TryErase(authoring, cell);
                e.Use();
                SceneView.RepaintAll();
                return;
            }

            if (e.type == EventType.MouseMove) SceneView.RepaintAll();
        }

        private void TryPaint(CityLayoutAuthoring authoring, IReadOnlyGrid grid, Vector2Int anchor)
        {
            if (_lastPaintCell == anchor) return;
            _lastPaintCell = anchor;
            if (authoring.layout == null) return;

            GridFootprint.ComputeCells(authoring.activePlaceable, anchor, authoring.currentRotation, _cellBuffer);
            foreach (Vector2Int c in _cellBuffer)
                if (!grid.IsInside(c) || _occupied.Contains(c))
                    return;

            Undo.RecordObject(authoring.layout, "Pintar pieza");
            authoring.layout.Add(authoring.activePlaceable, anchor, authoring.currentRotation);
            EditorUtility.SetDirty(authoring.layout);
            _previewDirty = true;
        }

        private void TryErase(CityLayoutAuthoring authoring, Vector2Int cell)
        {
            CityLayout layout = authoring.layout;
            if (layout == null) return;

            for (int i = layout.entries.Count - 1; i >= 0; i--)
            {
                CityLayout.Entry entry = layout.entries[i];
                if (entry.placeable == null) continue;

                GridFootprint.ComputeCells(entry.placeable, entry.anchor, entry.rotation, _cellBuffer);
                if (_cellBuffer.Contains(cell))
                {
                    Undo.RecordObject(layout, "Borrar pieza");
                    layout.entries.RemoveAt(i);
                    EditorUtility.SetDirty(layout);
                    _previewDirty = true;
                    return;
                }
            }
        }

        // --- Ocupación autoriada ---

        private void RebuildOccupied(CityLayoutAuthoring authoring)
        {
            _occupied.Clear();
            if (authoring.layout == null) return;

            foreach (CityLayout.Entry entry in authoring.layout.entries)
            {
                if (entry.placeable == null) continue;
                GridFootprint.ComputeCells(entry.placeable, entry.anchor, entry.rotation, _cellBuffer);
                foreach (Vector2Int c in _cellBuffer) _occupied.Add(c);
            }
        }

        // --- Dibujo ---

        private void DrawGrid(IReadOnlyGrid grid, Color color)
        {
            Handles.color = color;
            for (int x = 0; x <= grid.Width; x++)
                Handles.DrawLine(grid.CellToWorld(new Vector2Int(x, 0)),
                                 grid.CellToWorld(new Vector2Int(x, grid.Height)));
            for (int y = 0; y <= grid.Height; y++)
                Handles.DrawLine(grid.CellToWorld(new Vector2Int(0, y)),
                                 grid.CellToWorld(new Vector2Int(grid.Width, y)));
        }

        private void DrawOccupied(IReadOnlyGrid grid, Color color)
        {
            Handles.color = color;
            foreach (Vector2Int cell in _occupied)
                DrawCellFill(grid, cell);
        }

        private void DrawGhost(CityLayoutAuthoring authoring, IReadOnlyGrid grid, Vector2Int anchor)
        {
            GridFootprint.ComputeCells(authoring.activePlaceable, anchor, authoring.currentRotation, _cellBuffer);

            bool valid = true;
            foreach (Vector2Int c in _cellBuffer)
                if (!grid.IsInside(c) || _occupied.Contains(c)) { valid = false; break; }

            Handles.color = valid ? ValidColor : InvalidColor;
            foreach (Vector2Int c in _cellBuffer)
                if (grid.IsInside(c)) DrawCellFill(grid, c);
        }

        // --- Utilidades ---

        private bool TryGetHoveredCell(IReadOnlyGrid grid, out Vector2Int cell)
        {
            cell = default;
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            Vector3 normal = grid.Plane == GridPlane.XZ ? Vector3.up : Vector3.forward;
            var plane = new Plane(normal, grid.Origin);

            if (plane.Raycast(ray, out float enter))
            {
                Vector3 world = ray.GetPoint(enter);
                return grid.TryWorldToCell(world, out cell);
            }
            return false;
        }

        private static void DrawCellFill(IReadOnlyGrid grid, Vector2Int cell)
        {
            Vector3 c0 = grid.CellToWorld(cell);
            Vector3 c1 = grid.CellToWorld(cell + new Vector2Int(1, 0));
            Vector3 c2 = grid.CellToWorld(cell + new Vector2Int(1, 1));
            Vector3 c3 = grid.CellToWorld(cell + new Vector2Int(0, 1));
            Handles.DrawAAConvexPolygon(c0, c1, c2, c3);
        }
    }
}