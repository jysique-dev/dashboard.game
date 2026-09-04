using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.GridEngine
{
    /// <summary>
    /// Serialized configuration for a grid. Lives as an asset so several scenes,
    /// prefabs or systems can share one source of truth, and so designers can tune
    /// it without touching a scene object.
    /// </summary>
    [CreateAssetMenu(fileName = "GridSettings", menuName = LoopRoutes.GridRoute + "/Grid Settings")]
    public class GridSettings : ScriptableObject
    {
        public enum Preset
        {
            /// <summary>Small cells, straight grid. Free movement over a subtle lattice.</summary>
            ActionRPG,
            /// <summary>Medium cells rotated 45 degrees. The classic diamond tactics board.</summary>
            Tactical,
            /// <summary>Large region, straight grid, strong lines for placement feedback.</summary>
            CityBuilder
        }

        [Header("Layout")]
        [SerializeField] private Vector3 origin = Vector3.zero;
        [SerializeField] private Vector2 cellSize = Vector2.one;
        [SerializeField] private float yawDegrees;
        [SerializeField] private PlanarGridLayout.PlaneAxis plane = PlanarGridLayout.PlaneAxis.XZ;

        [Header("Bounds")]
        [SerializeField] private GridCoord boundsMin = GridCoord.Zero;
        [SerializeField, Min(1)] private int width = 16;
        [SerializeField, Min(1)] private int height = 16;

        [Header("Cell content")]
        [Tooltip("Written to every cell before the sub-grids are applied.")]
        [SerializeField] private CellType baseCellType = CellType.Empty;

        [Tooltip("Applied in order. A later entry overwrites an earlier one where they overlap.")]
        [SerializeField] private List<SubGrid> subGrids = new List<SubGrid>();

        [Tooltip("Traversal cost per cell type. Consumed by movement and pathfinding.")]
        [SerializeField] private CellWeightTable weights = new CellWeightTable();

        public Vector3 Origin => origin;
        public Vector2 CellSize => cellSize;
        public float YawDegrees => yawDegrees;
        public PlanarGridLayout.PlaneAxis Plane => plane;

        public GridCoord BoundsMin => boundsMin;
        public int Width => width;
        public int Height => height;

        public CellType BaseCellType => baseCellType;
        public IReadOnlyList<SubGrid> SubGrids => subGrids;
        public CellWeightTable Weights => weights;

        /// <summary>Builds a layout from the current values. Returns a new instance each call.</summary>
        public PlanarGridLayout CreateLayout()
        {
            PlanarGridLayout layout = new PlanarGridLayout(origin, cellSize, yawDegrees, plane);
            layout.Rebuild();
            return layout;
        }

        /// <summary>Builds the region described by these settings.</summary>
        public GridBounds CreateBounds() => new GridBounds(boundsMin, width, height);

        /// <summary>
        /// Overwrites these values with a layout and a region. Used by the "Save Settings"
        /// action so a configuration tuned live in a scene can be persisted to the asset.
        /// Marking the asset dirty is the caller's job.
        /// </summary>
        public void CopyFrom(PlanarGridLayout sourceLayout, GridBounds sourceBounds,
                             IReadOnlyList<SubGrid> sourceSubGrids = null, CellType sourceBaseType = CellType.Empty,
                             CellWeightTable sourceWeights = null)
        {
            if (sourceLayout == null) return;

            origin = sourceLayout.Origin;
            cellSize = sourceLayout.CellSize;
            yawDegrees = sourceLayout.YawDegrees;
            plane = sourceLayout.Plane;

            boundsMin = sourceBounds.Min;
            width = Mathf.Max(1, sourceBounds.Width);
            height = Mathf.Max(1, sourceBounds.Height);

            baseCellType = sourceBaseType;

            if (sourceWeights != null)
                weights = sourceWeights.Clone();

            // Cloned, not referenced: the asset must not share instances with a scene object.
            subGrids.Clear();
            if (sourceSubGrids == null) return;

            for (int i = 0; i < sourceSubGrids.Count; i++)
                if (sourceSubGrids[i] != null)
                    subGrids.Add(sourceSubGrids[i].Clone());
        }

        /// <summary>Overwrites the values with a starting point for a genre. Editor convenience, not a runtime API.</summary>
        public void LoadPreset(Preset preset)
        {
            switch (preset)
            {
                case Preset.ActionRPG:
                    cellSize = new Vector2(0.5f, 0.5f);
                    yawDegrees = 0f;
                    width = 40;
                    height = 40;
                    break;

                case Preset.Tactical:
                    cellSize = new Vector2(1f, 1f);
                    yawDegrees = 45f;
                    width = 16;
                    height = 16;
                    break;

                case Preset.CityBuilder:
                    cellSize = new Vector2(2f, 2f);
                    yawDegrees = 0f;
                    width = 64;
                    height = 64;
                    break;
            }

            plane = PlanarGridLayout.PlaneAxis.XZ;
            boundsMin = GridCoord.Zero;
        }

        private void OnValidate()
        {
            // A zero or negative cell size makes every world conversion undefined.
            cellSize = new Vector2(
                Mathf.Max(0.0001f, Mathf.Abs(cellSize.x)),
                Mathf.Max(0.0001f, Mathf.Abs(cellSize.y)));

            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
        }
    }
}