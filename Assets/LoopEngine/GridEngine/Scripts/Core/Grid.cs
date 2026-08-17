using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.GridEngine
{
    /// <summary>
    /// Grilla genérica y bidimensional. Es C# puro: NO hereda de MonoBehaviour ni
    /// depende de la escena, por lo que puede reutilizarse desde cualquier sistema
    /// (colocación, selección, movimiento, herramientas de editor, tests, etc.).
    ///
    /// TCell es el dato que guarda cada celda. Puede ser:
    ///   - un valor:  Grid&lt;int&gt;, Grid&lt;bool&gt;   (mapas de calor, ocupación simple)
    ///   - un objeto: Grid&lt;MiCelda&gt;              (celda con estado propio de dominio)
    ///
    /// La grilla solo conoce coordenadas (Vector2Int) y su mapeo al mundo.
    /// No sabe nada de render, input ni edificios: eso vive en sesiones posteriores
    /// y se conecta a través del evento OnCellChanged.
    /// </summary>
    public class Grid<TCell> : IReadOnlyGrid
    {
        public int Width { get; }
        public int Height { get; }
        public float CellSize { get; }
        public Vector3 Origin { get; }
        public GridPlane Plane { get; }

        private readonly TCell[,] _cells;

        /// <summary>
        /// Se dispara cuando el contenido de una celda cambia. Entrega la coordenada afectada.
        /// Los sistemas de visualización u otros consumidores se suscriben aquí en lugar de
        /// que la grilla los conozca directamente (desacople por eventos).
        /// </summary>
        public event Action<Vector2Int> OnCellChanged;

        /// <param name="createCell">
        /// Fábrica opcional para inicializar cada celda. Recibe la grilla y la coordenada.
        /// Si es null, las celdas quedan en default(TCell).
        /// </param>
        public Grid(
            int width,
            int height,
            float cellSize,
            Vector3 origin,
            GridPlane plane,
            Func<Grid<TCell>, Vector2Int, TCell> createCell = null)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));

            Width = width;
            Height = height;
            CellSize = cellSize;
            Origin = origin;
            Plane = plane;

            _cells = new TCell[width, height];

            if (createCell != null)
            {
                for (int x = 0; x < width; x++)
                    for (int y = 0; y < height; y++)
                        _cells[x, y] = createCell(this, new Vector2Int(x, y));
            }
        }

        // ------------------------------------------------------------------
        // Conversión de coordenadas
        // ------------------------------------------------------------------

        /// <summary>Esquina inferior-izquierda de la celda, en coordenadas del mundo.</summary>
        public Vector3 CellToWorld(Vector2Int cell)
            => Origin + GridToLocalOffset(cell.x, cell.y);

        /// <summary>Centro de la celda en el mundo (lo habitual para instanciar objetos encima).</summary>
        public Vector3 GetCellCenterWorld(Vector2Int cell)
            => Origin + GridToLocalOffset(cell.x + 0.5f, cell.y + 0.5f);

        /// <summary>
        /// Convierte una posición del mundo a coordenada de celda.
        /// Devuelve false (y una celda posiblemente fuera de rango) si el punto cae fuera de la grilla.
        /// </summary>
        public bool TryWorldToCell(Vector3 world, out Vector2Int cell)
        {
            cell = LocalOffsetToGrid(world - Origin);
            return IsInside(cell);
        }

        /// <summary>Igual que TryWorldToCell pero recorta el resultado a los límites de la grilla.</summary>
        public Vector2Int WorldToCellClamped(Vector3 world)
        {
            Vector2Int cell = LocalOffsetToGrid(world - Origin);
            cell.x = Mathf.Clamp(cell.x, 0, Width - 1);
            cell.y = Mathf.Clamp(cell.y, 0, Height - 1);
            return cell;
        }

        // ------------------------------------------------------------------
        // Acceso a celdas
        // ------------------------------------------------------------------

        public bool IsInside(Vector2Int cell)
            => cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;

        /// <summary>Lee una celda. Lanza excepción si está fuera de rango (usa TryGetCell si no lo sabes).</summary>
        public TCell GetCell(Vector2Int cell)
        {
            if (!IsInside(cell))
                throw new ArgumentOutOfRangeException(
                    nameof(cell), $"Celda {cell} fuera de la grilla {Width}x{Height}.");
            return _cells[cell.x, cell.y];
        }

        public bool TryGetCell(Vector2Int cell, out TCell value)
        {
            if (IsInside(cell))
            {
                value = _cells[cell.x, cell.y];
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>Reemplaza el contenido de una celda y notifica el cambio. Ignora coordenadas fuera de rango.</summary>
        public void SetCell(Vector2Int cell, TCell value)
        {
            if (!IsInside(cell)) return;
            _cells[cell.x, cell.y] = value;
            OnCellChanged?.Invoke(cell);
        }

        /// <summary>
        /// Notifica manualmente que una celda cambió sin reemplazar su referencia.
        /// Útil cuando TCell es un objeto mutable que se modifica desde fuera y luego
        /// quiere avisar a los suscriptores.
        /// </summary>
        public void NotifyCellChanged(Vector2Int cell)
        {
            if (IsInside(cell)) OnCellChanged?.Invoke(cell);
        }

        /// <summary>Recorre todas las coordenadas válidas (útil para inicializar o dibujar).</summary>
        public IEnumerable<Vector2Int> AllCoordinates()
        {
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    yield return new Vector2Int(x, y);
        }

        // ------------------------------------------------------------------
        // Helpers de plano (único punto donde importa XZ vs XY)
        // ------------------------------------------------------------------

        private Vector3 GridToLocalOffset(float gx, float gy)
            => Plane == GridPlane.XZ
                ? new Vector3(gx * CellSize, 0f, gy * CellSize)
                : new Vector3(gx * CellSize, gy * CellSize, 0f);

        private Vector2Int LocalOffsetToGrid(Vector3 local)
        {
            float u = local.x / CellSize;
            float v = (Plane == GridPlane.XZ ? local.z : local.y) / CellSize;
            return new Vector2Int(Mathf.FloorToInt(u), Mathf.FloorToInt(v));
        }
    }
}