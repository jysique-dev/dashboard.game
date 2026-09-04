using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.GridEngine
{
    /// <summary>
    /// A named rectangular region that stamps a <see cref="CellType"/> into a map.
    ///
    /// Serializable, so a designer can lay out zones in the Inspector: a road, a
    /// building plot, a spawn area. Sub-grids are applied in list order, so a later
    /// one overwrites an earlier one where they overlap. That ordering is the whole
    /// composition model, and it is why they are a plain list and not a set.
    ///
    /// Concrete rather than generic because Unity does not serialize open generic
    /// types. For a payload of your own, copy this class or call
    /// <see cref="GridStamp.Fill{T}"/> directly.
    /// </summary>
    [Serializable]
    public class SubGrid
    {
        [SerializeField] private string label = "Sub Grid";
        [SerializeField] private bool enabled = true;

        [Tooltip("Region in cell coordinates. Parts outside the map are clipped, not an error.")]
        [SerializeField] private GridCoord origin = GridCoord.Zero;
        [SerializeField, Min(1)] private int width = 4;
        [SerializeField, Min(1)] private int height = 4;

        [SerializeField] private CellType cellType = CellType.Ground;

        [Tooltip("Stamp only the perimeter instead of the whole region.")]
        [SerializeField] private bool borderOnly;

        public string Label => label;
        public bool Enabled => enabled;
        public CellType CellType => cellType;
        public bool BorderOnly => borderOnly;

        public SubGrid() { }

        public SubGrid(string label, GridCoord origin, int width, int height, CellType cellType)
        {
            this.label = label;
            this.origin = origin;
            this.width = width;
            this.height = height;
            this.cellType = cellType;
        }

        /// <summary>
        /// Deep copy. Used when saving a scene's sub-grids into an asset: without it the
        /// asset and the scene component would share the same instances, and editing one
        /// would silently change the other.
        /// </summary>
        public SubGrid Clone()
        {
            return new SubGrid(label, origin, width, height, cellType)
            {
                enabled = this.enabled,
                borderOnly = this.borderOnly
            };
        }

        /// <summary>The region this sub-grid covers.</summary>
        public GridBounds Region => new GridBounds(origin, Mathf.Max(1, width), Mathf.Max(1, height));

        /// <summary>Stamps this sub-grid into the map. Returns how many cells were written.</summary>
        public int ApplyTo(IGridMap<CellType> map)
        {
            if (!enabled || map == null) return 0;

            return borderOnly
                ? GridStamp.FillBorder(map, Region, cellType)
                : GridStamp.Fill(map, Region, cellType);
        }

        /// <summary>
        /// Applies a whole list in order, over a base type written first.
        /// Call this again after editing the list; nothing reapplies automatically.
        /// </summary>
        public static int ApplyAll(IReadOnlyList<SubGrid> subGrids, IGridMap<CellType> map, CellType baseType = CellType.Empty)
        {
            if (map == null) return 0;

            int written = GridStamp.Fill(map, map.Bounds, baseType);

            if (subGrids == null) return written;

            for (int i = 0; i < subGrids.Count; i++)
                written += subGrids[i]?.ApplyTo(map) ?? 0;

            return written;
        }
    }
}