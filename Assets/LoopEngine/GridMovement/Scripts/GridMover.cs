using LoopEngine.GridMovement.Internal;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.GridMovement
{
    /// <summary>
    /// Walks a GameObject along a path, one cell at a time.
    ///
    /// Knows nothing about GridSystem and nothing about pathfinding. It is given an
    /// <see cref="IMovementGrid"/> to ask where cells are and whether they are still
    /// walkable, and a list of cells to travel. That is the whole dependency surface,
    /// which is what lets the same component serve a player, an enemy and a test dummy.
    ///
    /// Re-pathing is delegated through <see cref="RepathHandler"/> rather than done here.
    /// A mover that owned a pathfinder would drag the whole search layer into every
    /// prefab, and would have to guess which algorithm the game wanted. Session 5 wires
    /// this up from the facade; left unset, a blocked path simply stops and reports.
    ///
    /// Speed and feel are inline fields, not a shared asset: two agents on the same grid
    /// routinely move at different speeds, so this is per-agent data by nature.
    /// </summary>
    [AddComponentMenu("Grid Movement/Grid Mover")]
    public class GridMover : MonoBehaviour, IPathFollower
    {
        [Header("Motion")]
        [Tooltip("Cells per second, measured in world units against the grid's cell size.")]
        [SerializeField, Min(0.01f)] private float speed = 4f;

        [SerializeField] private MovementInterpolation interpolation = MovementInterpolation.Linear;

        [Tooltip("Used only when Interpolation is Curve. Should start at 0 and end at 1.")]
        [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Lifts the agent above the cell centre. For models whose pivot sits at their feet, leave at 0.")]
        [SerializeField] private float heightOffset = 0f;

        [Header("Terrain")]
        [Tooltip("Divides speed by the cost of the cell being entered, so heavy terrain slows the agent visibly.")]
        [SerializeField] private bool speedAffectedByCost = false;

        [Header("Rotation")]
        [Tooltip("Turns the agent to face the direction of travel.")]
        [SerializeField] private bool rotateTowardsMovement = true;

        [Tooltip("Degrees per second. Zero snaps instantly.")]
        [SerializeField, Min(0f)] private float rotationSpeed = 720f;

        [Header("Blocking")]
        [Tooltip("Re-checks the next cell before entering it. Turn off only on a grid that never changes.")]
        [SerializeField] private bool validateNextCell = true;

        // --- State ----------------------------------------------------------

        private IMovementGrid grid;
        private readonly List<GridCoord> path = new List<GridCoord>();

        private int pathIndex;
        private bool isMoving;

        private GridCoord fromCoord;
        private GridCoord toCoord;
        private Vector3 fromPosition;
        private Vector3 toPosition;
        private float stepProgress;
        private float stepDuration;
        private int repathDepth;

        /// <summary>How many times in a row a blocked step may ask for a new route before giving up.</summary>
        private const int MaxRepathDepth = 4;

        public bool IsMoving => isMoving;

        public GridCoord CurrentCoord => fromCoord;

        public GridCoord DestinationCoord => path.Count > 0 ? path[path.Count - 1] : fromCoord;

        /// <summary>
        /// The path being travelled, start cell first. Empty when idle.
        ///
        /// Read-only and exposed for debug drawing and for UI that previews a route. It is
        /// the live list, not a copy, so do not hold onto it across frames.
        /// </summary>
        public IReadOnlyList<GridCoord> CurrentPath => path;

        /// <summary>Index into <see cref="CurrentPath"/> of the cell the agent is leaving.</summary>
        public int CurrentPathIndex => pathIndex;

        /// <summary>How far through the current step the agent is, from 0 to 1.</summary>
        public float StepProgress => stepProgress;

        /// <summary>The grid this mover reads. Null until <see cref="Initialize"/> is called.</summary>
        public IMovementGrid Grid => grid;

        /// <summary>
        /// Asked for a replacement path when the road ahead closes. Receives the cell the
        /// agent is standing on and the cell it was trying to reach, and returns whatever it
        /// finds. Returning a failed result is fine and means "no way through".
        /// </summary>
        public Func<GridCoord, GridCoord, PathResult> RepathHandler;

        public event Action<GridCoord, GridCoord> StepStarted;
        public event Action<GridCoord> StepCompleted;
        public event Action<GridCoord> PathCompleted;
        public event Action<GridCoord> PathBlocked;

        // --- Setup ----------------------------------------------------------

        /// <summary>
        /// Binds the grid. Required before any movement, and safe to call again to move an
        /// agent onto a different grid.
        /// </summary>
        public void Initialize(IMovementGrid movementGrid)
        {
            grid = movementGrid ?? throw new ArgumentNullException(nameof(movementGrid));
        }

        /// <summary>
        /// Places the agent on a cell with no travel and no events. This is the spawn
        /// operation, and the only sanctioned way to change position without a path.
        /// </summary>
        public void Teleport(GridCoord coord)
        {
            if (grid == null) return;

            Stop();
            fromCoord = coord;
            toCoord = coord;
            transform.position = ToWorld(coord);
        }

        // --- IPathFollower --------------------------------------------------

        public void SetPath(IReadOnlyList<GridCoord> newPath)
        {
            path.Clear();
            pathIndex = 0;
            isMoving = false;

            if (grid == null || newPath == null || newPath.Count == 0) return;

            path.AddRange(newPath);

            // The searches all return the start cell as entry zero. Adopting it rather than
            // walking to it means a path handed over while the agent is mid-air from a
            // teleport still lines up.
            fromCoord = path[0];

            // A path of one cell is the "already there" answer. Reporting it as completed
            // keeps callers from waiting on a move that will never happen.
            if (path.Count == 1)
            {
                PathCompleted?.Invoke(fromCoord);
                return;
            }

            isMoving = true;
            BeginStep();
        }

        public void Stop()
        {
            if (!isMoving) return;

            isMoving = false;
            path.Clear();
            pathIndex = 0;
        }

        // --- Loop -----------------------------------------------------------

        private void Update()
        {
            if (!isMoving || grid == null) return;

            stepProgress += stepDuration > 0f ? Time.deltaTime / stepDuration : 1f;
            float eased = Easing.Apply(Mathf.Clamp01(stepProgress), interpolation, curve);

            transform.position = Vector3.Lerp(fromPosition, toPosition, eased);
            ApplyRotation();

            if (stepProgress < 1f) return;

            fromCoord = toCoord;
            transform.position = toPosition;
            StepCompleted?.Invoke(fromCoord);

            pathIndex++;

            if (pathIndex >= path.Count - 1)
            {
                isMoving = false;
                path.Clear();
                pathIndex = 0;
                PathCompleted?.Invoke(fromCoord);
                return;
            }

            BeginStep();
        }

        /// <summary>
        /// Sets up the move into the next cell, re-checking that the cell is still there to
        /// be moved into.
        /// </summary>
        private void BeginStep()
        {
            GridCoord next = path[pathIndex + 1];

            if (validateNextCell && !grid.IsWalkable(next))
            {
                if (!TryRepath(next)) return;

                // TryRepath replaced the path and started a step of its own.
                return;
            }

            fromCoord = path[pathIndex];
            toCoord = next;

            fromPosition = ToWorld(fromCoord);
            toPosition = ToWorld(toCoord);

            float distance = Vector3.Distance(fromPosition, toPosition);
            float effectiveSpeed = speed;

            if (speedAffectedByCost)
            {
                // Cost is a multiplier on effort, so it divides speed. Guarded because a
                // cost of zero would make the step instantaneous and a negative one would
                // run the agent backwards.
                float cost = grid.GetCost(toCoord);
                if (cost > 0f && !float.IsInfinity(cost)) effectiveSpeed = speed / cost;
            }

            stepDuration = interpolation == MovementInterpolation.Instant || effectiveSpeed <= 0f
                ? 0f
                : distance / effectiveSpeed;

            stepProgress = 0f;
            StepStarted?.Invoke(fromCoord, toCoord);
        }

        /// <summary>
        /// Asks for a way around a cell that closed. Returns true when movement continues.
        /// </summary>
        private bool TryRepath(GridCoord blockedCell)
        {
            GridCoord destination = DestinationCoord;

            // A replacement path can be blocked at its own first step, which would send
            // this straight back into SetPath. On a grid that is closing faster than the
            // search can route around it, that recurses until the stack gives out.
            if (repathDepth >= MaxRepathDepth)
            {
                repathDepth = 0;
                isMoving = false;
                path.Clear();
                pathIndex = 0;
                PathBlocked?.Invoke(blockedCell);
                return false;
            }

            if (RepathHandler != null)
            {
                repathDepth++;
                PathResult result = RepathHandler(fromCoord, destination);

                if (result.HasPath && result.Length > 1)
                {
                    SetPath(result.Path);
                    repathDepth = 0;
                    return true;
                }

                repathDepth = 0;
            }

            isMoving = false;
            path.Clear();
            pathIndex = 0;
            PathBlocked?.Invoke(blockedCell);
            return false;
        }

        // --- Helpers --------------------------------------------------------

        private Vector3 ToWorld(GridCoord coord)
        {
            Vector3 position = grid.CoordToWorld(coord);
            position.y += heightOffset;
            return position;
        }

        private void ApplyRotation()
        {
            if (!rotateTowardsMovement) return;

            Vector3 direction = toPosition - fromPosition;
            direction.y = 0f;

            // Below this length the direction vector is numerical noise and would spin the
            // agent randomly.
            if (direction.sqrMagnitude < 0.0001f) return;

            Quaternion target = Quaternion.LookRotation(direction, Vector3.up);

            transform.rotation = rotationSpeed <= 0f
                ? target
                : Quaternion.RotateTowards(transform.rotation, target, rotationSpeed * Time.deltaTime);
        }
    }
}