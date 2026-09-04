using System.Collections.Generic;
using LoopEngine.GridEngine;
using UnityEngine;

namespace LoopEngine.GridMovement
{
    /// <summary>
    /// Single public entry point for grid movement. Other systems talk to this and never
    /// to the adapter, the neighbour providers or the pathfinders underneath.
    ///
    /// It mirrors the shape of GridSystem deliberately: one component, no live reload, and
    /// an explicit <see cref="ApplySettings"/> that rebuilds everything. Changing the
    /// algorithm or the settings asset during Play does nothing until it is called, which
    /// is the same contract the grid and the camera already follow.
    ///
    /// This is also the only class that wires a pathfinder into a mover. That is what keeps
    /// GridMover free of the search layer while still letting an agent route around a door
    /// that closed in front of it.
    /// </summary>
    [AddComponentMenu("Grid Movement/Grid Movement System")]
    public class GridMovementSystem : MonoBehaviour
    {
        [Header("Grid")]
        [Tooltip("The grid to move on. The only reference this system holds to the grid engine.")]
        [SerializeField] private GridSystem gridSystem;

        [Header("Configuration")]
        [Tooltip("Connectivity rules. Leaving this empty falls back to four-way movement.")]
        [SerializeField] private MovementSettings settings;

        [SerializeField] private PathfindingAlgorithm algorithm = PathfindingAlgorithm.AStar;

        [Tooltip("Auto derives the estimate from the connectivity. Change only to compare algorithms.")]
        [SerializeField] private HeuristicType heuristic = HeuristicType.Auto;

        [Header("Search limits")]
        [Tooltip("Cells a single search may expand. Zero means no limit.")]
        [SerializeField, Min(0)] private int maxExploredNodes = 0;

        [Tooltip("Return the closest reachable cell when the goal cannot be reached.")]
        [SerializeField] private bool allowPartialPaths = true;

        [Header("Heuristic scaling")]
        [Tooltip("Scans the grid for the cheapest walkable cell. Needed only when some cell costs less than 1.")]
        [SerializeField] private bool autoDetectMinCellCost = false;

        [Tooltip("Used when auto detect is off. Lower it by hand if the weight table contains values below 1.")]
        [SerializeField, Min(0.01f)] private float minCellCost = 1f;

        private GridSystemMovementAdapter adapter;
        private INeighborProvider neighbors;
        private IPathfinder pathfinder;
        private DijkstraPathfinder rangeFinder;

        private readonly List<GridMover> movers = new List<GridMover>();

        /// <summary>The grid seen through the movement system's own contract.</summary>
        public IMovementGrid Grid
        {
            get
            {
                if (adapter == null) ApplySettings();
                return adapter;
            }
        }

        /// <summary>The connectivity rules in effect.</summary>
        public INeighborProvider Neighbors
        {
            get
            {
                if (neighbors == null) ApplySettings();
                return neighbors;
            }
        }

        /// <summary>The search in effect.</summary>
        public IPathfinder Pathfinder
        {
            get
            {
                if (pathfinder == null) ApplySettings();
                return pathfinder;
            }
        }

        /// <summary>Movers this system has spawned or been given. Read-only.</summary>
        public IReadOnlyList<GridMover> Movers => movers;

        private void Awake() => ApplySettings();

        /// <summary>
        /// Rebuilds the adapter, the neighbour provider and the pathfinder from the current
        /// inspector values. Not automatic: edit the fields during Play and nothing changes
        /// until this is called.
        /// </summary>
        public void ApplySettings()
        {
            if (gridSystem == null)
            {
                Debug.LogError($"[{nameof(GridMovementSystem)}] No GridSystem assigned. Movement is disabled.", this);
                return;
            }

            adapter = new GridSystemMovementAdapter(gridSystem);
            neighbors = NeighborProviderFactory.Create(settings);

            float scale = autoDetectMinCellCost ? DetectMinCellCost() : minCellCost;

            pathfinder = algorithm == PathfindingAlgorithm.AStar || algorithm == PathfindingAlgorithm.GreedyBestFirst
                ? BuildInformedPathfinder(scale)
                : PathfinderFactory.Create(algorithm);

            rangeFinder = null;

            // Movers spawned before a rebuild are still holding a handler that closes over
            // the old adapter. Re-wiring them here is what makes ApplySettings safe to call
            // mid-game.
            for (int i = movers.Count - 1; i >= 0; i--)
            {
                if (movers[i] == null) movers.RemoveAt(i);
                else Bind(movers[i]);
            }
        }

        private IPathfinder BuildInformedPathfinder(float scale)
        {
            IHeuristic h = HeuristicFactory.Create(heuristic, neighbors, scale);

            return algorithm == PathfindingAlgorithm.AStar
                ? new AStarPathfinder(h)
                : new GreedyBestFirstPathfinder(h);
        }

        /// <summary>
        /// Walks the whole grid looking for the cheapest walkable cell. Only worth doing
        /// when the weight table contains costs below 1, because that is the case where an
        /// unscaled heuristic overestimates and A* stops returning the cheapest path.
        /// </summary>
        private float DetectMinCellCost()
        {
            float min = float.PositiveInfinity;

            foreach (GridCoord coord in adapter.AllCoords())
            {
                float cost = adapter.GetCost(coord);
                if (float.IsInfinity(cost) || cost <= 0f) continue;
                if (cost < min) min = cost;
            }

            return float.IsInfinity(min) ? 1f : min;
        }

        // --- Spawning -------------------------------------------------------

        /// <summary>
        /// Creates an agent on a cell, ready to move.
        ///
        /// The prefab does not need a GridMover: one is added if it is missing, so a plain
        /// visual prefab works. The returned mover is already bound to the grid, placed at
        /// the cell centre and wired for re-pathing.
        ///
        /// Returns null when the cell is not walkable. Spawning an agent inside a wall is
        /// almost never what the caller meant, and letting it through produces an agent
        /// that can never take a step.
        /// </summary>
        public GridMover Spawn(GameObject prefab, GridCoord coord, Transform parent = null)
        {
            if (prefab == null)
            {
                Debug.LogError($"[{nameof(GridMovementSystem)}] Spawn was given no prefab.", this);
                return null;
            }

            if (!Grid.IsWalkable(coord))
            {
                Debug.LogWarning($"[{nameof(GridMovementSystem)}] Cannot spawn at {coord}: the cell is not walkable.", this);
                return null;
            }

            GameObject instance = Instantiate(prefab, Grid.CoordToWorld(coord), Quaternion.identity, parent);

            GridMover mover = instance.GetComponent<GridMover>();
            if (mover == null) mover = instance.AddComponent<GridMover>();

            Register(mover);
            mover.Teleport(coord);

            return mover;
        }

        /// <summary>
        /// Adopts a mover that already exists in the scene: binds the grid, wires re-pathing
        /// and starts tracking it. Placing it on a cell is left to the caller, because a
        /// hand-placed agent usually already sits where it belongs.
        /// </summary>
        public void Register(GridMover mover)
        {
            if (mover == null) return;

            Bind(mover);
            if (!movers.Contains(mover)) movers.Add(mover);
        }

        public void Unregister(GridMover mover)
        {
            if (mover == null) return;

            mover.RepathHandler = null;
            movers.Remove(mover);
        }

        private void Bind(GridMover mover)
        {
            mover.Initialize(Grid);

            // Closing over this system rather than over the current pathfinder, so a later
            // ApplySettings changes the algorithm for movers already in the field.
            mover.RepathHandler = (from, to) => FindPath(from, to);
        }

        // --- Movement -------------------------------------------------------

        /// <summary>
        /// Sends an agent from where it stands to a cell, honouring walkability and cell
        /// weights. Returns false when no usable path exists, in which case the agent does
        /// not move and keeps whatever it was doing.
        /// </summary>
        public bool MoveTo(GridMover mover, GridCoord goal)
        {
            if (mover == null) return false;

            PathResult result = FindPath(mover.CurrentCoord, goal);
            if (!result.HasPath) return false;

            mover.SetPath(result.Path);
            return true;
        }

        /// <summary>Sends an agent to the cell containing a world position.</summary>
        public bool MoveTo(GridMover mover, Vector3 worldPosition)
            => Grid.TryGetCoord(worldPosition, out GridCoord coord) && MoveTo(mover, coord);

        /// <summary>
        /// Runs a search without moving anything. For previewing a route, measuring a
        /// distance, or deciding whether a destination is worth walking to.
        /// </summary>
        public PathResult FindPath(GridCoord from, GridCoord to)
            => Pathfinder.FindPath(Grid, Neighbors, new PathRequest(from, to, maxExploredNodes, allowPartialPaths));

        /// <summary>
        /// Every cell reachable from a starting cell within a cost budget, mapped to what it
        /// costs to get there. This is the movement range of a tactics game.
        ///
        /// Always uses Dijkstra, whatever the configured algorithm is: a range has no goal,
        /// so a heuristic has nothing to point at.
        /// </summary>
        public Dictionary<GridCoord, float> GetReachable(GridCoord from, float maxCost)
        {
            rangeFinder ??= new DijkstraPathfinder();
            return rangeFinder.FindReachable(Grid, Neighbors, from, maxCost);
        }
    }
}