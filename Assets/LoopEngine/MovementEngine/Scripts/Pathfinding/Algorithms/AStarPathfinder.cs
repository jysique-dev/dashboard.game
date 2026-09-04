using LoopEngine.GridMovement.Internal;
using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.GridMovement
{
    /// <summary>
    /// A*: Dijkstra plus an estimate of what is left to travel.
    ///
    /// Orders the frontier by cost-so-far plus estimated-cost-remaining, so it expands
    /// cells that lead towards the goal before cells that merely happen to be cheap. On an
    /// open grid it walks almost straight there; behind a wall it degrades gracefully back
    /// towards Dijkstra's behaviour.
    ///
    /// Returns the cheapest path as long as the heuristic never overestimates. Building it
    /// through <see cref="HeuristicFactory"/> is what keeps that true.
    ///
    /// Hart, P. E., Nilsson, N. J. and Raphael, B. (1968), "A Formal Basis for the
    /// Heuristic Determination of Minimum Cost Paths", IEEE Transactions on Systems
    /// Science and Cybernetics 4(2), pp. 100-107.
    /// </summary>
    public sealed class AStarPathfinder : IPathfinder
    {
        private readonly MinHeap<GridCoord> frontier = new MinHeap<GridCoord>(64);
        private readonly Dictionary<GridCoord, GridCoord> cameFrom = new Dictionary<GridCoord, GridCoord>();
        private readonly Dictionary<GridCoord, float> costSoFar = new Dictionary<GridCoord, float>();
        private readonly HashSet<GridCoord> settled = new HashSet<GridCoord>();

        private GridCoord[] neighborBuffer;
        private float[] stepCostBuffer;

        private readonly IHeuristic heuristic;

        /// <summary>
        /// A heuristic may be supplied for full control. Passing null means the heuristic
        /// is derived from the neighbour provider on every call, which is the right default
        /// because the same pathfinder instance can then serve four-way and eight-way
        /// agents without returning wrong answers to one of them.
        /// </summary>
        public AStarPathfinder(IHeuristic heuristic = null) => this.heuristic = heuristic;

        public string Name => heuristic != null ? $"A* ({heuristic.Name})" : "A*";

        public bool RespectsCosts => true;

        public PathResult FindPath(IMovementGrid grid, INeighborProvider neighbors, PathRequest request)
        {
            if (grid == null || neighbors == null) return PathResult.Failure(PathStatus.InvalidStart);
            if (!grid.IsWalkable(request.Start)) return PathResult.Failure(PathStatus.InvalidStart);

            bool goalWalkable = grid.IsWalkable(request.Goal);

            // An unwalkable goal is normally an error, but with AllowPartial it becomes a
            // useful request: walk as close to that wall as you can.
            if (!goalWalkable && !request.AllowPartial) return PathResult.Failure(PathStatus.InvalidGoal);

            if (request.Start.Equals(request.Goal))
                return PathResult.Success(new List<GridCoord> { request.Start }, 0f, 0);

            IHeuristic h = heuristic ?? HeuristicFactory.CreateFor(neighbors);

            EnsureBuffers(neighbors);
            frontier.Clear();
            cameFrom.Clear();
            costSoFar.Clear();
            settled.Clear();

            costSoFar[request.Start] = 0f;
            frontier.Push(request.Start, h.Estimate(request.Start, request.Goal));

            // The best partial answer seen so far: the settled cell whose estimate to the
            // goal is smallest. Tracked continuously rather than searched for at the end,
            // because by the end the frontier has been consumed.
            GridCoord bestCoord = request.Start;
            float bestEstimate = h.Estimate(request.Start, request.Goal);

            int explored = 0;

            while (frontier.Count > 0)
            {
                if (request.HasNodeLimit && explored >= request.MaxExploredNodes)
                    return Give(grid, request, bestCoord, explored, PathStatus.LimitReached);

                GridCoord current = frontier.Pop();

                // Stale duplicate from the heap: this cell was already settled more cheaply.
                if (!settled.Add(current)) continue;
                explored++;

                if (current.Equals(request.Goal))
                {
                    List<GridCoord> path = PathBuilder.Build(cameFrom, request.Start, request.Goal, grid, out _);
                    return PathResult.Success(path, costSoFar[current], explored);
                }

                float estimate = h.Estimate(current, request.Goal);
                if (estimate < bestEstimate)
                {
                    bestEstimate = estimate;
                    bestCoord = current;
                }

                float currentCost = costSoFar[current];
                int count = neighbors.GetNeighbors(grid, current, neighborBuffer, stepCostBuffer);

                for (int i = 0; i < count; i++)
                {
                    GridCoord next = neighborBuffer[i];
                    if (settled.Contains(next)) continue;

                    float cellCost = grid.GetCost(next);
                    if (float.IsInfinity(cellCost)) continue;

                    float newCost = currentCost + cellCost * stepCostBuffer[i];

                    if (costSoFar.TryGetValue(next, out float knownCost) && newCost >= knownCost) continue;

                    costSoFar[next] = newCost;
                    cameFrom[next] = current;
                    frontier.Push(next, newCost + h.Estimate(next, request.Goal));
                }
            }

            return Give(grid, request, bestCoord, explored, PathStatus.NotFound);
        }

        /// <summary>
        /// Produces the partial path to the closest cell reached, or the plain failure when
        /// the caller did not ask for one.
        /// </summary>
        private PathResult Give(IMovementGrid grid, PathRequest request, GridCoord bestCoord, int explored, PathStatus failure)
        {
            if (!request.AllowPartial || bestCoord.Equals(request.Start))
                return PathResult.Failure(failure, explored);

            List<GridCoord> path = PathBuilder.Build(cameFrom, request.Start, bestCoord, grid, out _);
            return PathResult.Partial(path, costSoFar[bestCoord], explored);
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