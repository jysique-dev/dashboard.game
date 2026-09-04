using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.GridEngine
{
    /// <summary>
    /// Rectangular, half-open region of cell coordinates.
    /// Owns everything that depends only on the coordinate space: containment,
    /// linear indexing, iteration and adjacency. It knows nothing about world
    /// space (that is <see cref="IGridLayout"/>) or stored data (that is <see cref="GridMap{T}"/>).
    /// </summary>
    [Serializable]
    public struct GridBounds : IEnumerable<GridCoord>
    {
        /// <summary>Largest buffer any neighbourhood query can fill.</summary>
        public const int MaxNeighbors = 8;

        [SerializeField] private GridCoord min;
        [SerializeField] private int width;
        [SerializeField] private int height;

        public GridBounds(GridCoord min, int width, int height)
        {
            this.min = min;
            this.width = Mathf.Max(0, width);
            this.height = Mathf.Max(0, height);
        }

        public GridBounds(int width, int height) : this(GridCoord.Zero, width, height) { }

        /// <summary>Bounds covering both corners, inclusive.</summary>
        public static GridBounds FromMinMax(GridCoord a, GridCoord b)
        {
            int minX = Mathf.Min(a.X, b.X);
            int minY = Mathf.Min(a.Y, b.Y);
            int maxX = Mathf.Max(a.X, b.X);
            int maxY = Mathf.Max(a.Y, b.Y);
            return new GridBounds(new GridCoord(minX, minY), maxX - minX + 1, maxY - minY + 1);
        }

        public GridCoord Min => min;
        public int Width => width;
        public int Height => height;

        /// <summary>Last coordinate inside the bounds. Undefined when <see cref="IsEmpty"/>.</summary>
        public GridCoord Max => new GridCoord(min.X + width - 1, min.Y + height - 1);

        public int Count => width * height;
        public bool IsEmpty => width <= 0 || height <= 0;

        public bool Contains(GridCoord coord)
            => coord.X >= min.X && coord.X < min.X + width
            && coord.Y >= min.Y && coord.Y < min.Y + height;

        /// <summary>Nearest coordinate inside the bounds.</summary>
        public GridCoord Clamp(GridCoord coord)
        {
            if (IsEmpty) return min;
            return new GridCoord(
                Mathf.Clamp(coord.X, min.X, min.X + width - 1),
                Mathf.Clamp(coord.Y, min.Y, min.Y + height - 1));
        }

        /// <summary>Row-major linear index, or -1 when the coordinate is outside.</summary>
        public int ToIndex(GridCoord coord)
        {
            if (!Contains(coord)) return -1;
            return (coord.Y - min.Y) * width + (coord.X - min.X);
        }

        /// <summary>Inverse of <see cref="ToIndex"/>. Throws when the index is outside the range.</summary>
        public GridCoord FromIndex(int index)
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new GridCoord(min.X + index % width, min.Y + index / width);
        }

        // --- Adjacency ------------------------------------------------------

        /// <summary>
        /// Writes the in-bounds neighbours of <paramref name="coord"/> into
        /// <paramref name="buffer"/> and returns how many were written.
        /// Allocation-free: reuse one buffer of <see cref="MaxNeighbors"/> entries.
        /// Cells on an edge simply yield fewer results.
        /// </summary>
        public int GetNeighbors(GridCoord coord, GridCoord[] buffer, GridNeighborhood neighborhood = GridNeighborhood.Cardinal)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            int count = 0;

            if (neighborhood != GridNeighborhood.Diagonal)
                count = Append(coord, GridCoord.Cardinals, buffer, count);

            if (neighborhood != GridNeighborhood.Cardinal)
                count = Append(coord, GridCoord.Diagonals, buffer, count);

            return count;
        }

        private int Append(GridCoord origin, GridCoord[] offsets, GridCoord[] buffer, int count)
        {
            for (int i = 0; i < offsets.Length; i++)
            {
                GridCoord candidate = origin + offsets[i];
                if (!Contains(candidate)) continue;
                if (count >= buffer.Length)
                    throw new ArgumentException($"Buffer too small; needs up to {MaxNeighbors} entries.", nameof(buffer));
                buffer[count++] = candidate;
            }
            return count;
        }

        // --- Iteration ------------------------------------------------------

        /// <summary>Row-major iteration. Returned as a struct to avoid allocating in foreach.</summary>
        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<GridCoord> IEnumerable<GridCoord>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<GridCoord>
        {
            private readonly GridBounds bounds;
            private int index;

            public Enumerator(GridBounds bounds)
            {
                this.bounds = bounds;
                index = -1;
            }

            public GridCoord Current => bounds.FromIndex(index);
            object IEnumerator.Current => Current;

            public bool MoveNext() => ++index < bounds.Count;
            public void Reset() => index = -1;
            public void Dispose() { }
        }

        public override string ToString() => $"GridBounds(min:{min}, {width}x{height})";
    }
}