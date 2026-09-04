using System;
using System.Collections.Generic;

namespace LoopEngine.GridEngine
{
    /// <summary>
    /// Dense per-cell storage over a <see cref="GridBounds"/>, backed by a flat array.
    /// Generic on purpose: the grid does not care whether a cell holds a tile type,
    /// a cost, an occupant reference or a struct of your own.
    /// </summary>
    public class GridMap<T> : IGridMap<T>
    {
        private readonly T[] cells;
        private readonly GridBounds bounds;
        private readonly IEqualityComparer<T> comparer;

        /// <summary>Raised after a cell's value actually changes: (coord, previous, current).</summary>
        public event Action<GridCoord, T, T> CellChanged;

        public GridMap(GridBounds bounds, T defaultValue = default, IEqualityComparer<T> comparer = null)
        {
            this.bounds = bounds;
            this.comparer = comparer ?? EqualityComparer<T>.Default;

            cells = new T[bounds.Count];
            if (!this.comparer.Equals(defaultValue, default))
            {
                for (int i = 0; i < cells.Length; i++)
                    cells[i] = defaultValue;
            }
        }

        public GridBounds Bounds => bounds;

        public T this[GridCoord coord]
        {
            get
            {
                int index = bounds.ToIndex(coord);
                if (index < 0)
                    throw new ArgumentOutOfRangeException(nameof(coord), $"{coord} is outside {bounds}.");
                return cells[index];
            }
            set
            {
                int index = bounds.ToIndex(coord);
                if (index < 0)
                    throw new ArgumentOutOfRangeException(nameof(coord), $"{coord} is outside {bounds}.");
                Write(index, coord, value);
            }
        }

        public bool TryGetValue(GridCoord coord, out T value)
        {
            int index = bounds.ToIndex(coord);
            if (index < 0)
            {
                value = default;
                return false;
            }
            value = cells[index];
            return true;
        }

        public bool TrySetValue(GridCoord coord, T value)
        {
            int index = bounds.ToIndex(coord);
            if (index < 0) return false;
            Write(index, coord, value);
            return true;
        }

        public void Fill(T value)
        {
            foreach (GridCoord coord in bounds)
                Write(bounds.ToIndex(coord), coord, value);
        }

        /// <summary>Resets every cell to <c>default(T)</c>.</summary>
        public void Clear() => Fill(default);

        private void Write(int index, GridCoord coord, T value)
        {
            T previous = cells[index];
            if (comparer.Equals(previous, value)) return;

            cells[index] = value;
            CellChanged?.Invoke(coord, previous, value);
        }
    }
}