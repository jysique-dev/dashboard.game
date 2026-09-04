using System;
using System.Collections.Generic;

namespace LoopEngine.GridMovement
{
    /// <summary>
    /// Something that can walk a list of cells.
    ///
    /// Separated from the searches on purpose: finding a route and travelling it are
    /// different problems with different failure modes, and the only thing they need to
    /// agree on is the list of cells. A caller can hand this a path from any of the five
    /// algorithms, or a path typed by hand in a test, and it makes no difference here.
    /// </summary>
    public interface IPathFollower
    {
        /// <summary>True while there are still cells left to travel.</summary>
        bool IsMoving { get; }

        /// <summary>The cell the agent occupies. Mid-step it is the cell it left, not the one it is heading to.</summary>
        GridCoord CurrentCoord { get; }

        /// <summary>The last cell of the current path. Meaningless when not moving.</summary>
        GridCoord DestinationCoord { get; }

        /// <summary>
        /// Starts travelling a path. The first entry is expected to be the cell the agent
        /// already occupies, which is what every search in this system produces. Replaces
        /// any path in progress.
        /// </summary>
        void SetPath(IReadOnlyList<GridCoord> path);

        /// <summary>
        /// Stops where the agent stands. It finishes the step it is in rather than
        /// stopping between cells, because half a cell is not a position the grid can
        /// describe.
        /// </summary>
        void Stop();

        /// <summary>Fired when the agent leaves one cell for the next.</summary>
        event Action<GridCoord, GridCoord> StepStarted;

        /// <summary>Fired when the agent settles on a cell.</summary>
        event Action<GridCoord> StepCompleted;

        /// <summary>Fired when the last cell of the path is reached.</summary>
        event Action<GridCoord> PathCompleted;

        /// <summary>
        /// Fired when the next cell has stopped being walkable and no replacement route was
        /// available. The argument is the cell that blocked the way.
        /// </summary>
        event Action<GridCoord> PathBlocked;
    }
}