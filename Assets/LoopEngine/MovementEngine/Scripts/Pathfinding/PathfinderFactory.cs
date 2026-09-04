using System;
using System.Collections.Generic;

namespace LoopEngine.GridMovement
{
    /// <summary>
    /// Turns a <see cref="PathfindingAlgorithm"/> into an <see cref="IPathfinder"/>.
    ///
    /// Instances are cached per algorithm. Pathfinders hold reusable scratch buffers, so
    /// building a new one for every request would allocate a heap, several dictionaries
    /// and an array each time an agent decides to walk somewhere. The cache is per
    /// factory instance, which keeps the sharing explicit: one factory per thread, or per
    /// agent, if a caller ever needs isolation.
    /// </summary>
    public sealed class PathfinderFactory
    {
        private readonly Dictionary<PathfindingAlgorithm, IPathfinder> cache = new Dictionary<PathfindingAlgorithm, IPathfinder>();

        /// <summary>A shared factory for the common case of single-threaded gameplay code.</summary>
        public static PathfinderFactory Default { get; } = new PathfinderFactory();

        /// <summary>Returns the cached pathfinder for this algorithm, creating it on first use.</summary>
        public IPathfinder Get(PathfindingAlgorithm algorithm)
        {
            if (cache.TryGetValue(algorithm, out IPathfinder existing)) return existing;

            IPathfinder created = Create(algorithm);
            cache[algorithm] = created;
            return created;
        }

        /// <summary>A fresh, uncached instance. Use when the pathfinder must not be shared.</summary>
        public static IPathfinder Create(PathfindingAlgorithm algorithm) => algorithm switch
        {
            PathfindingAlgorithm.BreadthFirst => new BreadthFirstPathfinder(),
            PathfindingAlgorithm.Dijkstra => new DijkstraPathfinder(),
            PathfindingAlgorithm.AStar => new AStarPathfinder(),
            PathfindingAlgorithm.GreedyBestFirst => new GreedyBestFirstPathfinder(),
            PathfindingAlgorithm.JumpPoint => new JumpPointPathfinder(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(algorithm), algorithm, "No pathfinder is registered for this algorithm.")
        };

        public void Clear() => cache.Clear();
    }
}