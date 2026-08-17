using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using LoopEngine.GridEngine.Interaction;
using LoopEngine.GridEngine.Visualization;

namespace LoopEngine.GridEngine.Placement
{
    /// <summary>
    /// Glue interactivo de colocación. Conecta GridInteractor (S3) con GridPlacementSystem
    /// y dibuja el "ghost" de la huella con un GridCellHighlighter (S2), verde/rojo según validez.
    ///
    /// - Hover: mueve el ghost y lo pinta según CanPlace.
    /// - Clic primario: coloca (soporta arrastre para pintar en serie vía PrimaryDragEntered).
    /// - Clic secundario: quita (si rightClickRemoves).
    /// - Rotación: método RotateClockwise() (por defecto ligado a la tecla R del Input System).
    ///
    /// Nota: usa su PROPIO GridCellHighlighter (una "capa" independiente del hover de la S3).
    /// La lectura de la tecla R está aquí solo por comodidad; conéctala a tu InputReader llamando
    /// a RotateClockwise()/RotateCounterClockwise() y quita el bloque de Update si prefieres.
    /// </summary>
    [AddComponentMenu("LoopEngine/Grid/Grid Placement Controller")]
    public class GridPlacementController : MonoBehaviour
    {
        [SerializeField] private GridInteractor interactor;
        [SerializeField] private GridPlacementSystem placement;
        [SerializeField] private GridCellHighlighter ghostHighlighter;
        [SerializeField] private PlaceableDefinition activePlaceable;

        [Header("Colores del ghost")]
        [SerializeField] private ColorEngine.PaletteColorReference validColor;
        [SerializeField] private ColorEngine.PaletteColorReference invalidColor;


        [Header("Comportamiento")]
        [SerializeField] private bool rightClickRemoves = true;
        [SerializeField] private bool paintOnDrag = true;

        private GridRotation _rotation = GridRotation.Deg0;
        private Vector2Int? _hovered;
        private readonly List<Vector2Int> _ghostCells = new();

        private void Awake()
        {
            if (interactor == null) interactor = GetComponent<GridInteractor>();
            if (placement == null) placement = GetComponent<GridPlacementSystem>();
        }

        private void OnEnable()
        {
            if (interactor == null) return;
            interactor.HoverChanged += OnHoverChanged;
            interactor.PrimaryClicked += OnPrimaryClicked;
            interactor.PrimaryDragEntered += OnPrimaryDragEntered;
            interactor.SecondaryClicked += OnSecondaryClicked;
        }

        private void OnDisable()
        {
            if (interactor == null) return;
            interactor.HoverChanged -= OnHoverChanged;
            interactor.PrimaryClicked -= OnPrimaryClicked;
            interactor.PrimaryDragEntered -= OnPrimaryDragEntered;
            interactor.SecondaryClicked -= OnSecondaryClicked;
        }

        private void Update()
        {
            // Rotación por comodidad (tecla R). Reemplázalo por tu InputReader si quieres.
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                RotateClockwise();
        }

        // --- API pública (para UI o tu InputReader) ---

        public void SetActivePlaceable(PlaceableDefinition def)
        {
            activePlaceable = def;
            RefreshGhost();
        }

        public void RotateClockwise()
        {
            _rotation = _rotation.RotatedCW();
            RefreshGhost();
        }

        public void RotateCounterClockwise()
        {
            _rotation = _rotation.RotatedCCW();
            RefreshGhost();
        }

        // --- Reacción a la interacción ---

        private void OnHoverChanged(Vector2Int? cell)
        {
            _hovered = cell;
            RefreshGhost();
        }

        private void OnPrimaryClicked(Vector2Int cell) => TryPlaceAt(cell);

        private void OnPrimaryDragEntered(Vector2Int cell)
        {
            if (paintOnDrag) TryPlaceAt(cell);
        }

        private void OnSecondaryClicked(Vector2Int cell)
        {
            if (rightClickRemoves && placement != null) placement.TryRemoveAt(cell);
            RefreshGhost();
        }

        private void TryPlaceAt(Vector2Int cell)
        {
            if (activePlaceable == null || placement == null) return;
            placement.TryPlace(activePlaceable, cell, _rotation);
            RefreshGhost();
        }

        private void RefreshGhost()
        {
            if (ghostHighlighter == null) return;

            if (!_hovered.HasValue || activePlaceable == null || placement == null)
            {
                ghostHighlighter.Clear();
                return;
            }

            bool ok = placement.CanPlace(activePlaceable, _hovered.Value, _rotation, _ghostCells);
            ghostHighlighter.Highlight(_ghostCells, ok ? validColor.Value : invalidColor.Value);
        }
    }
}