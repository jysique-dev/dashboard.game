using UnityEngine;

namespace LoopEngine.GridEngine.Tests
{
    /// <summary>Cell payload used only by the session 2 visual test.</summary>
    public enum TestCellState
    {
        Empty = 0,
        Filled = 1,
        Blocked = 2
    }

    /// <summary>
    /// Session 2 visual test.
    ///
    /// Verifies, in this order:
    ///  - GridBounds iteration: every cell of the region gets painted by the pattern.
    ///  - GridMap storage: filled cells render solid, empty cells render as outline.
    ///  - GridBounds.GetNeighbors: neighbours of the probe cell are outlined; move the
    ///    probe to a corner and the count must drop (4 -> 2 cardinal, 8 -> 3 for All).
    ///  - GridCoord distance helpers: the radius patterns are driven by them.
    ///
    /// Gizmos are Editor-only and do not render in the Game view or in a build.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu(LoopRoutes.GridRoute + "/Grid Map Visual Test")]
    public class GridMapVisualTest : MonoBehaviour
    {
        public enum Pattern
        {
            None,
            Checker,
            Border,
            ManhattanRadius,
            ChebyshevRadius
        }

        [Header("Layout")]
        [SerializeField] private PlanarGridLayout layout = new PlanarGridLayout();

        [Header("Bounds")]
        [SerializeField] private GridCoord boundsMin = GridCoord.Zero;
        [SerializeField, Min(1)] private int width = 12;
        [SerializeField, Min(1)] private int height = 8;

        [Header("Pattern")]
        [SerializeField] private Pattern pattern = Pattern.Checker;
        [SerializeField, Min(0)] private int radius = 3;

        [Header("Probe")]
        [Tooltip("Its world position is resolved to a cell; that cell and its neighbours are highlighted.")]
        [SerializeField] private Transform probe;
        [SerializeField] private GridNeighborhood neighborhood = GridNeighborhood.Cardinal;

        [Header("Display")]
        [SerializeField, Range(0.1f, 1f)] private float cellFill = 0.9f;
        [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.18f);
        [SerializeField] private Color filledColor = new Color(0.35f, 0.75f, 0.4f, 0.6f);
        [SerializeField] private Color blockedColor = new Color(0.8f, 0.25f, 0.25f, 0.6f);
        [SerializeField] private Color probeColor = new Color(0.2f, 0.9f, 1f, 1f);
        [SerializeField] private Color neighborColor = new Color(1f, 0.85f, 0.2f, 1f);

        private GridMap<TestCellState> map;
        private readonly GridCoord[] neighborBuffer = new GridCoord[GridBounds.MaxNeighbors];
        private readonly Vector3[] cornerBuffer = new Vector3[4];

        public IReadOnlyGridMap<TestCellState> Map => map;
        public IGridLayout Layout => layout;

        private void OnEnable() => Rebuild();

        private void OnValidate()
        {
            layout.Rebuild();
            Rebuild();
        }

        private void Rebuild()
        {
            GridBounds bounds = new GridBounds(boundsMin, width, height);
            map = new GridMap<TestCellState>(bounds, TestCellState.Empty);
            ApplyPattern(bounds);
        }

        private void ApplyPattern(GridBounds bounds)
        {
            GridCoord center = bounds.Clamp(
                probe != null ? layout.WorldToCell(probe.position)
                              : new GridCoord(bounds.Min.X + width / 2, bounds.Min.Y + height / 2));

            foreach (GridCoord coord in bounds)
            {
                TestCellState state = pattern switch
                {
                    Pattern.Checker => ((coord.X + coord.Y) & 1) == 0 ? TestCellState.Filled : TestCellState.Empty,
                    Pattern.Border => coord.X == bounds.Min.X || coord.X == bounds.Max.X
                                   || coord.Y == bounds.Min.Y || coord.Y == bounds.Max.Y
                                      ? TestCellState.Blocked : TestCellState.Empty,
                    Pattern.ManhattanRadius => GridCoord.ManhattanDistance(coord, center) <= radius
                                      ? TestCellState.Filled : TestCellState.Empty,
                    Pattern.ChebyshevRadius => GridCoord.ChebyshevDistance(coord, center) <= radius
                                      ? TestCellState.Filled : TestCellState.Empty,
                    _ => TestCellState.Empty
                };

                map[coord] = state;
            }
        }

        private void Update()
        {
            // The radius patterns follow the probe, so they need refreshing while it moves.
            if (map == null || probe == null) return;

            if (pattern == Pattern.ManhattanRadius || pattern == Pattern.ChebyshevRadius)
                ApplyPattern(map.Bounds);
        }

        private void OnDrawGizmos()
        {
            layout.Rebuild();
            if (map == null) Rebuild();

            Quaternion cellRotation = GetCellRotation();
            Vector3 fillScale = new Vector3(
                layout.CellSize.x * cellFill,
                0.01f,
                layout.CellSize.y * cellFill);

            Matrix4x4 previousMatrix = Gizmos.matrix;

            foreach (GridCoord coord in map.Bounds)
            {
                TestCellState state = map[coord];

                if (state == TestCellState.Empty)
                {
                    DrawCellOutline(coord, emptyColor);
                    continue;
                }

                Gizmos.color = state == TestCellState.Filled ? filledColor : blockedColor;
                Gizmos.matrix = Matrix4x4.TRS(layout.CellCenterToWorld(coord), cellRotation, Vector3.one);
                Gizmos.DrawCube(Vector3.zero, fillScale);
                Gizmos.matrix = previousMatrix;
            }

            DrawProbe();
        }

        private void DrawProbe()
        {
            if (probe == null) return;

            GridCoord cell = layout.WorldToCell(probe.position);
            if (!map.Bounds.Contains(cell)) return;

            int count = map.Bounds.GetNeighbors(cell, neighborBuffer, neighborhood);
            for (int i = 0; i < count; i++)
                DrawCellOutline(neighborBuffer[i], neighborColor);

            DrawCellOutline(cell, probeColor);
            Gizmos.color = probeColor;
            Gizmos.DrawLine(layout.CellCenterToWorld(cell), probe.position);
        }

        private void DrawCellOutline(GridCoord coord, Color color)
        {
            layout.GetCellCorners(coord, cornerBuffer);
            Gizmos.color = color;
            for (int i = 0; i < cornerBuffer.Length; i++)
                Gizmos.DrawLine(cornerBuffer[i], cornerBuffer[(i + 1) % cornerBuffer.Length]);
        }

        /// <summary>
        /// Derives the cell's world orientation from the layout alone, so filled cells
        /// stay aligned when the layout is rotated (yaw 45 for a diamond look).
        /// </summary>
        private Quaternion GetCellRotation()
        {
            Vector3 forward = layout.CellToWorld(new GridCoord(0, 1)) - layout.CellToWorld(GridCoord.Zero);
            if (forward.sqrMagnitude < 1e-8f) return Quaternion.identity;
            return Quaternion.LookRotation(forward.normalized, layout.PlaneNormal);
        }
    }
}