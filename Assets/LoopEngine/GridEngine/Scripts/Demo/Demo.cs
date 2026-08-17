using UnityEngine;

namespace LoopEngine.GridEngine.Demo
{
    /// <summary>
    /// Demo de validación de la Sesión 1. NO forma parte del sistema final:
    /// solo sirve para comprobar visualmente que el núcleo funciona.
    /// Construye una Grid&lt;int&gt; desde GridSettings, dibuja las líneas con Gizmos
    /// y prueba la ida y vuelta mundo &lt;-&gt; celda.
    ///
    /// Uso:
    ///   1. Crea un asset: clic derecho en Project > Create > LoopEngine/Grid/Grid Settings.
    ///   2. Añade este componente a un GameObject vacío y asígnale ese asset.
    ///   3. Entra en Play: revisa la consola. Verás la grilla dibujada en la vista de escena.
    /// </summary>
    public class GridDemo : MonoBehaviour
    {
        [SerializeField] private GridSettings settings;
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private Color gizmoColor = Color.cyan;

        private Grid<int> _grid;

        private void Awake()
        {
            if (settings == null)
            {
                Debug.LogError("[GridDemo] Falta asignar GridSettings.", this);
                return;
            }

            _grid = settings.CreateGrid<int>((g, coord) => 0);

            // Prueba de ida y vuelta: celda -> centro en el mundo -> celda de nuevo.
            Vector2Int probe = new Vector2Int(3, 2);
            Vector3 center = _grid.GetCellCenterWorld(probe);
            bool inside = _grid.TryWorldToCell(center, out Vector2Int back);
            Debug.Log($"[GridDemo] Celda {probe} -> centro {center} -> celda {back} (dentro={inside})");

            // Prueba del evento de cambio.
            _grid.OnCellChanged += c => Debug.Log($"[GridDemo] Celda cambiada: {c}");
            _grid.SetCell(probe, 5);
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos || settings == null) return;

            // En modo edición aún no existe _grid; construimos una vista ligera para dibujar.
            // (Asigna GC en el editor; es aceptable para una demo, no para producción.)
            Grid<int> preview = _grid ?? settings.CreateGrid<int>();

            Gizmos.color = gizmoColor;

            // Líneas verticales (incluye el borde derecho en x = Width).
            for (int x = 0; x <= preview.Width; x++)
            {
                Vector3 a = preview.CellToWorld(new Vector2Int(x, 0));
                Vector3 b = preview.CellToWorld(new Vector2Int(x, preview.Height));
                Gizmos.DrawLine(a, b);
            }

            // Líneas horizontales (incluye el borde superior en y = Height).
            for (int y = 0; y <= preview.Height; y++)
            {
                Vector3 a = preview.CellToWorld(new Vector2Int(0, y));
                Vector3 b = preview.CellToWorld(new Vector2Int(preview.Width, y));
                Gizmos.DrawLine(a, b);
            }
        }
    }
}