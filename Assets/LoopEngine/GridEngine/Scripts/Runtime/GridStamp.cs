
using UnityEngine;

namespace LoopEngine.GridEngine
{
    /// <summary>
    /// Bulk writes over a region of a map. Generic on the cell type, so it works with
    /// <see cref="CellType"/> or with any payload of your own.
    ///
    /// Every write goes through TrySetValue, so a region that sticks out of the map is
    /// clipped instead of throwing. That makes sub-grids safe to move around freely.
    /// </summary>
    public static class GridStamp
    {
        /// <summary>Writes <paramref name="value"/> into every cell of <paramref name="region"/>. Returns how many cells were written.</summary>
        public static int Fill<T>(IGridMap<T> map, GridBounds region, T value)
        {
            if (map == null) return 0;

            int written = 0;
            foreach (GridCoord coord in region)
                if (map.TrySetValue(coord, value)) written++;

            return written;
        }

        /// <summary>Writes only the perimeter of <paramref name="region"/>.</summary>
        public static int FillBorder<T>(IGridMap<T> map, GridBounds region, T value)
        {
            if (map == null || region.IsEmpty) return 0;

            GridCoord min = region.Min;
            GridCoord max = region.Max;
            int written = 0;

            foreach (GridCoord coord in region)
            {
                bool onBorder = coord.X == min.X || coord.X == max.X
                             || coord.Y == min.Y || coord.Y == max.Y;
                if (!onBorder) continue;
                if (map.TrySetValue(coord, value)) written++;
            }

            return written;
        }

        /// <summary>Writes only where the cell currently holds <paramref name="expected"/>. Useful for layering without erasing.</summary>
        public static int FillWhere<T>(IGridMap<T> map, GridBounds region, T expected, T value)
        {
            if (map == null) return 0;

            var comparer = System.Collections.Generic.EqualityComparer<T>.Default;
            int written = 0;

            foreach (GridCoord coord in region)
            {
                if (!map.TryGetValue(coord, out T current)) continue;
                if (!comparer.Equals(current, expected)) continue;
                if (map.TrySetValue(coord, value)) written++;
            }

            return written;
        }

        /// <summary>Region of <paramref name="width"/> x <paramref name="height"/> centred on a cell.</summary>
        public static GridBounds RegionAround(GridCoord center, int width, int height)
        {
            int w = Mathf.Max(1, width);
            int h = Mathf.Max(1, height);
            return new GridBounds(new GridCoord(center.X - (w - 1) / 2, center.Y - (h - 1) / 2), w, h);
        }
    }
}