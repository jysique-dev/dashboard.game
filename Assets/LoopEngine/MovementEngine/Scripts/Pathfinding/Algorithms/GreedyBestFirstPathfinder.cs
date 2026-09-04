using LoopEngine.GridMovement.Internal;
using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.GridMovement
{
    /// <summary>
    /// Greedy best-first search: always expands whichever cell looks closest to the goal,
    /// and never looks at what the journey has cost so far.
    ///
    /// That single omission is the whole trade. On open terrain it reaches the goal after
    /// expanding a fraction of what A* touches. In exchange, the path it returns is not
    /// the cheapest one and can be visibly worse: it will happily lead an agent straight
    /// into a dead end, back out, and around, because at every moment the dead end looked
    /// like progress.
    ///
    /// Worth having for cases where "a path, now" beats "the best path": a crowd of
    /// background agents, a first guess that a slower search refines later, or anything
    /// where the player will not measure the route. Also the clearest way to see, side by
    /// side with A*, what the cost-so-far term is actually buying.
    ///
    /// The name and the technique are older than A*; the framing used here follows the
    /// treatment of best-first search in Russell and Norvig, "Artificial Intelligence: A
    /// Modern Approach", chapter on informed search.
    /// </summary>
    public sealed class GreedyBestFirstPathfinder : IPathfinder
    {
        private readonly MinHeap<GridCoord> frontier = new MinHeap<GridCoord>(64);
        private readonly Dictionary<GridCoord, GridCoord> cameFrom = new Dictionary<GridCoord, GridCoord>();
        private readonly HashSet<GridCoord> visited = new HashSet<GridCoord>();

        private GridCoord[] neighborBuffer;
        private float[] stepCostBuffer;

        private readonly IHeuristic heuristic;

        public GreedyBestFirstPathfinder(IHeuristic heuristic = null) => this.heuristic = heuristic;

        public string Name => "Greedy Best-First";

        /// <summary>
        /// False, and not for the same reason as BFS. This search does read cell costs, in
        /// the sense that it refuses impassable cells, but it never compares them, so a
        /// caller expecting the cheap route will not get it.
        /// </summary>
        public bool RespectsCosts => false;

        public PathResult FindPath(IMovementGrid grid, INeighborProvider neighbors, PathRequest request)
        {
            if (grid == null || neighbors == null) return PathResult.Failure(PathStatus.InvalidStart);
            if (!grid.IsWalkable(request.Start)) return PathResult.Failure(PathStatus.InvalidStart);

            bool goalWalkable = grid.IsWalkable(request.Goal);
            if (!goalWalkable && !request.AllowPartial) return PathResult.Failure(PathStatus.InvalidGoal);

            if (request.Start.Equals(request.Goal))
                return PathResult.Success(new List<GridCoord> { request.Start }, 0f, 0);

            IHeuristic h = heuristic ?? HeuristicFactory.CreateFor(neighbors);

            EnsureBuffers(neighbors);
            frontier.Clear();
            cameFrom.Clear();
            visited.Clear();

            frontier.Push(request.Start, h.Estimate(request.Start, request.Goal));
            visited.Add(request.Start);

            GridCoord bestCoord = request.Start;
            float bestEstimate = h.Estimate(request.Start, request.Goal);

            int explored = 0;

            while (frontier.Count > 0)
            {
                if (request.HasNodeLimit && explored >= request.MaxExploredNodes)
                    return Give(grid, request, bestCoord, explored, PathStatus.LimitReached);

                GridCoord current = frontier.Pop();
                explored++;

                if (current.Equals(request.Goal))
                {
                    List<GridCoord> path = PathBuilder.Build(cameFrom, request.Start, request.Goal, grid, out float cost);
                    return PathResult.Success(path, cost, explored);
                }

                float estimate = h.Estimate(current, request.Goal);
                if (estimate < bestEstimate)
                {
                    bestEstimate = estimate;
                    bestCoord = current;
                }

                int count = neighbors.GetNeighbors(grid, current, neighborBuffer, stepCostBuffer);

                for (int i = 0; i < count; i++)
                {
                    GridCoord next = neighborBuffer[i];

                    // A cell is claimed the first time it is reached and never revisited.
                    // With no cost-so-far term there is nothing to improve on a second visit,
                    // so the usual reopening logic would only add work.
                    if (!visited.Add(next)) continue;
                    if (float.IsInfinity(grid.GetCost(next))) continue;

                    cameFrom[next] = current;
                    frontier.Push(next, h.Estimate(next, request.Goal));
                }
            }

            return Give(grid, request, bestCoord, explored, PathStatus.NotFound);
        }

        private PathResult Give(IMovementGrid grid, PathRequest request, GridCoord bestCoord, int explored, PathStatus failure)
        {
            if (!request.AllowPartial || bestCoord.Equals(request.Start))
                return PathResult.Failure(failure, explored);

            List<GridCoord> path = PathBuilder.Build(cameFrom, request.Start, bestCoord, grid, out float cost);
            return PathResult.Partial(path, cost, explored);
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