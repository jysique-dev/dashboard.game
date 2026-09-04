using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.GridEngine
{
    /// <summary>
    /// Aligns world positions to the grid. Separate from <see cref="GridPicker"/>
    /// because snapping is useful without any ray: dropping an object, placing a
    /// prefab from code, or clamping a moving unit.
    /// </summary>
    public static class GridSnapper
    {
        /// <summary>Center of the cell containing <paramref name="worldPosition"/>, on the grid plane.</summary>
        public static Vector3 SnapToCellCenter(IGridLayout layout, Vector3 worldPosition)
            => layout.CellCenterToWorld(layout.WorldToCell(worldPosition));

        /// <summary>Minimum corner of the cell containing <paramref name="worldPosition"/>.</summary>
        public static Vector3 SnapToCellCorner(IGridLayout layout, Vector3 worldPosition)
            => layout.CellToWorld(layout.WorldToCell(worldPosition));

        /// <summary>
        /// Snaps horizontally while keeping the object's current distance along the
        /// plane normal, so a snapped object does not sink into the ground.
        /// </summary>
        public static Vector3 SnapPreservingHeight(IGridLayout layout, Vector3 worldPosition)
        {
            Vector3 snapped = SnapToCellCenter(layout, worldPosition);
            Vector3 normal = layout.PlaneNormal;
            float height = Vector3.Dot(worldPosition - layout.Origin, normal);
            float snappedHeight = Vector3.Dot(snapped - layout.Origin, normal);
            return snapped + normal * (height - snappedHeight);
        }

        // --- Multi-cell footprints ------------------------------------------

        /// <summary>
        /// Origin (minimum corner cell) of a <paramref name="footprintWidth"/> x
        /// <paramref name="footprintHeight"/> block centred on the cell under
        /// <paramref name="worldPosition"/>. Odd sizes centre exactly; even sizes
        /// bias toward the lower coordinates, which keeps placement predictable.
        /// </summary>
        public static GridCoord GetFootprintOrigin(IGridLayout layout, Vector3 worldPosition, int footprintWidth, int footprintHeight)
        {
            GridCoord center = layout.WorldToCell(worldPosition);
            return new GridCoord(
                center.X - (Mathf.Max(1, footprintWidth) - 1) / 2,
                center.Y - (Mathf.Max(1, footprintHeight) - 1) / 2);
        }

        /// <summary>World centre of a block of cells starting at <paramref name="origin"/>.</summary>
        public static Vector3 GetFootprintCenter(IGridLayout layout, GridCoord origin, int footprintWidth, int footprintHeight)
        {
            int w = Mathf.Max(1, footprintWidth);
            int h = Mathf.Max(1, footprintHeight);

            Vector3 min = layout.CellToWorld(origin);
            Vector3 max = layout.CellToWorld(new GridCoord(origin.X + w, origin.Y + h));
            return (min + max) * 0.5f;
        }

        /// <summary>
        /// Fills <paramref name="results"/> with every cell a footprint occupies.
        /// The list is cleared first; reuse the same list to avoid allocations.
        /// </summary>
        public static void GetFootprintCells(GridCoord origin, int footprintWidth, int footprintHeight, List<GridCoord> results)
        {
            if (results == null) return;
            results.Clear();

            int w = Mathf.Max(1, footprintWidth);
            int h = Mathf.Max(1, footprintHeight);

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    results.Add(new GridCoord(origin.X + x, origin.Y + y));
        }

        /// <summary>True when every cell of the footprint lies inside the bounds.</summary>
        public static bool IsFootprintInside(GridBounds bounds, GridCoord origin, int footprintWidth, int footprintHeight)
        {
            int w = Mathf.Max(1, footprintWidth);
            int h = Mathf.Max(1, footprintHeight);

            return bounds.Contains(origin)
                && bounds.Contains(new GridCoord(origin.X + w - 1, origin.Y + h - 1));
        }
    }
}