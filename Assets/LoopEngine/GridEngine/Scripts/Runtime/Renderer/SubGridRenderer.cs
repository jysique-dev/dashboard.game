using System;
using UnityEngine;

namespace LoopEngine.GridEngine
{
    [AddComponentMenu("IsoGrid/Sub Grid Renderer")]
    public class SubGridRenderer : MonoBehaviour, IGridCellColorSource
    {

        [SerializeField] private GridSystem grid;
        private void Reset() => grid = GetComponent<GridSystem>();

        private void Start()
        {
            if (grid == null) grid = GetComponent<GridSystem>();
            if (grid == null) return;

            grid.SetColorSource(this);
        }

        public bool TryGetCellColor(GridCoord coord, out Color color)
        {
            color = default;
            if (grid == null) return false;

            return grid.CellTypes.TryGetValue(coord, out CellType type)
                && grid.TryGetPaletteColor(type, out color);
        }
    }
}

