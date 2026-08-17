using UnityEngine;
using LoopEngine.GridEngine.Interaction;

namespace LoopEngine.GridEngine.Movement
{
    /// <summary>
    /// "Haz clic para mover": al clic primario, calcula un camino A* desde la celda del personaje
    /// hasta la celda pulsada y se lo pasa al GridMover. Une S1 (grilla), S3 (input), S4 (ocupación)
    /// y este módulo (caminabilidad + pathfinding + movimiento).
    ///
    /// Actívalo solo en "modo movimiento" (GridToolSwitcher) para no chocar con colocación/selección.
    /// </summary>
    [AddComponentMenu("LoopEngine/Grid/Grid Movement Controller")]
    public class GridMovementController : MonoBehaviour
    {
        [SerializeField] private GridInteractor interactor;
        [Tooltip("Componente que implementa IGridProvider (el host de la grilla).")]
        [SerializeField] private MonoBehaviour gridProviderSource;
        [Tooltip("Componente que implementa IGridWalkability.")]
        [SerializeField] private MonoBehaviour walkabilitySource;
        [SerializeField] private GridMover mover;
        [SerializeField] private bool allowDiagonal = false;

        private IReadOnlyGrid _grid;
        private IGridWalkability _walk;
        private GridPathfinder _pathfinder;

        private void Awake()
        {
            if (interactor == null) interactor = GetComponent<GridInteractor>();

            IGridProvider provider = ResolveProvider();
            _walk = walkabilitySource as IGridWalkability ?? GetComponentInParent<IGridWalkability>();

            if (provider != null)
            {
                if (provider.Grid != null) OnGridReady(provider.Grid);
                provider.OnGridReady += OnGridReady;
            }

            if (_walk == null)
                Debug.LogError("[GridMovementController] No se encontró un IGridWalkability.", this);
        }

        private void OnGridReady(IReadOnlyGrid grid)
        {
            _grid = grid;
            if (mover != null) mover.SetGrid(grid);
            if (_walk != null) _pathfinder = new GridPathfinder(grid, _walk, allowDiagonal);
        }

        private void OnEnable()
        {
            if (interactor != null) interactor.PrimaryClicked += OnClick;
        }

        private void OnDisable()
        {
            if (interactor != null) interactor.PrimaryClicked -= OnClick;
        }

        private void OnClick(Vector2Int goal)
        {
            if (_pathfinder == null || mover == null || _grid == null) return;

            Vector2Int start = _grid.WorldToCellClamped(mover.transform.position);
            var path = _pathfinder.FindPath(start, goal);
            if (path != null) mover.FollowPath(path);
        }

        private IGridProvider ResolveProvider()
        {
            if (gridProviderSource is IGridProvider p) return p;
            return GetComponentInParent<IGridProvider>();
        }
    }
}