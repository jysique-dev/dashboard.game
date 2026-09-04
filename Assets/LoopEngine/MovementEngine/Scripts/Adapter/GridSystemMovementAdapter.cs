using System;
using System.Collections.Generic;
using LoopEngine.GridEngine;
using UnityEngine;

namespace LoopEngine.GridMovement
{
    /// <summary>
    /// The one and only class in this system that knows GridSystem exists.
    ///
    /// Everything else depends on <see cref="IMovementGrid"/>, so replacing the grid
    /// backend means writing another adapter and nothing more. It holds no copy of the
    /// grid data: every call is forwarded live, so edits to the grid are visible to the
    /// next query. Use <see cref="ToSnapshot"/> when you want the opposite.
    ///
    /// Only the public surface of GridSystem is touched:
    /// Bounds, IsPassable, GetWeight, GetCellCenter and TryGetCell.
    /// </summary>
    public sealed class GridSystemMovementAdapter : IMovementGrid
    {
        private readonly GridSystem grid;

        public GridSystemMovementAdapter(GridSystem grid)
        {
            this.grid = grid != null
                ? grid
                : throw new ArgumentNullException(nameof(grid), "The movement system needs a GridSystem to read from.");
        }

        /// <summary>The wrapped facade, for callers that legitimately need grid-side features.</summary>
        public GridSystem Source => grid;

        public bool IsInBounds(GridCoord coord) => grid.Bounds.Contains(coord);

        /// <summary>
        /// GridSystem.IsPassable already returns false for out-of-bounds cells, so the
        /// bounds check is not repeated here.
        /// </summary>
        public bool IsWalkable(GridCoord coord) => grid.IsPassable(coord);

        /// <summary>
        /// GridSystem.GetWeight already returns Infinity outside the bounds. It does not,
        /// however, promise Infinity for a cell that is merely impassable, so that case is
        /// forced here: the contract of IMovementGrid is that cost and walkability never
        /// disagree.
        /// </summary>
        public float GetCost(GridCoord coord)
        {
            if (!grid.IsPassable(coord)) return Mathf.Infinity;

            float weight = grid.GetWeight(coord);
            return weight > 0f ? weight : 0f;
        }

        public Vector3 CoordToWorld(GridCoord coord) => grid.GetCellCenter(coord);

        public bool TryGetCoord(Vector3 worldPosition, out GridCoord coord)
            => grid.TryGetCell(worldPosition, out coord);

        /// <summary>Every coordinate in the grid. GridBounds is enumerable, so this is a straight pass-through.</summary>
        public IEnumerable<GridCoord> AllCoords()
        {
            foreach (GridCoord coord in grid.Bounds)
                yield return coord;
        }

        /// <summary>
        /// Freezes the current state into a <see cref="MovementMap"/>. Worth doing before a
        /// batch of searches over a grid that other code may be mutating.
        /// </summary>
        public MovementMap ToSnapshot() => MovementMap.Snapshot(this, AllCoords(), grid.Layout);
    }
}