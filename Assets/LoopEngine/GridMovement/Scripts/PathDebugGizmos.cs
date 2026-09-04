using UnityEngine;

namespace LoopEngine.GridMovement
{
    /// <summary>
    /// Draws the route a mover is walking, in the Scene view only.
    ///
    /// Gizmos on purpose, and a separate component on purpose. This is authoring and
    /// debugging output: it exists to answer "why did it go that way", never to be seen by
    /// a player. Anything the player should see belongs in a renderer, which is the same
    /// separation the grid engine keeps between GridRenderer and its gizmo test scripts.
    ///
    /// Drop it next to a GridMover and it finds it. It costs nothing in a build, because
    /// OnDrawGizmos is stripped outside the editor.
    /// </summary>
    [RequireComponent(typeof(GridMover))]
    [AddComponentMenu("Grid Movement/Path Debug Gizmos")]
    public class PathDebugGizmos : MonoBehaviour
    {
        [SerializeField] private bool onlyWhenSelected = false;

        [Header("Appearance")]
        [SerializeField] private Color travelledColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
        [SerializeField] private Color remainingColor = new Color(0.2f, 0.9f, 0.4f, 0.9f);
        [SerializeField] private Color destinationColor = new Color(1f, 0.8f, 0.2f, 1f);

        [Tooltip("Radius of the marker drawn on each cell of the route.")]
        [SerializeField, Min(0.01f)] private float nodeRadius = 0.12f;

        [Tooltip("Lifts the drawing off the ground so it is not swallowed by the grid mesh.")]
        [SerializeField] private float heightOffset = 0.05f;

        private GridMover mover;

        private void OnDrawGizmos()
        {
            if (onlyWhenSelected) return;
            Draw();
        }

        private void OnDrawGizmosSelected()
        {
            if (!onlyWhenSelected) return;
            Draw();
        }

        private void Draw()
        {
            if (mover == null) mover = GetComponent<GridMover>();
            if (mover == null || mover.Grid == null) return;

            var path = mover.CurrentPath;
            if (path == null || path.Count < 2) return;

            int index = mover.CurrentPathIndex;

            for (int i = 0; i < path.Count; i++)
            {
                Vector3 position = ToWorld(path[i]);

                // Cells already behind the agent are dimmed rather than hidden: seeing the
                // whole route is what makes a bad path obvious at a glance.
                bool travelled = i <= index;
                bool last = i == path.Count - 1;

                Gizmos.color = last ? destinationColor : travelled ? travelledColor : remainingColor;
                Gizmos.DrawSphere(position, last ? nodeRadius * 1.6f : nodeRadius);

                if (i == 0) continue;

                Gizmos.color = travelled ? travelledColor : remainingColor;
                Gizmos.DrawLine(ToWorld(path[i - 1]), position);
            }
        }

        private Vector3 ToWorld(GridCoord coord)
        {
            Vector3 position = mover.Grid.CoordToWorld(coord);
            position.y += heightOffset;
            return position;
        }
    }
}