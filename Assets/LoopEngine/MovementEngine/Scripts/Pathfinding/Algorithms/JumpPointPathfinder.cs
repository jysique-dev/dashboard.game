using LoopEngine.GridMovement.Internal;
using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.GridMovement
{
    /// <summary>
    /// Jump Point Search: A* that refuses to expand cells whose expansion could not
    /// possibly matter.
    ///
    /// On an open eight-way grid, thousands of paths are the same length, and A* settles
    /// every cell in the region between start and goal proving it. JPS skips ahead in a
    /// straight line until something interesting appears - an obstacle that forces a turn,
    /// or the goal - and only then puts a node on the heap. The path it returns is the
    /// same optimal path; it simply never wrote down the cells in between, which is why it
    /// expands so much less.
    ///
    /// It buys that with two hard preconditions, and they are not stylistic:
    ///
    /// Uniform cost. The pruning argument is that any skipped cell can be reached at least
    /// as cheaply by another route of the same length. Terrain weights break that argument
    /// outright: a skipped cell might have been the cheap way through. On a weighted grid
    /// this returns a path that looks plausible and is not the cheapest. Use A* there.
    ///
    /// Corner cutting allowed. The pruning and forced-neighbour rules below are the ones
    /// from the original paper, which assumes a diagonal may pass beside an obstacle. Run
    /// them against a stricter corner rule and the search prunes moves that were legal and
    /// keeps moves that were not.
    ///
    /// Both are checked before the search starts rather than trusted, because the failure
    /// mode is a subtly wrong path rather than a crash.
    ///
    /// Harabor, D. and Grastien, A. (2011), "Online Graph Pruning for Pathfinding on Grid
    /// Maps", Proceedings of the 25th AAAI Conference on Artificial Intelligence.
    /// </summary>
    public sealed class JumpPointPathfinder : IPathfinder
    {
        private readonly MinHeap<GridCoord> frontier = new MinHeap<GridCoord>(64);
        private readonly Dictionary<GridCoord, GridCoord> cameFrom = new Dictionary<GridCoord, GridCoord>();
        private readonly Dictionary<GridCoord, float> costSoFar = new Dictionary<GridCoord, float>();
        private readonly HashSet<GridCoord> settled = new HashSet<GridCoord>();
        private readonly GridCoord[] directionBuffer = new GridCoord[8];

        private readonly bool validateUniformCost;

        private IMovementGrid grid;
        private GridCoord goal;
        private float diagonalCost = 1.41421356f;
        private bool costWarningIssued;

        /// <summary>
        /// <paramref name="validateUniformCost"/> samples cell costs during the search and
        /// logs once if the grid turns out to be weighted. Cheap, and it turns a silent
        /// wrong answer into a message. Turn it off in a build where the grid is known to
        /// be uniform.
        /// </summary>
        public JumpPointPathfinder(bool validateUniformCost = true) => this.validateUniformCost = validateUniformCost;

        public string Name => "Jump Point Search";

        /// <summary>False. JPS treats every walkable cell as costing the same, by construction.</summary>
        public bool RespectsCosts => false;

        public PathResult FindPath(IMovementGrid movementGrid, INeighborProvider neighbors, PathRequest request)
        {
            if (movementGrid == null || neighbors == null) return PathResult.Failure(PathStatus.InvalidStart);

            // The preconditions, checked once and refused loudly.
            if (neighbors is not EightWayNeighbors eightWay || eightWay.CornerRule != CornerRule.Allow)
                return PathResult.Failure(PathStatus.UnsupportedConfiguration);

            if (!movementGrid.IsWalkable(request.Start)) return PathResult.Failure(PathStatus.InvalidStart);
            if (!movementGrid.IsWalkable(request.Goal)) return PathResult.Failure(PathStatus.InvalidGoal);

            if (request.Start.Equals(request.Goal))
                return PathResult.Success(new List<GridCoord> { request.Start }, 0f, 0);

            grid = movementGrid;
            goal = request.Goal;
            diagonalCost = eightWay.DiagonalCost;
            costWarningIssued = false;

            IHeuristic heuristic = new OctileHeuristic(1f, diagonalCost);

            frontier.Clear();
            cameFrom.Clear();
            costSoFar.Clear();
            settled.Clear();

            costSoFar[request.Start] = 0f;
            frontier.Push(request.Start, heuristic.Estimate(request.Start, goal));

            int explored = 0;

            while (frontier.Count > 0)
            {
                if (request.HasNodeLimit && explored >= request.MaxExploredNodes)
                    return PathResult.Failure(PathStatus.LimitReached, explored);

                GridCoord current = frontier.Pop();
                if (!settled.Add(current)) continue;
                explored++;

                if (current.Equals(goal))
                {
                    List<GridCoord> jumpPoints = PathBuilder.Build(cameFrom, request.Start, goal, grid, out _);
                    List<GridCoord> full = Expand(jumpPoints);
                    return PathResult.Success(full, costSoFar[current], explored);
                }

                CheckUniformCost(current);

                int directions = GetPrunedDirections(current, request.Start);
                float currentCost = costSoFar[current];

                for (int i = 0; i < directions; i++)
                {
                    if (!TryJump(current, directionBuffer[i], out GridCoord jumpPoint)) continue;
                    if (settled.Contains(jumpPoint)) continue;

                    // Uniform cost means the price of a jump is pure geometry: the octile
                    // distance between the two jump points.
                    float newCost = currentCost + OctileDistance(current, jumpPoint);

                    if (costSoFar.TryGetValue(jumpPoint, out float knownCost) && newCost >= knownCost) continue;

                    costSoFar[jumpPoint] = newCost;
                    cameFrom[jumpPoint] = current;
                    frontier.Push(jumpPoint, newCost + heuristic.Estimate(jumpPoint, goal));
                }
            }

            return PathResult.Failure(PathStatus.NotFound, explored);
        }

        // --- Pruning --------------------------------------------------------

        /// <summary>
        /// The directions worth searching from a cell, given where it was reached from.
        ///
        /// Arriving from a direction makes most neighbours irrelevant: they were already
        /// reachable at least as cheaply without passing through here. What survives is the
        /// natural continuation, plus any neighbour that only became reachable because an
        /// obstacle blocked the alternative route - a forced neighbour.
        ///
        /// The start cell has no parent, so nothing can be pruned and all eight go in.
        /// </summary>
        private int GetPrunedDirections(GridCoord current, GridCoord start)
        {
            int count = 0;

            if (current.Equals(start) || !cameFrom.TryGetValue(current, out GridCoord parent))
            {
                for (int i = 0; i < GridCoord.Cardinals.Length; i++)
                    directionBuffer[count++] = GridCoord.Cardinals[i];

                for (int i = 0; i < GridCoord.Diagonals.Length; i++)
                    directionBuffer[count++] = GridCoord.Diagonals[i];

                return count;
            }

            int dx = Sign(current.X - parent.X);
            int dy = Sign(current.Y - parent.Y);
            int x = current.X;
            int y = current.Y;

            if (dx != 0 && dy != 0)
            {
                bool alongY = IsWalkable(x, y + dy);
                bool alongX = IsWalkable(x + dx, y);

                if (alongY) directionBuffer[count++] = new GridCoord(0, dy);
                if (alongX) directionBuffer[count++] = new GridCoord(dx, 0);
                if (alongY || alongX) directionBuffer[count++] = new GridCoord(dx, dy);

                // Forced: the obstacle behind us on one axis makes a sideways diagonal the
                // only way to reach what is past it.
                if (!IsWalkable(x - dx, y) && alongY) directionBuffer[count++] = new GridCoord(-dx, dy);
                if (!IsWalkable(x, y - dy) && alongX) directionBuffer[count++] = new GridCoord(dx, -dy);
            }
            else if (dx != 0)
            {
                if (IsWalkable(x + dx, y))
                {
                    directionBuffer[count++] = new GridCoord(dx, 0);

                    if (!IsWalkable(x, y + 1)) directionBuffer[count++] = new GridCoord(dx, 1);
                    if (!IsWalkable(x, y - 1)) directionBuffer[count++] = new GridCoord(dx, -1);
                }
            }
            else if (dy != 0)
            {
                if (IsWalkable(x, y + dy))
                {
                    directionBuffer[count++] = new GridCoord(0, dy);

                    if (!IsWalkable(x + 1, y)) directionBuffer[count++] = new GridCoord(1, dy);
                    if (!IsWalkable(x - 1, y)) directionBuffer[count++] = new GridCoord(-1, dy);
                }
            }

            return count;
        }

        // --- Jumping --------------------------------------------------------

        /// <summary>
        /// Runs in one direction until it finds a cell worth putting on the heap, or until
        /// it runs out of walkable ground.
        /// </summary>
        private bool TryJump(GridCoord from, GridCoord direction, out GridCoord jumpPoint)
            => direction.X != 0 && direction.Y != 0
                ? TryJumpDiagonal(from, direction, out jumpPoint)
                : TryJumpStraight(from, direction, out jumpPoint);

        /// <summary>
        /// Straight scan. Iterative rather than recursive: a long open corridor would
        /// otherwise put one stack frame per cell, and a big map can overflow.
        /// </summary>
        private bool TryJumpStraight(GridCoord from, GridCoord direction, out GridCoord jumpPoint)
        {
            int dx = direction.X;
            int dy = direction.Y;
            GridCoord current = from;

            while (true)
            {
                current += direction;
                if (!IsWalkable(current)) break;

                if (current.Equals(goal))
                {
                    jumpPoint = current;
                    return true;
                }

                int x = current.X;
                int y = current.Y;

                if (dx != 0)
                {
                    // A neighbour ahead-and-to-the-side that is walkable while the cell
                    // directly to the side is not: this cell is the only way around.
                    if ((IsWalkable(x + dx, y + 1) && !IsWalkable(x, y + 1)) ||
                        (IsWalkable(x + dx, y - 1) && !IsWalkable(x, y - 1)))
                    {
                        jumpPoint = current;
                        return true;
                    }
                }
                else
                {
                    if ((IsWalkable(x + 1, y + dy) && !IsWalkable(x + 1, y)) ||
                        (IsWalkable(x - 1, y + dy) && !IsWalkable(x - 1, y)))
                    {
                        jumpPoint = current;
                        return true;
                    }
                }
            }

            jumpPoint = default;
            return false;
        }

        /// <summary>
        /// Diagonal scan. At every step it also looks along both component axes, because a
        /// jump point found there makes this diagonal cell worth recording as the branch.
        /// The two component scans are the iterative straight version, so recursion never
        /// goes deeper than one level.
        /// </summary>
        private bool TryJumpDiagonal(GridCoord from, GridCoord direction, out GridCoord jumpPoint)
        {
            int dx = direction.X;
            int dy = direction.Y;
            GridCoord current = from;

            while (true)
            {
                current += direction;
                if (!IsWalkable(current)) break;

                if (current.Equals(goal))
                {
                    jumpPoint = current;
                    return true;
                }

                int x = current.X;
                int y = current.Y;

                if ((IsWalkable(x - dx, y + dy) && !IsWalkable(x - dx, y)) ||
                    (IsWalkable(x + dx, y - dy) && !IsWalkable(x, y - dy)))
                {
                    jumpPoint = current;
                    return true;
                }

                if (TryJumpStraight(current, new GridCoord(dx, 0), out _) ||
                    TryJumpStraight(current, new GridCoord(0, dy), out _))
                {
                    jumpPoint = current;
                    return true;
                }
            }

            jumpPoint = default;
            return false;
        }

        // --- Output ---------------------------------------------------------

        /// <summary>
        /// Fills in the cells between jump points.
        ///
        /// The search deals in jump points, which can be many cells apart. A mover walks
        /// cell by cell, so handing it the raw result would teleport an agent across a
        /// corridor. Every gap is a straight or perfectly diagonal run by construction, so
        /// filling it is a matter of stepping by the unit direction.
        /// </summary>
        private static List<GridCoord> Expand(List<GridCoord> jumpPoints)
        {
            List<GridCoord> full = new List<GridCoord>(jumpPoints.Count);
            if (jumpPoints.Count == 0) return full;

            full.Add(jumpPoints[0]);

            for (int i = 1; i < jumpPoints.Count; i++)
            {
                GridCoord from = jumpPoints[i - 1];
                GridCoord to = jumpPoints[i];

                GridCoord step = new GridCoord(Sign(to.X - from.X), Sign(to.Y - from.Y));
                GridCoord current = from;

                while (!current.Equals(to))
                {
                    current += step;
                    full.Add(current);
                }
            }

            return full;
        }

        // --- Helpers --------------------------------------------------------

        private float OctileDistance(GridCoord a, GridCoord b)
        {
            int dx = Mathf.Abs(a.X - b.X);
            int dy = Mathf.Abs(a.Y - b.Y);

            int diagonal = Mathf.Min(dx, dy);
            int straight = Mathf.Max(dx, dy) - diagonal;

            return diagonal * diagonalCost + straight;
        }

        private bool IsWalkable(GridCoord coord) => grid.IsWalkable(coord);

        private bool IsWalkable(int x, int y) => grid.IsWalkable(new GridCoord(x, y));

        private static int Sign(int value) => value > 0 ? 1 : value < 0 ? -1 : 0;

        private void CheckUniformCost(GridCoord coord)
        {
            if (!validateUniformCost || costWarningIssued) return;

            float cost = grid.GetCost(coord);
            if (Mathf.Approximately(cost, 1f)) return;

            costWarningIssued = true;
            Debug.LogWarning(
                $"[JumpPointPathfinder] Cell {coord} costs {cost}. This search assumes every walkable cell " +
                "costs 1 and will not return the cheapest path on a weighted grid. Use A* instead.");
        }
    }
}