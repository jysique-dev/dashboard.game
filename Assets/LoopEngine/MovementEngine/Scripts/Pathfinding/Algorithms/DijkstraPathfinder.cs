using LoopEngine.GridMovement.Internal;
using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.GridMovement
{
    /// <summary>
    /// Dijkstra's algorithm: the cheapest path, with terrain weights honoured.
    ///
    /// Always expands the cheapest cell known so far, so the first time it settles the
    /// goal it has the optimal cost. It has no idea where the goal is, so it spreads in
    /// every direction equally; on an open grid that is a lot of wasted expansion, which
    /// is precisely the problem A* solves in session 3.
    ///
    /// Still the right tool when there is no usable heuristic, when several goals are
    /// being compared, or when you want a reference answer to check a faster search
    /// against.
    ///
    /// Dijkstra, E. W. (1959), "A note on two problems in connexion with graphs",
    /// Numerische Mathematik 1, pp. 269-271.
    ///
    /// One requirement inherited from the paper: costs must not be negative.
    /// <see cref="MovementCell"/> clamps them, and the adapter floors GridSystem weights
    /// at zero, so the grid cannot hand this search a case it would answer wrongly.
    /// </summary>
    public sealed class DijkstraPathfinder : IPathfinder
    {
        private readonly MinHeap<GridCoord> frontier = new MinHeap<GridCoord>(64);
        private readonly Dictionary<GridCoord, GridCoord> cameFrom = new Dictionary<GridCoord, GridCoord>();
        private readonly Dictionary<GridCoord, float> costSoFar = new Dictionary<GridCoord, float>();
        private readonly HashSet<GridCoord> settled = new HashSet<GridCoord>();

        private GridCoord[] neighborBuffer;
        private float[] stepCostBuffer;

        public string Name => "Dijkstra";

        public bool RespectsCosts => true;

        public PathResult FindPath(IMovementGrid grid, INeighborProvider neighbors, PathRequest request)
        {
            if (grid == null || neighbors == null) return PathResult.Failure(PathStatus.InvalidStart);
            if (!grid.IsWalkable(request.Start)) return PathResult.Failure(PathStatus.InvalidStart);
            if (!grid.IsWalkable(request.Goal)) return PathResult.Failure(PathStatus.InvalidGoal);

            if (request.Start.Equals(request.Goal))
                return PathResult.Success(new List<GridCoord> { request.Start }, 0f, 0);

            EnsureBuffers(neighbors);
            frontier.Clear();
            cameFrom.Clear();
            costSoFar.Clear();
            settled.Clear();

            costSoFar[request.Start] = 0f;
            frontier.Push(request.Start, 0f);
            int explored = 0;

            while (frontier.Count > 0)
            {
                if (request.HasNodeLimit && explored >= request.MaxExploredNodes)
                    return PathResult.Failure(PathStatus.LimitReached, explored);

                GridCoord current = frontier.Pop();

                // The heap holds duplicates instead of doing a decrease-key, so a cell can
                // surface again after it was already settled at a lower cost. Skipping it
                // here is what makes that trade safe.
                if (!settled.Add(current)) continue;
                explored++;

                if (current.Equals(request.Goal))
                {
                    List<GridCoord> path = PathBuilder.Build(cameFrom, request.Start, request.Goal, grid, out _);
                    return PathResult.Success(path, costSoFar[current], explored);
                }

                float currentCost = costSoFar[current];
                int count = neighbors.GetNeighbors(grid, current, neighborBuffer, stepCostBuffer);

                for (int i = 0; i < count; i++)
                {
                    GridCoord next = neighborBuffer[i];
                    if (settled.Contains(next)) continue;

                    float cellCost = grid.GetCost(next);
                    if (float.IsInfinity(cellCost)) continue;

                    // Cost of the move and cost of the destination are two different things.
                    // The step multiplier is geometry (a diagonal is longer); the cell cost is
                    // terrain. Multiplying them keeps a diagonal through mud more expensive
                    // than a diagonal through grass, which adding them would not.
                    float stepCost = cellCost * stepCostBuffer[i];
                    float newCost = currentCost + stepCost;

                    if (costSoFar.TryGetValue(next, out float knownCost) && newCost >= knownCost) continue;

                    costSoFar[next] = newCost;
                    cameFrom[next] = current;
                    frontier.Push(next, newCost);
                }
            }

            return PathResult.Failure(PathStatus.NotFound, explored);
        }

        /// <summary>
        /// Every cell reachable from the start within a cost budget, mapped to what it
        /// costs to get there. This is a Dijkstra flood without a goal, and it is what
        /// draws a movement range in a tactics game.
        ///
        /// Handed out here rather than as its own class because it is the same search with
        /// the termination condition removed.
        /// </summary>
        public Dictionary<GridCoord, float> FindReachable(
            IMovementGrid grid, INeighborProvider neighbors, GridCoord start, float maxCost)
        {
            Dictionary<GridCoord, float> reachable = new Dictionary<GridCoord, float>();
            if (grid == null || neighbors == null || !grid.IsWalkable(start)) return reachable;

            EnsureBuffers(neighbors);
            frontier.Clear();
            settled.Clear();

            reachable[start] = 0f;
            frontier.Push(start, 0f);

            while (frontier.Count > 0)
            {
                GridCoord current = frontier.Pop();
                if (!settled.Add(current)) continue;

                float currentCost = reachable[current];
                int count = neighbors.GetNeighbors(grid, current, neighborBuffer, stepCostBuffer);

                for (int i = 0; i < count; i++)
                {
                    GridCoord next = neighborBuffer[i];
                    if (settled.Contains(next)) continue;

                    float cellCost = grid.GetCost(next);
                    if (float.IsInfinity(cellCost)) continue;

                    float newCost = currentCost + cellCost * stepCostBuffer[i];
                    if (newCost > maxCost) continue;

                    if (reachable.TryGetValue(next, out float knownCost) && newCost >= knownCost) continue;

                    reachable[next] = newCost;
                    frontier.Push(next, newCost);
                }
            }

            return reachable;
        }

        private void EnsureBuffers(INeighborProvider neighbors)
        {
            int size = Mathf.Max(1, neighbors.MaxNeighbors);

            if (neighborBuffer == null || neighborBuffer.Length < size)
                neighborBuffer = new GridCoord[size];

            if (stepCostBuffer == null || stepCostBuffer.Length < size)
                stepCostBuffer = new float[size];
        }
    }
}