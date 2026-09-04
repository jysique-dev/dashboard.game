using UnityEngine;

namespace LoopEngine.GridMovement
{
    /// <summary>
    /// Everything the movement system needs to know about a grid, and nothing else.
    ///
    /// Pathfinders, neighbour providers and movers depend only on this interface, so
    /// they can run against a GridSystem, against an in-memory map built by a test, or
    /// against any future grid backend without a single line changing.
    ///
    /// GridCoord is reused on purpose: it is the coordinate vocabulary of the project.
    /// Introducing a parallel coordinate type would force a conversion at every call
    /// site and buy nothing, since GridCoord is a plain value type with no behaviour
    /// tied to the grid engine's internals.
    /// </summary>
    public interface IMovementGrid
    {
        /// <summary>True when the coordinate exists in this grid at all.</summary>
        bool IsInBounds(GridCoord coord);

        /// <summary>
        /// True when an agent is allowed to stand on this cell. Out-of-bounds
        /// coordinates always return false, so callers never need two checks.
        /// </summary>
        bool IsWalkable(GridCoord coord);

        /// <summary>
        /// Cost of entering this cell. Returns <see cref="Mathf.Infinity"/> for cells
        /// that are impassable or outside the grid, which lets a pathfinder discard
        /// both cases with the same comparison.
        /// </summary>
        float GetCost(GridCoord coord);

        /// <summary>World position of the cell centre. Used by movers, not by the search.</summary>
        Vector3 CoordToWorld(GridCoord coord);

        /// <summary>Cell containing a world position. False when it falls outside the grid.</summary>
        bool TryGetCoord(Vector3 worldPosition, out GridCoord coord);
    }
}