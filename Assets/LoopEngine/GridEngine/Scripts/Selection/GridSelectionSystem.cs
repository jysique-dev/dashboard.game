using System;
using UnityEngine;
using LoopEngine.GridEngine.Interaction;
using LoopEngine.GridEngine.Placement;
using LoopEngine.GridEngine.Visualization;

namespace LoopEngine.GridEngine.Selection
{
    /// <summary>
    /// Selección de objetos ya colocados. Consume el clic del GridInteractor (S3),
    /// pregunta al GridPlacementSystem (S4) qué hay en la celda y resalta su huella.
    /// Clic en celda vacía = deseleccionar.
    ///
    /// Como también reacciona al clic primario, activa este componente SOLO cuando estás
    /// en "modo selección" (ver GridToolSwitcher) para no chocar con la colocación.
    /// </summary>
    [AddComponentMenu("LoopEngine/Grid/Grid Selection System")]
    public class GridSelectionSystem : MonoBehaviour
    {
        [SerializeField] private GridInteractor interactor;
        [SerializeField] private GridPlacementSystem placement;
        [SerializeField] private GridCellHighlighter selectionHighlighter;
        [SerializeField] private ColorEngine.PaletteColorReference selectionColor;

        public PlacedObject Selected { get; private set; }
        public event Action<PlacedObject> SelectionChanged;

        private void Awake()
        {
            if (interactor == null) interactor = GetComponent<GridInteractor>();
            if (placement == null) placement = GetComponent<GridPlacementSystem>();
        }

        private void OnEnable()
        {
            if (interactor != null) interactor.PrimaryClicked += OnClick;
        }

        private void OnDisable()
        {
            if (interactor != null) interactor.PrimaryClicked -= OnClick;
            ClearSelection();
        }

        private void OnClick(Vector2Int cell)
        {
            Select(placement != null ? placement.GetAt(cell) : null);
        }

        public void ClearSelection() => Select(null);

        private void Select(PlacedObject obj)
        {
            if (obj == Selected) return;
            Selected = obj;

            if (selectionHighlighter != null)
            {
                if (obj != null) selectionHighlighter.Highlight(obj.Cells, selectionColor.Value);
                else selectionHighlighter.Clear();
            }

            SelectionChanged?.Invoke(obj);
        }
    }
}