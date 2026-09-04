using System.Collections.Generic;
using LoopEngine.GridEngine;
using UnityEngine;

namespace LoopEngine.GridMovement
{
    /// <summary>
    /// A standalone <see cref="IMovementGrid"/> held entirely in memory.
    ///
    /// Two uses. First, tests and tools that need a grid without a scene, a GameObject
    /// or a GridSystem. Second, a snapshot: take the state of a live grid once and let
    /// a long search read from it, so nothing mutates underneath the pathfinder.
    ///
    /// It stores only the cells you write. Anything never written is out of bounds,
    /// which keeps sparse grids cheap and removes the need for a rectangle.
    /// </summary>
    public sealed class MovementMap : IMovementGrid
    {
        private readonly Dictionary<GridCoord, MovementCell> cells;
        private readonly IGridLayout layout;

        /// <summary>
        /// A map with no cells. <paramref name="layout"/> is optional and only powers the
        /// world-space conversions; leave it null for a pure graph with no geometry.
        /// </summary>
        public MovementMap(IGridLayout layout = null, int capacity = 0)
        {
            this.layout = layout;
            cells = new Dictionary<GridCoord, MovementCell>(capacity);
        }

        public int Count => cells.Count;

        /// <summary>Every coordinate present in the map, walkable or not.</summary>
        public IEnumerable<GridCoord> Coords => cells.Keys;

        /// <summary>Adds or overwrites a cell.</summary>
        public void SetCell(GridCoord coord, MovementCell cell) => cells[coord] = cell;

        /// <summary>Removes a cell, which makes it out of bounds again.</summary>
        public bool RemoveCell(GridCoord coord) => cells.Remove(coord);

        public void Clear() => cells.Clear();

        public bool TryGetCell(GridCoord coord, out MovementCell cell) => cells.TryGetValue(coord, out cell);

        public bool IsInBounds(GridCoord coord) => cells.ContainsKey(coord);

        public bool IsWalkable(GridCoord coord)
            => cells.TryGetValue(coord, out MovementCell cell) && cell.IsWalkable;

        public float GetCost(GridCoord coord)
            => cells.TryGetValue(coord, out MovementCell cell) ? cell.Cost : Mathf.Infinity;

        public Vector3 CoordToWorld(GridCoord coord)
            => layout != null ? layout.CellCenterToWorld(coord) : Vector3.zero;

        public bool TryGetCoord(Vector3 worldPosition, out GridCoord coord)
        {
            if (layout == null)
            {
                coord = default;
                return false;
            }

            coord = layout.WorldToCell(worldPosition);
            return IsInBounds(coord);
        }

        /// <summary>
        /// Copies another grid's cells into a new map. The source only has to be able to
        /// list its coordinates, which is why the caller supplies them: IMovementGrid is
        /// deliberately not enumerable, so that adapters over huge or infinite grids stay
        /// legal.
        /// </summary>
        public static MovementMap Snapshot(IMovementGrid source, IEnumerable<GridCoord> coords, IGridLayout layout = null)
        {
            MovementMap map = new MovementMap(layout);
            if (source == null || coords == null) return map;

            foreach (GridCoord coord in coords)
            {
                if (!source.IsInBounds(coord)) continue;
                map.SetCell(coord, source.IsWalkable(coord)
                    ? MovementCell.Walkable(source.GetCost(coord))
                    : MovementCell.Blocked);
            }

            return map;
        }
    }
}