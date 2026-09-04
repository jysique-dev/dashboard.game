using System;
using UnityEngine;

namespace LoopEngine.GridEngine
{
    /// <summary>
    /// Quad grid lying on a plane in 3D space, defined by an origin, a cell size
    /// and a yaw rotation around the plane normal.
    ///
    /// This single implementation covers both the "straight" grid and the
    /// diamond-looking isometric grid: a diamond layout is a rectangular layout
    /// rotated 45 degrees. The isometric look belongs to the camera, not the data.
    /// </summary>
    [Serializable]
    public class PlanarGridLayout : IGridLayout
    {
        public enum PlaneAxis
        {
            /// <summary>Cells lie on the XZ plane. Standard for 3D isometric games.</summary>
            XZ,
            /// <summary>Cells lie on the XY plane. Useful for flat 2D-style setups.</summary>
            XY
        }

        [SerializeField] private Vector3 origin = Vector3.zero;
        [SerializeField] private Vector2 cellSize = Vector2.one;
        [SerializeField] private float yawDegrees;
        [SerializeField] private PlaneAxis plane = PlaneAxis.XZ;

        private Matrix4x4 gridToWorld;
        private Matrix4x4 worldToGrid;
        private bool built;

        public PlanarGridLayout() { }

        public PlanarGridLayout(Vector3 origin, Vector2 cellSize, float yawDegrees = 0f, PlaneAxis plane = PlaneAxis.XZ)
        {
            this.origin = origin;
            this.cellSize = cellSize;
            this.yawDegrees = yawDegrees;
            this.plane = plane;
        }

        // --- Configuration --------------------------------------------------

        public Vector3 Origin
        {
            get => origin;
            set { origin = value; built = false; }
        }

        public Vector2 CellSize
        {
            get => cellSize;
            set
            {
                // A zero or negative cell size makes every conversion undefined.
                cellSize = new Vector2(
                    Mathf.Max(0.0001f, Mathf.Abs(value.x)),
                    Mathf.Max(0.0001f, Mathf.Abs(value.y)));
                built = false;
            }
        }

        public float YawDegrees
        {
            get => yawDegrees;
            set { yawDegrees = value; built = false; }
        }

        public PlaneAxis Plane
        {
            get => plane;
            set { plane = value; built = false; }
        }

        /// <summary>Recomputes the cached matrices. Call after editing serialized fields directly (e.g. in OnValidate).</summary>
        public void Rebuild()
        {
            CellSize = cellSize; // clamps and keeps the value consistent
            Quaternion rotation = Quaternion.AngleAxis(yawDegrees, PlaneNormalFor(plane));
            gridToWorld = Matrix4x4.TRS(origin, rotation, Vector3.one);
            worldToGrid = gridToWorld.inverse;
            built = true;
        }

        private void EnsureBuilt()
        {
            if (!built) Rebuild();
        }

        private static Vector3 PlaneNormalFor(PlaneAxis axis)
            => axis == PlaneAxis.XZ ? Vector3.up : Vector3.back;

        public Vector3 PlaneNormal
        {
            get
            {
                EnsureBuilt();
                return gridToWorld.MultiplyVector(PlaneNormalFor(plane)).normalized;
            }
        }

        public int CornerCount => 4;

        // --- Conversions ----------------------------------------------------

        /// <summary>Maps grid-local (u, v) offsets onto the layout plane.</summary>
        private Vector3 ToPlane(float u, float v)
            => plane == PlaneAxis.XZ ? new Vector3(u, 0f, v) : new Vector3(u, v, 0f);

        /// <summary>Extracts the (u, v) offsets from a grid-local point.</summary>
        private Vector2 FromPlane(Vector3 local)
            => plane == PlaneAxis.XZ ? new Vector2(local.x, local.z) : new Vector2(local.x, local.y);

        public Vector3 CellToWorld(GridCoord coord)
        {
            EnsureBuilt();
            return gridToWorld.MultiplyPoint3x4(ToPlane(coord.X * cellSize.x, coord.Y * cellSize.y));
        }

        public Vector3 CellCenterToWorld(GridCoord coord)
        {
            EnsureBuilt();
            return gridToWorld.MultiplyPoint3x4(ToPlane(
                (coord.X + 0.5f) * cellSize.x,
                (coord.Y + 0.5f) * cellSize.y));
        }

        public GridCoord WorldToCell(Vector3 worldPosition)
        {
            EnsureBuilt();
            Vector2 uv = FromPlane(worldToGrid.MultiplyPoint3x4(worldPosition));
            return new GridCoord(
                Mathf.FloorToInt(uv.x / cellSize.x),
                Mathf.FloorToInt(uv.y / cellSize.y));
        }

        public void GetCellCorners(GridCoord coord, Vector3[] corners)
        {
            if (corners == null) throw new ArgumentNullException(nameof(corners));
            if (corners.Length < CornerCount)
                throw new ArgumentException($"Array needs at least {CornerCount} entries.", nameof(corners));

            EnsureBuilt();

            float u0 = coord.X * cellSize.x;
            float v0 = coord.Y * cellSize.y;
            float u1 = u0 + cellSize.x;
            float v1 = v0 + cellSize.y;

            corners[0] = gridToWorld.MultiplyPoint3x4(ToPlane(u0, v0));
            corners[1] = gridToWorld.MultiplyPoint3x4(ToPlane(u1, v0));
            corners[2] = gridToWorld.MultiplyPoint3x4(ToPlane(u1, v1));
            corners[3] = gridToWorld.MultiplyPoint3x4(ToPlane(u0, v1));
        }
    }
}