using UnityEngine;

namespace LoopEngine.GridEngine.Visualization
{
    /// <summary>
    /// Dibuja la grilla con Gizmos para depuración. Los Gizmos solo se ven en la ventana
    /// Scene y no afectan al juego ni al render final.
    ///
    /// - En modo edición usa 'previewSettings' para mostrar una vista estática.
    /// - En Play, si el mismo GameObject tiene un IGridProvider listo, dibuja la grilla REAL.
    ///
    /// Es de solo lectura: no modifica la grilla, solo la observa a través de IReadOnlyGrid.
    /// </summary>
    [ExecuteAlways]
    public class GridGizmoView : MonoBehaviour
    {
        [Tooltip("Config usada para previsualizar en modo edición. En Play se prefiere la grilla viva.")]
        [SerializeField] private GridSettings previewSettings;
        [SerializeField] private ColorEngine.PaletteColorReference lineColor;
        [SerializeField] private bool drawCellCenters = false;

        private IGridProvider _provider;

        private void Awake() => _provider = GetComponent<IGridProvider>();

        private void OnDrawGizmos()
        {
            IReadOnlyGrid grid = GetGridForDrawing();
            if (grid == null) return;

            Gizmos.color = lineColor.Value;

            // Líneas verticales (incluye borde derecho en x = Width).
            for (int x = 0; x <= grid.Width; x++)
                Gizmos.DrawLine(
                    grid.CellToWorld(new Vector2Int(x, 0)),
                    grid.CellToWorld(new Vector2Int(x, grid.Height)));

            // Líneas horizontales (incluye borde superior en y = Height).
            for (int y = 0; y <= grid.Height; y++)
                Gizmos.DrawLine(
                    grid.CellToWorld(new Vector2Int(0, y)),
                    grid.CellToWorld(new Vector2Int(grid.Width, y)));

            if (drawCellCenters)
            {
                float r = grid.CellSize * 0.05f;
                foreach (Vector2Int c in grid.AllCoordinates())
                    Gizmos.DrawSphere(grid.GetCellCenterWorld(c), r);
            }
        }

        private IReadOnlyGrid GetGridForDrawing()
        {
            // En Play refleja la grilla real si está lista.
            if (Application.isPlaying)
            {
                if (_provider == null) _provider = GetComponent<IGridProvider>();
                if (_provider?.Grid != null) return _provider.Grid;
            }

            // En edición (o si aún no hay grilla viva) usa una vista estática desde settings.
            // Nota: esto asigna GC por redibujado; es aceptable solo para Gizmos de editor.
            return previewSettings != null ? previewSettings.CreateGrid<int>() : null;
        }
    }
}