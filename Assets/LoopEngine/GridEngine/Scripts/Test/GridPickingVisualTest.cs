using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.GridEngine.Tests
{
    /// <summary>
    /// Session 4 visual test. Judge it in the GAME view, in Play mode.
    ///
    /// Verifies:
    ///  - GridPicker resolves a world ray into a cell by intersecting the grid plane,
    ///    with no colliders and no physics.
    ///  - The result stays correct when the layout is rotated (yaw 45) and when the
    ///    camera is orthographic and tilted, which is the isometric case.
    ///  - GridSnapper places the ghost object at the footprint centre, keeping its
    ///    height above the plane.
    ///  - Footprints larger than 1x1 highlight every occupied cell and turn red when
    ///    they do not fit inside the bounds.
    /// </summary>
    [RequireComponent(typeof(GridRenderer))]
    [AddComponentMenu(LoopRoutes.GridRoute + "/Grid Picking Visual Test")]
    public class GridPickingVisualTest : MonoBehaviour
    {
        public enum RaySourceMode
        {
            /// <summary>Uses the Ray Source transform's position and forward axis. Works with any input backend.</summary>
            Transform,
            /// <summary>Uses the mouse through the legacy Input Manager. Falls back to Transform when that backend is off.</summary>
            Mouse
        }

        [Header("Layout")]
        [SerializeField] private PlanarGridLayout layout = new PlanarGridLayout();

        [Header("Bounds")]
        [SerializeField] private GridCoord boundsMin = GridCoord.Zero;
        [SerializeField, Min(1)] private int width = 16;
        [SerializeField, Min(1)] private int height = 12;

        [Header("Ray")]
        [SerializeField] private RaySourceMode raySource = RaySourceMode.Transform;
        [Tooltip("Point this object's blue Z axis at the grid.")]
        [SerializeField] private Transform rayOrigin;
        [SerializeField] private Camera pickingCamera;

        [Header("Footprint")]
        [SerializeField, Min(1)] private int footprintWidth = 1;
        [SerializeField, Min(1)] private int footprintHeight = 1;
        [Tooltip("Optional object moved to the snapped position.")]
        [SerializeField] private Transform ghost;
        [SerializeField] private bool clampToBounds;

        private GridRenderer gridRenderer;
        private GridBounds bounds;
        private readonly List<GridCoord> footprintCells = new List<GridCoord>();

        private bool hasHit;
        private Vector3 hitPoint;
#if !ENABLE_LEGACY_INPUT_MANAGER
        private bool warnedAboutInput;
#endif

        private void OnValidate() => layout.Rebuild();

        private void OnEnable()
        {
            gridRenderer = GetComponent<GridRenderer>();
            bounds = new GridBounds(boundsMin, width, height);
            gridRenderer.Bind(layout, bounds);

            if (pickingCamera == null) pickingCamera = Camera.main;
        }

        private void Update()
        {
            if (gridRenderer == null) return;

            Ray ray = BuildRay();
            hasHit = GridPicker.RaycastPlane(layout, ray, out hitPoint);

            if (!hasHit)
            {
                gridRenderer.ClearHighlight();
                return;
            }

            GridCoord origin = GridSnapper.GetFootprintOrigin(layout, hitPoint, footprintWidth, footprintHeight);

            if (clampToBounds)
            {
                origin = new GridCoord(
                    Mathf.Clamp(origin.X, bounds.Min.X, bounds.Max.X - footprintWidth + 1),
                    Mathf.Clamp(origin.Y, bounds.Min.Y, bounds.Max.Y - footprintHeight + 1));
            }

            bool fits = GridSnapper.IsFootprintInside(bounds, origin, footprintWidth, footprintHeight);
            if (!fits && !clampToBounds)
            {
                gridRenderer.ClearHighlight();
                return;
            }

            GridSnapper.GetFootprintCells(origin, footprintWidth, footprintHeight, footprintCells);
            gridRenderer.SetHighlight(footprintCells);

            if (ghost != null)
            {
                Vector3 center = GridSnapper.GetFootprintCenter(layout, origin, footprintWidth, footprintHeight);
                Vector3 normal = layout.PlaneNormal;
                float currentHeight = Vector3.Dot(ghost.position - layout.Origin, normal);
                float centerHeight = Vector3.Dot(center - layout.Origin, normal);
                ghost.position = center + normal * (currentHeight - centerHeight);
            }
        }

        private Ray BuildRay()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (raySource == RaySourceMode.Mouse && pickingCamera != null)
                return pickingCamera.ScreenPointToRay(Input.mousePosition);
#else
            if (raySource == RaySourceMode.Mouse && !warnedAboutInput)
            {
                warnedAboutInput = true;
                Debug.LogWarning(
                    "[GridPickingVisualTest] Mouse mode needs the legacy Input Manager " +
                    "(Project Settings > Player > Active Input Handling). Falling back to the Transform ray.", this);
            }
#endif
            if (rayOrigin != null)
                return new Ray(rayOrigin.position, rayOrigin.forward);

            return new Ray(transform.position, Vector3.down);
        }

        private void OnDrawGizmos()
        {
            // Scene-view aid only; the actual result is the highlight in the Game view.
            Ray ray = Application.isPlaying ? BuildRay() : new Ray(
                rayOrigin != null ? rayOrigin.position : transform.position,
                rayOrigin != null ? rayOrigin.forward : Vector3.down);

            Gizmos.color = hasHit ? Color.green : Color.red;
            Gizmos.DrawRay(ray.origin, ray.direction * 50f);

            if (!hasHit) return;
            Gizmos.DrawSphere(hitPoint, 0.1f);
        }
    }
}