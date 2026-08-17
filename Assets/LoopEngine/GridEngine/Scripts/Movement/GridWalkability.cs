using UnityEngine;
using LoopEngine.GridEngine.Placement;

namespace LoopEngine.GridEngine.Movement
{
    /// <summary>
    /// Caminabilidad derivada de la grilla de ocupación: una celda es transitable si está
    /// vacía o la pieza que la ocupa no bloquea el movimiento (PlaceableDefinition.blocksMovement).
    /// </summary>
    [AddComponentMenu("LoopEngine/Grid/Grid Walkability")]
    public class GridWalkability : MonoBehaviour, IGridWalkability
    {
        [SerializeField] private PlacementGridHost host;

        private Grid<PlacedObject> Grid => host != null ? host.TypedGrid : null;

        private void Awake()
        {
            if (host == null) host = GetComponentInParent<PlacementGridHost>();
            if (host == null)
                Debug.LogError("[GridWalkability] No se encontró un PlacementGridHost.", this);
        }

        public bool IsWalkable(Vector2Int cell)
        {
            Grid<PlacedObject> grid = Grid;
            if (grid == null || !grid.IsInside(cell)) return false;

            PlacedObject occ = grid.GetCell(cell);
            return occ == null || occ.Definition == null || !occ.Definition.blocksMovement;
        }
    }
}