using UnityEngine;

namespace LoopEngine.GridEngine.Tests
{
    /// <summary>
    /// Session 5 visual test. Judge it in the GAME view, in Play mode.
    ///
    /// Everything here goes through GridSystem only. There is no direct reference to
    /// PlanarGridLayout, GridBounds, GridPicker or GridRenderer, which is the point:
    /// this is what integrating the package into another project looks like.
    /// </summary>
    [AddComponentMenu(LoopRoutes.GridRoute + "/Grid System Visual Test")]
    public class GridSystemVisualTest : MonoBehaviour, IGridCellColorSource
    {
        [SerializeField] private GridSystem grid;

        [Tooltip("Point this object's blue Z axis at the grid.")]
        [SerializeField] private Transform rayOrigin;

        [SerializeField] private bool paint;
        [SerializeField] private Color paintedColor = new Color(0.85f, 0.3f, 0.2f, 0.6f);

        private GridMap<bool> painted;

        private void Start()
        {
            if (grid == null) grid = GetComponent<GridSystem>();
            if (grid == null) return;

            painted = grid.CreateMap<bool>();
            painted.CellChanged += (coord, previous, current) => grid.RefreshCells();

            grid.SetColorSource(this);
        }

        private void Update()
        {
            if (grid == null || painted == null || rayOrigin == null) return;

            Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);

            if (!grid.TryGetCell(ray, out GridCoord coord))
            {
                grid.ClearHighlight();
                return;
            }

            grid.SetHighlight(coord);

            if (paint) painted.TrySetValue(coord, true);
        }

        public bool TryGetCellColor(GridCoord coord, out Color color)
        {
            color = paintedColor;
            return painted != null && painted[coord];
        }
    }
}