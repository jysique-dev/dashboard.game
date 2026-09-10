
using System;
using UnityEngine;



namespace LoopEngine.GridEngine.VisualDebug
{
    /// <summary>
    /// Visual test for cell types and sub-grids.
    ///
    /// The sub-grids now live on GridSystem (or on its GridSettings asset), not here.
    /// This component only decides how each CellType looks, which is exactly the split
    /// a real project wants: configuration in the system, presentation in the consumer.
    ///
    /// Scene view without Play: the Gizmo preview draws the sub-grids straight from the
    /// configuration. Game view in Play: GridRenderer draws the stamped map.
    /// </summary>
    [AddComponentMenu(LoopRoutes.GridRoute + "/Sub Grid Visual Debug")]
    public class SubGridVisualDebug : MonoBehaviour
    {
        [SerializeField] private GridSystem grid;
        [SerializeField] private SubGridRenderer sub_renderer;


        [Header("Editor preview")]
        [Tooltip("Draws the sub-grids with Gizmos in the Scene view, without entering Play mode.")]
        [SerializeField] private bool previewInEditor = true;
        [SerializeField, Range(0.1f, 1f)] private float previewFill = 0.92f;
        [SerializeField] private bool previewRegionOutlines = true;

        private readonly Vector3[] cornerBuffer = new Vector3[4];

        private void Reset()
        {
            grid = GetComponent<GridSystem>();
            sub_renderer = GetComponent<SubGridRenderer>();
        }

        private void Start()
        {
            if (grid == null) grid = GetComponent<GridSystem>();
            if(sub_renderer == null) sub_renderer = GetComponent<SubGridRenderer>();
        }


        // --- Editor preview -------------------------------------------------

        /// <summary>
        /// Draws the sub-grids straight from the configuration, with no GridRenderer.
        /// Gizmos are Editor-only, so this is what you author with; GridRenderer is what
        /// the player sees.
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!previewInEditor) return;

            if (grid == null) grid = GetComponent<GridSystem>();
            if (grid == null) return;

            if (sub_renderer == null) sub_renderer = GetComponent<SubGridRenderer>();
            if (sub_renderer == null) return;

            DrawGizmos();
        }

        private void DrawGizmos()
        {

            IGridLayout gridLayout = grid.Layout;
            GridBounds gridBounds = grid.Bounds;
            if (gridLayout == null || gridBounds.IsEmpty) return;

            var subGrids = grid.SubGrids;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Quaternion cellRotation = GetCellRotation(gridLayout);
            Vector3 fillScale = new Vector3(
                gridLayout.CellSize.x * previewFill,
                0.01f,
                gridLayout.CellSize.y * previewFill);

            if (previewRegionOutlines)
            {
                Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
                DrawRegionOutline(gridLayout, gridBounds);
            }

            if (subGrids == null)
            {
                Gizmos.matrix = previousMatrix;
                return;
            }

            for (int i = 0; i < subGrids.Count; i++)
            {
                SubGrid subGrid = subGrids[i];
                if (subGrid == null || !subGrid.Enabled) continue;

                if (!grid.TryGetPaletteColor(subGrid.CellType, out Color color))
                    color = new Color(0.6f, 0.6f, 0.6f, 0.4f);

                GridBounds region = subGrid.Region;

                foreach (GridCoord coord in region)
                {
                    // Cells outside the grid are skipped, matching the clipping that
                    // TrySetValue does when the sub-grid is actually stamped.
                    if (!gridBounds.Contains(coord)) continue;
                    if (subGrid.BorderOnly && !IsOnBorder(region, coord)) continue;

                    Gizmos.color = color;
                    Gizmos.matrix = Matrix4x4.TRS(gridLayout.CellCenterToWorld(coord), cellRotation, Vector3.one);
                    Gizmos.DrawCube(Vector3.zero, fillScale);
                }

                Gizmos.matrix = previousMatrix;

                if (previewRegionOutlines)
                {
                    Gizmos.color = new Color(color.r, color.g, color.b, 1f);
                    DrawRegionOutline(gridLayout, region);
                }
            }

            Gizmos.matrix = previousMatrix;
        }

        private static bool IsOnBorder(GridBounds region, GridCoord coord)
        {
            GridCoord min = region.Min;
            GridCoord max = region.Max;
            return coord.X == min.X || coord.X == max.X
                || coord.Y == min.Y || coord.Y == max.Y;
        }

        private void DrawRegionOutline(IGridLayout gridLayout, GridBounds region)
        {
            if (region.IsEmpty) return;

            GridCoord min = region.Min;
            int maxX = min.X + region.Width;
            int maxY = min.Y + region.Height;

            cornerBuffer[0] = gridLayout.CellToWorld(min);
            cornerBuffer[1] = gridLayout.CellToWorld(new GridCoord(maxX, min.Y));
            cornerBuffer[2] = gridLayout.CellToWorld(new GridCoord(maxX, maxY));
            cornerBuffer[3] = gridLayout.CellToWorld(new GridCoord(min.X, maxY));

            for (int i = 0; i < 4; i++)
                Gizmos.DrawLine(cornerBuffer[i], cornerBuffer[(i + 1) % 4]);
        }

        /// <summary>Derives cell orientation from the layout so the preview follows the yaw.</summary>
        private static Quaternion GetCellRotation(IGridLayout gridLayout)
        {
            Vector3 forward = gridLayout.CellToWorld(new GridCoord(0, 1)) - gridLayout.CellToWorld(GridCoord.Zero);
            if (forward.sqrMagnitude < 1e-8f) return Quaternion.identity;
            return Quaternion.LookRotation(forward.normalized, gridLayout.PlaneNormal);
        }
    }
}