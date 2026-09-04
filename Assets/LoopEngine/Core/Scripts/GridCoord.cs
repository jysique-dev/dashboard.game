using System;
using UnityEngine;

    /// <summary>
    /// Discrete 2D cell coordinate. X maps to the layout's first plane axis,
    /// Y maps to the layout's second plane axis (world Z by default).
    /// Layout-agnostic: it carries no world-space information.
    /// </summary>
    [Serializable]
    public struct GridCoord : IEquatable<GridCoord>
    {
        [SerializeField] private int x;
        [SerializeField] private int y;

        public int X => x;
        public int Y => y;

        public GridCoord(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public static readonly GridCoord Zero = new GridCoord(0, 0);
        public static readonly GridCoord One = new GridCoord(1, 1);

        public static readonly GridCoord Right = new GridCoord(1, 0);
        public static readonly GridCoord Left = new GridCoord(-1, 0);
        public static readonly GridCoord Forward = new GridCoord(0, 1);
        public static readonly GridCoord Back = new GridCoord(0, -1);

        /// <summary>Right, Forward, Left, Back (clockwise from +X).</summary>
        public static readonly GridCoord[] Cardinals =
        {
            new GridCoord(1, 0),
            new GridCoord(0, 1),
            new GridCoord(-1, 0),
            new GridCoord(0, -1)
        };

        /// <summary>The four diagonal offsets.</summary>
        public static readonly GridCoord[] Diagonals =
        {
            new GridCoord(1, 1),
            new GridCoord(-1, 1),
            new GridCoord(-1, -1),
            new GridCoord(1, -1)
        };

        // --- Arithmetic -----------------------------------------------------

        public static GridCoord operator +(GridCoord a, GridCoord b) => new GridCoord(a.x + b.x, a.y + b.y);
        public static GridCoord operator -(GridCoord a, GridCoord b) => new GridCoord(a.x - b.x, a.y - b.y);
        public static GridCoord operator -(GridCoord a) => new GridCoord(-a.x, -a.y);
        public static GridCoord operator *(GridCoord a, int k) => new GridCoord(a.x * k, a.y * k);
        public static GridCoord operator *(int k, GridCoord a) => new GridCoord(a.x * k, a.y * k);

        // --- Distances ------------------------------------------------------

        /// <summary>Steps required using only cardinal moves.</summary>
        public static int ManhattanDistance(GridCoord a, GridCoord b)
            => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        /// <summary>Steps required when diagonal moves cost the same as cardinal ones.</summary>
        public static int ChebyshevDistance(GridCoord a, GridCoord b)
            => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        // --- Conversions ----------------------------------------------------

        public static implicit operator Vector2Int(GridCoord c) => new Vector2Int(c.x, c.y);
        public static implicit operator GridCoord(Vector2Int v) => new GridCoord(v.x, v.y);

        // --- Equality -------------------------------------------------------

        public bool Equals(GridCoord other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is GridCoord other && Equals(other);
        public override int GetHashCode() => unchecked((x * 397) ^ y);

        public static bool operator ==(GridCoord a, GridCoord b) => a.Equals(b);
        public static bool operator !=(GridCoord a, GridCoord b) => !a.Equals(b);

        public override string ToString() => $"({x}, {y})";
    }
