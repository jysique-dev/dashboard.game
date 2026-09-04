using LoopEngine.GridMovement.Internal;
using System.Collections.Generic;

namespace LoopEngine.GridMovement
{
    /// <summary>
    /// Breadth-first search: the path with the fewest steps, weights ignored.
    ///
    /// Expands cells in rings around the start, so the first time it touches the goal it
    /// has arrived by the shortest possible number of moves. That guarantee holds only
    /// because every step is treated as equal; on a grid with terrain costs this will
    /// happily route an agent through the swamp because the swamp is one cell nearer.
    /// When costs matter, use <see cref="DijkstraPathfinder"/>.
    ///
    /// Worth keeping for exactly that reason: puzzle grids, tile-based board games and
    /// "how many moves away is this" queries want step count, not cost, and BFS answers
    /// them without a priority queue.
    ///
    /// Method described by Moore (1959), "The shortest path through a maze", Proceedings
    /// of an International Symposium on the Theory of Switching, Harvard University Press.
    /// </summary>
    public sealed class BreadthFirstPathfinder : IPathfinder
    {
        private readonly Queue<GridCoord> frontier = new Queue<GridCoord>();
        private readonly Dictionary<GridCoord, GridCoord> cameFrom = new Dictionary<GridCoord, GridCoord>();
        private readonly HashSet<GridCoord> visited = new HashSet<GridCoord>();

        private GridCoord[] neighborBuffer;

        public string Name => "Breadth-First Search";

        /// <summary>False: BFS counts steps and cannot see terrain weights.</summary>
        public bool RespectsCosts => false;

        public PathResult FindPath(IMovementGrid grid, INeighborProvider neighbors, PathRequest request)
        {
            if (grid == null || neighbors == null) return PathResult.Failure(PathStatus.InvalidStart);
            if (!grid.IsWalkable(request.Start)) return PathResult.Failure(PathStatus.InvalidStart);
            if (!grid.IsWalkable(request.Goal)) return PathResult.Failure(PathStatus.InvalidGoal);

            if (request.Start.Equals(request.Goal))
                return PathResult.Success(new List<GridCoord> { request.Start }, 0f, 0);

            EnsureBuffer(neighbors);
            frontier.Clear();
            cameFrom.Clear();
            visited.Clear();

            frontier.Enqueue(request.Start);
            visited.Add(request.Start);
            int explored = 0;

            while (frontier.Count > 0)
            {
                if (request.HasNodeLimit && explored >= request.MaxExploredNodes)
                    return PathResult.Failure(PathStatus.LimitReached, explored);

                GridCoord current = frontier.Dequeue();
                explored++;

                int count = neighbors.GetNeighbors(grid, current, neighborBuffer);

                for (int i = 0; i < count; i++)
                {
                    GridCoord next = neighborBuffer[i];

                    // Marking on enqueue rather than on dequeue. Marking later would let the
                    // same cell enter the queue once per neighbour that reaches it, which is
                    // still correct but does several times the work.
                    if (!visited.Add(next)) continue;

                    cameFrom[next] = current;

                    if (next.Equals(request.Goal))
                    {
                        List<GridCoord> path = PathBuilder.Build(cameFrom, request.Start, request.Goal, grid, out float cost);
                        return PathResult.Success(path, cost, explored);
                    }

                    frontier.Enqueue(next);
                }
            }

            return PathResult.Failure(PathStatus.NotFound, explored);
        }

        private void EnsureBuffer(INeighborProvider neighbors)
        {
            int size = neighbors.MaxNeighbors;
            if (neighborBuffer == null || neighborBuffer.Length < size)
                neighborBuffer = new GridCoord[size];
        }
    }
}