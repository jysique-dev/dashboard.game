using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.GridEngine
{
    /// <summary>
    /// Single public entry point for the grid. Other systems talk to this and never
    /// to the internals, so the pieces underneath can change without breaking them.
    ///
    /// Deliberately not generic: a MonoBehaviour cannot be. Per-cell data lives in a
    /// <see cref="GridMap{T}"/> that your own code owns; use <see cref="CreateMap{T}"/>
    /// so it is created against the right bounds.
    /// </summary>
    [AddComponentMenu("IsoGrid/Grid System")]
    [ExecuteAlways]
    public class GridSystem : MonoBehaviour
    {
        public enum ConfigSource
        {
            /// <summary>Uses the inline values below. Tune them live, then press Save Settings.</summary>
            Inline,
            /// <summary>Reads from the GridSettings asset. Inline values are ignored.</summary>
            SettingsAsset
        }

        [Header("Configuration")]
        [Tooltip("Inline keeps the values below editable so you can see them before saving.")]
        [SerializeField] private ConfigSource source = ConfigSource.Inline;

        [Tooltip("Target asset for Save Settings, and the source when Config Source is SettingsAsset.")]
        [SerializeField] private GridSettings settings;

        [Header("Inline layout")]
        [SerializeField] private PlanarGridLayout inlineLayout = new PlanarGridLayout();
        [SerializeField] private GridCoord inlineBoundsMin = GridCoord.Zero;
        [SerializeField, Min(1)] private int inlineWidth = 16;
        [SerializeField, Min(1)] private int inlineHeight = 16;

        [Header("Inline cell content")]
        [Tooltip("Written to every cell before the sub-grids are applied.")]
        [SerializeField] private CellType inlineBaseCellType = CellType.Empty;

        [Tooltip("Applied in order. A later entry overwrites an earlier one where they overlap.")]
        [SerializeField] private List<SubGrid> inlineSubGrids = new List<SubGrid>();

        [Tooltip("Traversal cost per cell type. Consumed by movement and pathfinding.")]
        [SerializeField] private CellWeightTable inlineWeights = new CellWeightTable();

        [Header("Optional")]
        [Tooltip("Assign to render the grid. Leave empty for a headless grid.")]
        [SerializeField] private GridRenderer gridRenderer;

        private PlanarGridLayout layout;
        private GridBounds bounds;
        private GridMap<CellType> cellTypes;
        private IReadOnlyList<SubGrid> activeSubGrids;
        private CellType activeBaseCellType = CellType.Empty;
        private CellWeightTable activeWeights;

        /// <summary>The sub-grids currently in effect, from the asset or from the inline list.</summary>
        public IReadOnlyList<SubGrid> SubGrids
        {
            get
            {
                if (layout == null) ApplySettings();
                return activeSubGrids;
            }
        }

        /// <summary>Value written to every cell before the sub-grids are stamped.</summary>
        public CellType BaseCellType
        {
            get
            {
                if (layout == null) ApplySettings();
                return activeBaseCellType;
            }
        }

        /// <summary>
        /// Cell types produced by the sub-grids. Read-only on purpose: the map is derived
        /// from the configuration, so edit the sub-grids and call RebuildCellTypes.
        /// For gameplay state that is not part of the layout, use CreateMap instead.
        /// </summary>
        public IReadOnlyGridMap<CellType> CellTypes
        {
            get
            {
                if (cellTypes == null) ApplySettings();
                return cellTypes;
            }
        }

        /// <summary>Coordinate/world conversions. Never null after Awake.</summary>
        public IGridLayout Layout
        {
            get
            {
                if (layout == null) ApplySettings();
                return layout;
            }
        }

        /// <summary>The region the grid covers.</summary>
        public GridBounds Bounds
        {
            get
            {
                if (layout == null) ApplySettings();
                return bounds;
            }
        }

        private void Awake() => ApplySettings();

        private void OnEnable() => ApplySettings();

        private void OnValidate() => ApplySettings();

        /// <summary>
        /// Rebuilds the layout and bounds from the settings asset, or from the inline
        /// values when no asset is assigned. Not automatic: changing the asset during
        /// Play does nothing until this is called.
        /// </summary>
        public void ApplySettings()
        {
            if (source == ConfigSource.SettingsAsset && settings != null)
            {
                layout = settings.CreateLayout();
                bounds = settings.CreateBounds();
                activeSubGrids = settings.SubGrids;
                activeBaseCellType = settings.BaseCellType;
                activeWeights = settings.Weights;
            }
            else
            {
                inlineLayout.Rebuild();
                layout = inlineLayout;
                bounds = new GridBounds(inlineBoundsMin, inlineWidth, inlineHeight);
                activeSubGrids = inlineSubGrids;
                activeBaseCellType = inlineBaseCellType;
                activeWeights = inlineWeights;
            }

            RebuildCellTypes();

            if (gridRenderer != null)
                gridRenderer.Bind(layout, bounds);
        }

        /// <summary>
        /// Rebuilds the cell type map by stamping the active sub-grids over the base type.
        /// Not automatic: editing the list at runtime does nothing until this is called.
        /// </summary>
        public void RebuildCellTypes()
        {
            cellTypes = new GridMap<CellType>(bounds, activeBaseCellType);
            SubGrid.ApplyAll(activeSubGrids, cellTypes, activeBaseCellType);

            if (gridRenderer != null)
                gridRenderer.SetCellsDirty();
        }

        /// <summary>The asset that Save Settings writes to. Null means nothing to save.</summary>
        public GridSettings Settings => settings;

        /// <summary>
        /// Writes the inline values into the referenced asset. Does nothing and returns
        /// false when no asset is assigned. The inline values are always the source, so
        /// what you tuned and looked at in the scene is exactly what gets persisted.
        /// </summary>
        public bool SaveToSettings()
        {
            if (settings == null) return false;

            settings.CopyFrom(
                inlineLayout,
                new GridBounds(inlineBoundsMin, inlineWidth, inlineHeight),
                inlineSubGrids,
                inlineBaseCellType,
                inlineWeights);

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(settings);
            UnityEditor.AssetDatabase.SaveAssets();
#endif
            return true;
        }

        /// <summary>Creates per-cell storage sized to this grid's bounds.</summary>
        public GridMap<T> CreateMap<T>(T defaultValue = default)
            => new GridMap<T>(Bounds, defaultValue);

        /// <summary>Connects a colour source and rebinds the renderer, if there is one.</summary>
        public void SetColorSource(IGridCellColorSource colorSource)
        {
            if (gridRenderer == null) return;
            gridRenderer.Bind(Layout, Bounds, colorSource);
        }

        // --- Queries --------------------------------------------------------

        /// <summary>Cell a world ray points at, restricted to the bounds.</summary>
        public bool TryGetCell(Ray ray, out GridCoord coord, bool clampToBounds = false)
            => GridPicker.RaycastCell(Layout, Bounds, ray, out coord, clampToBounds);

        /// <summary>Cell containing a world position, restricted to the bounds.</summary>
        public bool TryGetCell(Vector3 worldPosition, out GridCoord coord)
        {
            coord = Layout.WorldToCell(worldPosition);
            return Bounds.Contains(coord);
        }

        public Vector3 GetCellCenter(GridCoord coord) => Layout.CellCenterToWorld(coord);

        // --- Weights --------------------------------------------------------

        /// <summary>Cost table in effect. Terrain defaults; per-unit rules belong to the caller.</summary>
        public CellWeightTable Weights
        {
            get
            {
                if (activeWeights == null) ApplySettings();
                return activeWeights;
            }
        }


        public bool TryGetPaletteColor(CellType type, out Color color)
        {
            return Weights.TryGetPaletteColor(type, out color);
        }

        /// <summary>
        /// Traversal cost of a cell. Returns <see cref="Mathf.Infinity"/> for impassable
        /// cells and for cells outside the bounds, so a pathfinder can treat both the same.
        /// </summary>
        public float GetWeight(GridCoord coord)
        {
            if (!CellTypes.TryGetValue(coord, out CellType type)) return Mathf.Infinity;
            return Weights.GetWeight(type);
        }

        /// <summary>True when the cell is inside the bounds and its type is passable.</summary>
        public bool IsPassable(GridCoord coord)
        {
            if (!CellTypes.TryGetValue(coord, out CellType type)) return false;
            return Weights.IsPassable(type);
        }

        /// <summary>
        /// Snapshot of the costs as a map, for a pathfinder that prefers reading floats
        /// over calling back per cell. Regenerate it after RebuildCellTypes.
        /// </summary>
        public GridMap<float> CreateWeightMap()
        {
            GridMap<float> map = new GridMap<float>(Bounds, Mathf.Infinity);

            foreach (GridCoord coord in Bounds)
                map[coord] = GetWeight(coord);

            return map;
        }

        /// <summary>Aligns a world position to the nearest cell centre, keeping its height.</summary>
        public Vector3 Snap(Vector3 worldPosition) => GridSnapper.SnapPreservingHeight(Layout, worldPosition);

        /// <summary>In-bounds neighbours of a cell. Reuse a buffer of GridBounds.MaxNeighbors entries.</summary>
        public int GetNeighbors(GridCoord coord, GridCoord[] buffer, GridNeighborhood neighborhood = GridNeighborhood.Cardinal)
            => Bounds.GetNeighbors(coord, buffer, neighborhood);

        // --- Visual feedback ------------------------------------------------

        public void SetHighlight(GridCoord coord) => gridRenderer?.SetHighlight(coord);

        public void ClearHighlight() => gridRenderer?.ClearHighlight();

        /// <summary>Call after mutating cell data if you are not using GridMap.CellChanged.</summary>
        public void RefreshCells() => gridRenderer?.SetCellsDirty();
    }
}