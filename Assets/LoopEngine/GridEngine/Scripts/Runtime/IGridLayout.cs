using UnityEngine;

namespace LoopEngine.GridEngine
{
    /// <summary>
    /// Contract for converting between discrete cell coordinates and world space.
    /// Implementations decide the plane, the cell shape and the orientation.
    /// Nothing here assumes a camera, a renderer or a data container.
    /// </summary>
    public interface IGridLayout
    {
        /// <summary>Size of one cell along each plane axis, in world units.</summary>
        Vector2 CellSize { get; }

        /// <summary>World position of the layout's (0,0) reference point.</summary>
        Vector3 Origin { get; }

        /// <summary>Normal of the plane the grid lies on, in world space.</summary>
        Vector3 PlaneNormal { get; }

        /// <summary>World position of the cell's minimum corner.</summary>
        Vector3 CellToWorld(GridCoord coord);

        /// <summary>World position of the cell's center.</summary>
        Vector3 CellCenterToWorld(GridCoord coord);

        /// <summary>Cell containing the given world position. The component along the plane normal is ignored.</summary>
        GridCoord WorldToCell(Vector3 worldPosition);

        /// <summary>
        /// Writes the cell's corners, in winding order, into <paramref name="corners"/>.
        /// The array must have room for <see cref="CornerCount"/> entries.
        /// </summary>
        void GetCellCorners(GridCoord coord, Vector3[] corners);

        /// <summary>Number of corners a cell has under this layout.</summary>
        int CornerCount { get; }
    }
}