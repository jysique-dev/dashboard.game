using System.Collections.Generic;
using UnityEngine;
using LoopEngine.GridEngine.Visualization;

namespace LoopEngine.GridEngine.Demo
{
    /// <summary>
    /// Ejemplo de uso de GridCellHighlighter. No usa input (eso llega en la Sesión 3):
    /// se prueba desde el menú contextual del componente (clic derecho sobre el título
    /// del script en el Inspector) o automáticamente al entrar en Play.
    ///
    /// Requisitos en la escena:
    ///   - Un GameObject con SampleGridHost (+ su GridSettings).
    ///   - Un GridCellHighlighter en el mismo objeto o en un hijo (para que tome la grilla
    ///     vía IGridProvider), con un material Unlit/Transparent asignado.
    ///   - Este componente, con la referencia 'highlighter' asignada.
    /// </summary>
    public class GridHighlighterExample : MonoBehaviour
    {
        [SerializeField] private GridCellHighlighter highlighter;

        [Header("Celda individual")]
        [SerializeField] private Vector2Int singleCell = new Vector2Int(2, 2);
        [SerializeField] private Color singleColor = new Color(0.2f, 0.8f, 1f, 0.5f);

        [Header("Huella rectangular (estilo edificio)")]
        [SerializeField] private Vector2Int footprintOrigin = new Vector2Int(5, 3);
        [SerializeField] private Vector2Int footprintSize = new Vector2Int(3, 2);
        [SerializeField] private Color footprintColor = new Color(0.3f, 1f, 0.4f, 0.5f);

        // Se llama al añadir el componente: intenta autoasignar el highlighter del mismo objeto.
        private void Reset() => highlighter = GetComponent<GridCellHighlighter>();

        private void Start()
        {
            if (highlighter == null) highlighter = GetComponent<GridCellHighlighter>();
            if (highlighter == null)
            {
                Debug.LogError("[GridHighlighterExample] Falta asignar un GridCellHighlighter.", this);
                return;
            }

            // Demostración automática al arrancar.
            ShowFootprint();
        }

        // --- Acciones probables desde el Inspector (clic derecho en el componente) ---

        [ContextMenu("Resaltar celda individual")]
        public void ShowSingle()
        {
            // La forma más simple: una celda con un color.
            highlighter.HighlightCell(singleCell, singleColor);
        }

        [ContextMenu("Resaltar huella rectangular")]
        public void ShowFootprint()
        {
            // Resaltar un conjunto de celdas de una sola vez (reutiliza el pool internamente).
            highlighter.Highlight(RectFootprint(footprintOrigin, footprintSize), footprintColor);
        }

        [ContextMenu("Limpiar")]
        public void ClearAll() => highlighter.Clear();

        /// <summary>
        /// Genera las coordenadas de un rectángulo desde 'origin' hasta origin+size.
        /// Es un adelanto simple de las "huellas" de edificios que formalizaremos en la Sesión 4.
        /// </summary>
        private static IEnumerable<Vector2Int> RectFootprint(Vector2Int origin, Vector2Int size)
        {
            for (int x = 0; x < size.x; x++)
                for (int y = 0; y < size.y; y++)
                    yield return new Vector2Int(origin.x + x, origin.y + y);
        }
    }
}