using UnityEngine;

namespace LoopEngine.GridEngine
{
    /// <summary>
    /// Resolves world rays into cells by intersecting the layout's infinite plane.
    ///
    /// Deliberately input-agnostic: everything takes a <see cref="Ray"/>. Where that
    /// ray comes from (mouse, touch, gamepad cursor, an AI aiming) is the caller's
    /// problem, so this file compiles with either input backend or none at all.
    /// No colliders and no physics are involved.
    /// </summary>
    public static class GridPicker
    {
        /// <summary>The infinite plane the grid lies on.</summary>
        public static Plane GetPlane(IGridLayout layout)
            => new Plane(layout.PlaneNormal, layout.Origin);

        /// <summary>World point where the ray crosses the grid plane.</summary>
        public static bool RaycastPlane(IGridLayout layout, Ray ray, out Vector3 point)
        {
            point = default;
            if (layout == null) return false;

            Plane plane = GetPlane(layout);

            // Returns false when the ray is parallel to the plane or points away from it.
            if (!plane.Raycast(ray, out float distance) || distance < 0f)
                return false;

            point = ray.GetPoint(distance);
            return true;
        }

        /// <summary>Cell the ray points at, over the infinite plane (no bounds check).</summary>
        public static bool RaycastCell(IGridLayout layout, Ray ray, out GridCoord coord)
        {
            coord = GridCoord.Zero;
            if (!RaycastPlane(layout, ray, out Vector3 point)) return false;

            coord = layout.WorldToCell(point);
            return true;
        }

        /// <summary>
        /// Cell the ray points at, restricted to <paramref name="bounds"/>.
        /// With <paramref name="clampToBounds"/> the nearest in-bounds cell is returned
        /// instead of failing, which is what you usually want while dragging.
        /// </summary>
        public static bool RaycastCell(IGridLayout layout, GridBounds bounds, Ray ray, out GridCoord coord, bool clampToBounds = false)
        {
            if (!RaycastCell(layout, ray, out coord)) return false;

            if (bounds.Contains(coord)) return true;

            if (clampToBounds)
            {
                coord = bounds.Clamp(coord);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Convenience overload for camera-driven picking. The screen point can come
        /// from any input source; this method never reads input itself.
        /// </summary>
        public static bool ScreenPointToCell(IGridLayout layout, Camera camera, Vector3 screenPoint, out GridCoord coord)
        {
            coord = GridCoord.Zero;
            if (camera == null) return false;

            return RaycastCell(layout, camera.ScreenPointToRay(screenPoint), out coord);
        }
    }
}