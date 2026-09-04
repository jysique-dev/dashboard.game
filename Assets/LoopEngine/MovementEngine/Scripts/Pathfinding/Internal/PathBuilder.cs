using System.Collections.Generic;

namespace LoopEngine.GridMovement.Internal
{
    /// <summary>
    /// Walks a came-from table backwards and produces the path in walking order.
    ///
    /// Shared by every algorithm: the search strategies differ, but they all end up
    /// holding the same "who reached this cell" table, and duplicating the reversal in
    /// each of them is how the two versions quietly drift apart.
    /// </summary>
    internal static class PathBuilder
    {
        /// <summary>
        /// Builds the path from <paramref name="start"/> to <paramref name="end"/>, both
        /// included, and reports what it costs to enter every cell after the start.
        ///
        /// The cost is recomputed from the grid rather than read from the search's own
        /// score table, so a caller can rebuild a path against a grid that has changed and
        /// get an honest number.
        /// </summary>
        public static List<GridCoord> Build(
            Dictionary<GridCoord, GridCoord> cameFrom,
            GridCoord start,
            GridCoord end,
            IMovementGrid grid,
            out float totalCost)
        {
            List<GridCoord> path = new List<GridCoord> { end };
            GridCoord current = end;

            // The table maps a cell to its predecessor. The start has no entry, which is
            // what terminates the walk; a malformed table would loop forever, so the size
            // of the table is used as a hard ceiling.
            int guard = cameFrom.Count + 1;

            while (!current.Equals(start) && guard-- > 0)
            {
                if (!cameFrom.TryGetValue(current, out GridCoord previous)) break;
                current = previous;
                path.Add(current);
            }

            path.Reverse();

            totalCost = 0f;
            for (int i = 1; i < path.Count; i++)
                totalCost += grid.GetCost(path[i]);

            return path;
        }
    }
}