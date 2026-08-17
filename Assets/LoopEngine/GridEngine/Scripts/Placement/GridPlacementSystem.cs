using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.GridEngine.Placement
{
    /// <summary>
    /// Lógica de colocación sobre una Grid&lt;PlacedObject&gt;. NO conoce el input ni el ghost:
    /// solo valida, coloca y quita. Por eso la reutilizan tanto la colocación del usuario (S4)
    /// como las herramientas de construcción del editor (S5), llamando a estos mismos métodos.
    ///
    /// Depende del host tipado (PlacementGridHost) porque necesita escribir ocupantes en las
    /// celdas; IGridProvider solo da la vista de solo lectura.
    /// </summary>
    [AddComponentMenu("LoopEngine/Grid/Grid Placement System")]
    public class GridPlacementSystem : MonoBehaviour
    {
        [Tooltip("Host de la grilla de ocupación. Si se deja vacío, se busca en este objeto o sus padres.")]
        [SerializeField] private PlacementGridHost host;

        [Tooltip("Padre opcional para los visuales instanciados. Si es null, se usa este transform.")]
        [SerializeField] private Transform visualParent;

        private readonly List<Vector2Int> _buffer = new();

        public event Action<PlacedObject> OnPlaced;
        public event Action<PlacedObject> OnRemoved;

        private Grid<PlacedObject> Grid => host != null ? host.TypedGrid : null;
        public bool IsReady => Grid != null;

        private void Awake()
        {
            if (host == null) host = GetComponentInParent<PlacementGridHost>();
            if (host == null)
                Debug.LogError("[GridPlacementSystem] No se encontró un PlacementGridHost.", this);
            if (visualParent == null) visualParent = transform;
        }

        /// <summary>
        /// ¿Se puede colocar 'def' en 'anchor' con 'rotation'? Rellena 'cellsOut' con la huella
        /// (aunque sea inválida) para poder mostrar el ghost en rojo.
        /// </summary>
        public bool CanPlace(PlaceableDefinition def, Vector2Int anchor, GridRotation rotation,
                             List<Vector2Int> cellsOut = null)
        {
            Grid<PlacedObject> grid = Grid;
            if (grid == null || def == null) { cellsOut?.Clear(); return false; }

            GridFootprint.ComputeCells(def, anchor, rotation, _buffer);
            if (cellsOut != null) { cellsOut.Clear(); cellsOut.AddRange(_buffer); }

            foreach (Vector2Int c in _buffer)
                if (!grid.IsInside(c) || grid.GetCell(c) != null)
                    return false;

            return true;
        }

        /// <summary>Coloca la pieza si es válido. Devuelve el PlacedObject o null si no se pudo.</summary>
        public PlacedObject TryPlace(PlaceableDefinition def, Vector2Int anchor, GridRotation rotation)
        {
            if (!CanPlace(def, anchor, rotation)) return null;

            Grid<PlacedObject> grid = Grid;
            var cells = GridFootprint.ComputeCells(def, anchor, rotation);
            var placed = new PlacedObject(def, anchor, rotation, cells);

            if (def.visualPrefab != null)
            {
                Vector3 pos = FootprintCenterWorld(cells);
                Quaternion rot = VisualRotation(rotation);
                placed.Visual = Instantiate(def.visualPrefab, pos, rot, visualParent);
            }

            foreach (Vector2Int c in cells)
                grid.SetCell(c, placed); // dispara OnCellChanged por celda

            OnPlaced?.Invoke(placed);
            return placed;
        }

        /// <summary>Quita el objeto que ocupa 'cell' (cualquiera de sus celdas). Devuelve true si quitó algo.</summary>
        public bool TryRemoveAt(Vector2Int cell)
        {
            Grid<PlacedObject> grid = Grid;
            if (grid == null || !grid.IsInside(cell)) return false;

            PlacedObject placed = grid.GetCell(cell);
            if (placed == null) return false;

            foreach (Vector2Int c in placed.Cells)
                grid.SetCell(c, null);

            if (placed.Visual != null) Destroy(placed.Visual);

            OnRemoved?.Invoke(placed);
            return true;
        }

        /// <summary>Objeto que ocupa una celda, o null.</summary>
        public PlacedObject GetAt(Vector2Int cell)
        {
            Grid<PlacedObject> grid = Grid;
            return (grid != null && grid.TryGetCell(cell, out PlacedObject p)) ? p : null;
        }

        // --- Colocación del visual: centro de la huella + giro alrededor de la normal del plano ---

        private Vector3 FootprintCenterWorld(List<Vector2Int> cells)
        {
            // Centro geométrico de los centros de celda: deja el prefab centrado sobre su huella,
            // sea cual sea la rotación. Asume que el pivote del prefab está en su centro.
            Vector3 sum = Vector3.zero;
            foreach (Vector2Int c in cells) sum += Grid.GetCellCenterWorld(c);
            return sum / Mathf.Max(1, cells.Count);
        }

        private Quaternion VisualRotation(GridRotation rotation)
        {
            Vector3 axis = Grid.Plane == GridPlane.XZ ? Vector3.up : Vector3.forward;
            return Quaternion.AngleAxis(rotation.ToDegrees(), axis);
        }
    }
}