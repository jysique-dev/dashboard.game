using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LoopEngine.GridEngine.Tests
{
    /// <summary>
    /// Session 1 visual test.
    ///
    /// Draws the layout with Gizmos and verifies both conversion directions:
    ///  - CellToWorld / GetCellCorners build the visible lattice.
    ///  - WorldToCell resolves the cell under the "probe" transform, which is
    ///    highlighted. Drag the probe around the Scene view: the highlight must
    ///    follow it exactly, including when yaw is not zero.
    ///
    /// Gizmos are Editor-only and never render in the Game view or in a build.
    /// Runtime drawing arrives in Session 3.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu(LoopRoutes.GridRoute +"/Grid Layout Visual Test")]
    public class GridLayoutVisualTest : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private PlanarGridLayout layout = new PlanarGridLayout();

        [Header("Extent (cells drawn)")]
        [SerializeField, Min(1)] private int width = 10;
        [SerializeField, Min(1)] private int height = 10;

        [Header("Probe")]
        [Tooltip("Any transform. Its position is fed to WorldToCell and the resulting cell is highlighted.")]
        [SerializeField] private Transform probe;
        [Tooltip("Snaps the probe to the center of its own cell. Useful to eyeball CellCenterToWorld.")]
        [SerializeField] private bool snapProbeToCellCenter;

        [Header("Display")]
        [SerializeField] private Color gridColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color highlightColor = new Color(0.2f, 0.9f, 1f, 1f);
        [SerializeField] private bool drawCoordinateLabels;
        [SerializeField, Min(1)] private int labelStep = 1;

        private Vector3[] lineBuffer;
        private readonly Vector3[] cornerBuffer = new Vector3[4];

        /// <summary>Exposed so other components can reuse the same configured layout.</summary>
        public IGridLayout Layout => layout;

        private void OnValidate()
        {
            layout.Rebuild();
            lineBuffer = null;
        }

        private void Update()
        {
            if (snapProbeToCellCenter && probe != null)
            {
                GridCoord cell = layout.WorldToCell(probe.position);
                probe.position = layout.CellCenterToWorld(cell);
            }
        }

        private void OnDrawGizmos()
        {
            layout.Rebuild();

            DrawLattice();
            DrawProbeCell();
#if UNITY_EDITOR
            DrawLabels();
#endif
        }

        private void DrawLattice()
        {
            int required = ((width + 1) + (height + 1)) * 2;
            if (lineBuffer == null || lineBuffer.Length != required)
                lineBuffer = new Vector3[required];

            int i = 0;

            for (int x = 0; x <= width; x++)
            {
                lineBuffer[i++] = layout.CellToWorld(new GridCoord(x, 0));
                lineBuffer[i++] = layout.CellToWorld(new GridCoord(x, height));
            }

            for (int y = 0; y <= height; y++)
            {
                lineBuffer[i++] = layout.CellToWorld(new GridCoord(0, y));
                lineBuffer[i++] = layout.CellToWorld(new GridCoord(width, y));
            }

            Gizmos.color = gridColor;
            Gizmos.DrawLineList(lineBuffer);
        }

        private void DrawProbeCell()
        {
            if (probe == null) return;

            GridCoord cell = layout.WorldToCell(probe.position);
            layout.GetCellCorners(cell, cornerBuffer);

            Gizmos.color = highlightColor;
            for (int i = 0; i < cornerBuffer.Length; i++)
                Gizmos.DrawLine(cornerBuffer[i], cornerBuffer[(i + 1) % cornerBuffer.Length]);

            Vector3 center = layout.CellCenterToWorld(cell);
            Gizmos.DrawSphere(center, Mathf.Min(layout.CellSize.x, layout.CellSize.y) * 0.12f);
            Gizmos.DrawLine(center, probe.position);
        }

#if UNITY_EDITOR
        private void DrawLabels()
        {
            if (!drawCoordinateLabels) return;

            GUIStyle style = new GUIStyle(EditorStyles.miniLabel);
            style.normal.textColor = gridColor;
            style.alignment = TextAnchor.MiddleCenter;

            for (int x = 0; x < width; x += labelStep)
            {
                for (int y = 0; y < height; y += labelStep)
                {
                    GridCoord coord = new GridCoord(x, y);
                    Handles.Label(layout.CellCenterToWorld(coord), coord.ToString(), style);
                }
            }
        }
#endif
    }
}