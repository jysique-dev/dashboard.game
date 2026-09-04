using UnityEngine;

namespace LoopEngine.GridEngine.Tests
{
    /// <summary>
    /// Session 3 visual test. Unlike sessions 1 and 2 this one must be judged in the
    /// GAME view, in Play mode: it proves the grid renders outside the Editor's Gizmos.
    ///
    /// Verifies:
    ///  - Lines and filled cells appear in the Game view and would appear in a build.
    ///  - Per-cell colours come from the vertex stream, so the whole grid is one draw call.
    ///  - Meshes rebuild only on demand: toggling a cell via GridMap.CellChanged marks
    ///    the renderer dirty instead of rebuilding every frame.
    ///  - The probe cell is highlighted on top of the filled cells.
    /// </summary>
    [RequireComponent(typeof(GridRenderer))]
    [AddComponentMenu(LoopRoutes.GridRoute + "/Grid Renderer Visual Test")]
    public class GridRendererVisualTest : MonoBehaviour, IGridCellColorSource
    {
        [Header("Layout")]
        [SerializeField] private PlanarGridLayout layout = new PlanarGridLayout();

        [Header("Bounds")]
        [SerializeField] private GridCoord boundsMin = GridCoord.Zero;
        [SerializeField, Min(1)] private int width = 16;
        [SerializeField, Min(1)] private int height = 12;

        [Header("Probe")]
        [Tooltip("Cell under this transform is highlighted, and is toggled to Blocked while Paint is on.")]
        [SerializeField] private Transform probe;
        [SerializeField] private bool paint;

        [Header("Colors")]
        [SerializeField] private Color filledColor = new Color(0.35f, 0.75f, 0.4f, 0.45f);
        [SerializeField] private Color blockedColor = new Color(0.85f, 0.25f, 0.25f, 0.6f);

        private GridRenderer gridRenderer;
        private GridMap<TestCellState> map;
        private GridCoord lastHighlighted;
        private bool hasHighlight;

        private void Awake() => Rebuild();

        private void OnValidate()
        {
            layout.Rebuild();
            if (isActiveAndEnabled) Rebuild();
        }

        private void Rebuild()
        {
            gridRenderer = GetComponent<GridRenderer>();

            GridBounds bounds = new GridBounds(boundsMin, width, height);
            map = new GridMap<TestCellState>(bounds, TestCellState.Empty);

            // A checker pattern gives an immediate read on whether per-cell colours work.
            foreach (GridCoord coord in bounds)
                map[coord] = ((coord.X + coord.Y) & 1) == 0 ? TestCellState.Filled : TestCellState.Empty;

            // The renderer never polls the map: it rebuilds only when told something changed.
            map.CellChanged += OnCellChanged;

            gridRenderer.Bind(layout, bounds, this);
        }

        private void OnDisable()
        {
            if (map != null) map.CellChanged -= OnCellChanged;
        }

        private void OnCellChanged(GridCoord coord, TestCellState previous, TestCellState current)
            => gridRenderer.SetCellsDirty();

        private void Update()
        {
            if (map == null || gridRenderer == null) return;

            if (probe == null)
            {
                if (hasHighlight)
                {
                    gridRenderer.ClearHighlight();
                    hasHighlight = false;
                }
                return;
            }

            GridCoord cell = layout.WorldToCell(probe.position);
            if (!map.Bounds.Contains(cell))
            {
                if (hasHighlight)
                {
                    gridRenderer.ClearHighlight();
                    hasHighlight = false;
                }
                return;
            }

            if (!hasHighlight || cell != lastHighlighted)
            {
                gridRenderer.SetHighlight(cell);
                lastHighlighted = cell;
                hasHighlight = true;
            }

            // TrySetValue is a no-op when the value already matches, so CellChanged
            // does not fire and no rebuild is queued while the probe sits still.
            if (paint) map.TrySetValue(cell, TestCellState.Blocked);
        }

        public bool TryGetCellColor(GridCoord coord, out Color color)
        {
            TestCellState state = map[coord];
            switch (state)
            {
                case TestCellState.Filled:
                    color = filledColor;
                    return true;
                case TestCellState.Blocked:
                    color = blockedColor;
                    return true;
                default:
                    color = default;
                    return false;
            }
        }
    }
}