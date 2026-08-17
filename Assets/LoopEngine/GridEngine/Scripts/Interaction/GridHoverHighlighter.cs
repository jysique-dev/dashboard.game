using UnityEngine;
using LoopEngine.GridEngine.Visualization;

namespace LoopEngine.GridEngine.Interaction
{
    /// <summary>
    /// Conecta GridInteractor (S3) con GridCellHighlighter (S2): resalta la celda bajo el puntero.
    /// Es una pieza de "pegamento" opcional; si prefieres, suscríbete tú mismo a HoverChanged.
    /// </summary>
    [RequireComponent(typeof(GridInteractor))]
    [AddComponentMenu(LoopRoutes.GridhoverHighlighter)]
    public class GridHoverHighlighter : MonoBehaviour
    {
        [SerializeField] private GridCellHighlighter highlighter;
        [SerializeField] private ColorEngine.PaletteColorReference hoverColor;

        private GridInteractor _interactor;

        private void Awake()
        {
            _interactor = GetComponent<GridInteractor>();
            if (highlighter == null) highlighter = GetComponentInChildren<GridCellHighlighter>();
        }

        private void OnEnable()
        {
            if (_interactor == null) _interactor = GetComponent<GridInteractor>();
            _interactor.HoverChanged += OnHoverChanged;
        }

        private void OnDisable()
        {
            if (_interactor != null) _interactor.HoverChanged -= OnHoverChanged;
        }

        private void OnHoverChanged(Vector2Int? cell)
        {
            if (highlighter == null) return;
            if (cell.HasValue) highlighter.HighlightCell(cell.Value, hoverColor.Value);
            else highlighter.Clear();
        }
    }
}