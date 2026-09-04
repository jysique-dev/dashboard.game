using System.Collections.Generic;

namespace LoopEngine.GridMovement
{
    /// <summary>
    /// The answer to a <see cref="PathRequest"/>: a status, the cells to walk, what they
    /// cost, and how much work the search did.
    ///
    /// The path always includes the start cell as its first entry. Movers therefore skip
    /// index 0, and a path of length 1 means "already there".
    /// </summary>
    public readonly struct PathResult
    {
        private readonly IReadOnlyList<GridCoord> path;

        public readonly PathStatus Status;

        /// <summary>Sum of the entry costs of every cell after the start.</summary>
        public readonly float TotalCost;

        /// <summary>Cells expanded before the search stopped. Purely diagnostic, but the honest way to compare algorithms.</summary>
        public readonly int ExploredNodes;

        private PathResult(PathStatus status, IReadOnlyList<GridCoord> path, float totalCost, int exploredNodes)
        {
            Status = status;
            this.path = path;
            TotalCost = totalCost;
            ExploredNodes = exploredNodes;
        }

        private static readonly GridCoord[] Empty = new GridCoord[0];

        /// <summary>Cells from start to end, inclusive. Never null; empty when there is no path.</summary>
        public IReadOnlyList<GridCoord> Path => path ?? Empty;

        public int Length => Path.Count;

        /// <summary>True when there is something to walk, complete or partial.</summary>
        public bool HasPath => Status is PathStatus.Success or PathStatus.Partial && Path.Count > 0;

        /// <summary>True only when the goal itself was reached.</summary>
        public bool IsComplete => Status == PathStatus.Success;

        public static PathResult Success(IReadOnlyList<GridCoord> path, float cost, int explored)
            => new PathResult(PathStatus.Success, path, cost, explored);

        public static PathResult Partial(IReadOnlyList<GridCoord> path, float cost, int explored)
            => new PathResult(PathStatus.Partial, path, cost, explored);

        /// <summary>A result with no path. The status says why.</summary>
        public static PathResult Failure(PathStatus status, int explored = 0)
            => new PathResult(status, Empty, 0f, explored);

        public override string ToString()
            => $"{Status} ({Length} cells, cost {TotalCost:0.##}, {ExploredNodes} explored)";
    }
}