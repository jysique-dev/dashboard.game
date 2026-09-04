using UnityEngine;

namespace LoopEngine.GridEngine
{
    /// <summary>
    /// Supplies a fill colour per cell. This is what keeps <see cref="GridRenderer"/>
    /// non-generic: the renderer never sees your cell type, only a colour decision.
    /// </summary>
    public interface IGridCellColorSource
    {
        /// <summary>Return false to leave the cell unfilled (only the outline shows).</summary>
        bool TryGetCellColor(GridCoord coord, out Color color);
    }
}