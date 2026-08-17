using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LoopEngine.GridEngine.Interaction
{
    /// <summary>
    /// Traduce el puntero a una celda de la grilla y emite eventos de hover y clic.
    /// Es el punto central de interacción: colocación (S4) y selección (S5) se suscriben aquí.
    ///
    /// Dependencias:
    ///   - Cámara: campo serializado; si es null usa Camera.main. Asigna la cámara de tu CameraEngine.
    ///   - Puntero: campo serializado (IGridPointerSource) o GetComponent en este GameObject.
    ///   - Grilla:  campo serializado (IGridProvider) o GetComponentInParent.
    ///
    /// Método: rayo desde la cámara por el puntero -> intersección MATEMÁTICA con el plano de la
    /// grilla (Plane.Raycast, sin colliders) -> celda (grid.TryWorldToCell).
    /// </summary>
    [AddComponentMenu(LoopRoutes.GridInteractor)]
    public class GridInteractor : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;

        [Tooltip("Componente que implementa IGridPointerSource. Si se deja vacío, se busca en este GameObject.")]
        [SerializeField] private MonoBehaviour pointerSource;

        [Tooltip("Componente que implementa IGridProvider. Si se deja vacío, se busca en los padres.")]
        [SerializeField] private MonoBehaviour gridProviderSource;

        [Tooltip("Ignora el puntero cuando está sobre UI (requiere un EventSystem con InputSystemUIInputModule).")]
        [SerializeField] private bool blockWhenOverUI = true;

        private IGridPointerSource _pointer;
        private IGridProvider _provider;
        private IReadOnlyGrid _grid;

        /// <summary>Celda actualmente bajo el puntero, o null si está fuera de la grilla / sobre UI.</summary>
        public Vector2Int? HoveredCell { get; private set; }
        public bool HasHover => HoveredCell.HasValue;

        /// <summary>Cambió la celda bajo el puntero (incluye null al salir de la grilla).</summary>
        public event Action<Vector2Int?> HoverChanged;
        /// <summary>Clic primario (izquierdo) sobre una celda válida.</summary>
        public event Action<Vector2Int> PrimaryClicked;
        /// <summary>Clic secundario (derecho) sobre una celda válida.</summary>
        public event Action<Vector2Int> SecondaryClicked;
        /// <summary>La celda cambió MIENTRAS se mantiene el primario (útil para pintar en arrastre).</summary>
        public event Action<Vector2Int> PrimaryDragEntered;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;

            _pointer = ResolvePointer();
            _provider = ResolveProvider();

            if (_pointer == null)
                Debug.LogError("[GridInteractor] No se encontró un IGridPointerSource.", this);

            if (_provider != null)
            {
                if (_provider.Grid != null) _grid = _provider.Grid;
                _provider.OnGridReady += g => _grid = g;
            }
            else
            {
                Debug.LogError("[GridInteractor] No se encontró un IGridProvider.", this);
            }
        }

        private void Update()
        {
            if (!CanInteract())
            {
                SetHover(null);
                return;
            }

            Vector2Int? cell = ComputeHoveredCell();
            bool changed = cell != HoveredCell;
            SetHover(cell);

            if (!cell.HasValue) return;

            if (_pointer.PrimaryPressedThisFrame) PrimaryClicked?.Invoke(cell.Value);
            if (_pointer.SecondaryPressedThisFrame) SecondaryClicked?.Invoke(cell.Value);
            if (changed && _pointer.PrimaryHeld) PrimaryDragEntered?.Invoke(cell.Value);
        }

        private bool CanInteract()
        {
            if (_grid == null || _pointer == null || targetCamera == null) return false;
            if (!_pointer.IsValid) return false;
            if (blockWhenOverUI && IsPointerOverUI()) return false;
            return true;
        }

        private Vector2Int? ComputeHoveredCell()
        {
            Ray ray = targetCamera.ScreenPointToRay(_pointer.ScreenPosition);
            Plane plane = GetGridPlane();

            if (plane.Raycast(ray, out float enter))
            {
                Vector3 world = ray.GetPoint(enter);
                if (_grid.TryWorldToCell(world, out Vector2Int cell))
                    return cell;
            }
            return null;
        }

        private Plane GetGridPlane()
        {
            Vector3 normal = _grid.Plane == GridPlane.XZ ? Vector3.up : Vector3.forward;
            return new Plane(normal, _grid.Origin);
        }

        private void SetHover(Vector2Int? cell)
        {
            if (cell == HoveredCell) return;
            HoveredCell = cell;
            HoverChanged?.Invoke(cell);
        }

        private static bool IsPointerOverUI()
        {
            EventSystem es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }

        // --- Resolución de dependencias por interfaz (Unity no serializa interfaces directas) ---

        private IGridPointerSource ResolvePointer()
        {
            if (pointerSource is IGridPointerSource fromField) return fromField;
            if (pointerSource != null)
                Debug.LogError("[GridInteractor] 'pointerSource' no implementa IGridPointerSource.", this);
            return GetComponent<IGridPointerSource>();
        }

        private IGridProvider ResolveProvider()
        {
            if (gridProviderSource is IGridProvider fromField) return fromField;
            if (gridProviderSource != null)
                Debug.LogError("[GridInteractor] 'gridProviderSource' no implementa IGridProvider.", this);
            return GetComponentInParent<IGridProvider>();
        }
    }
}