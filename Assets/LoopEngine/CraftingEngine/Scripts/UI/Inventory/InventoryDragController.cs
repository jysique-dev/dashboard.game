using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>
    /// Drives dragging a stack from a cell to a drop target: the ghost that follows the
    /// pointer, the hover feedback, and the drop itself.
    /// </summary>
    /// <remarks>
    /// One controller per screen, shared by every grid. It also owns click detection, because
    /// capturing the pointer on press suppresses the cell's own click event: a release that
    /// never moved past <see cref="DragThreshold"/> is reported back to the cell as a click.
    /// </remarks>
    public sealed class InventoryDragController : IDisposable
    {
        private readonly VisualElement _root;
        private readonly InventoryUIContext _context;
        private readonly List<IItemDropTarget> _targets = new List<IItemDropTarget>(4);

        private VisualElement _ghost;
        private VisualElement _ghostIcon;
        private Label _ghostAmount;

        private InventorySlotView _pressedCell;
        private IItemDragSource _source;
        private IItemDropTarget _hovered;
        private Vector2 _pressPosition;
        private ItemStack _dragging;
        private int _pointerId = -1;
        private bool _dragActive;
        private bool _disposed;

        /// <summary>Pixels the pointer must travel before a press becomes a drag.</summary>
        public float DragThreshold { get; set; } = 6f;

        /// <summary>Raised after a successful drop, with the amount that moved.</summary>
        public event Action<IItemDropTarget, ItemStack, int> Dropped;

        public InventoryDragController(VisualElement root, InventoryUIContext context)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>True while a stack is in flight.</summary>
        public bool IsDragging => _dragActive;

        /// <summary>The stack being dragged, or empty.</summary>
        public ItemStack Dragging => _dragging;

        // --- Registration ------------------------------------------------------------

        public void RegisterTarget(IItemDropTarget target)
        {
            if (target != null && !_targets.Contains(target))
                _targets.Add(target);
        }

        public bool UnregisterTarget(IItemDropTarget target) => _targets.Remove(target);

        /// <summary>Makes a cell draggable. Called by the grid as it creates its cells.</summary>
        public void AttachSource(InventorySlotView cell, IItemDragSource source)
        {
            if (cell == null || source == null)
                return;

            cell.userData = source;
            cell.RegisterCallback<PointerDownEvent>(OnPointerDown);
            cell.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            cell.RegisterCallback<PointerUpEvent>(OnPointerUp);
            cell.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        /// <summary>Undoes <see cref="AttachSource"/>.</summary>
        public void DetachSource(InventorySlotView cell)
        {
            if (cell == null)
                return;

            cell.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            cell.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            cell.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            cell.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            cell.userData = null;
        }

        // --- Pointer flow ------------------------------------------------------------

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (_dragActive || evt.button != 0)
                return;

            var cell = evt.currentTarget as InventorySlotView;
            if (cell == null)
                return;

            _pressedCell = cell;
            _source = cell.userData as IItemDragSource;
            _pressPosition = evt.position;
            _pointerId = evt.pointerId;

            // The slice is decided here, while the modifier keys are still down.
            TransferAmount amount = InventoryTransfer.FromModifiers(evt.shiftKey, evt.ctrlKey, evt.altKey);
            _dragging = _source == null || cell.IsEmpty
                ? ItemStack.Empty
                : InventoryTransfer.SliceFrom(_source.Adapter, cell.Stack, amount);

            cell.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_pressedCell == null || evt.pointerId != _pointerId)
                return;

            Vector2 position = evt.position;

            if (!_dragActive)
            {
                if ((position - _pressPosition).sqrMagnitude < DragThreshold * DragThreshold)
                    return;

                if (_dragging.IsEmpty || _source == null || !_source.AllowsDragOut)
                    return;

                BeginDrag();
            }

            MoveGhost(position);
            UpdateHover(position);
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (_pressedCell == null || evt.pointerId != _pointerId)
                return;

            InventorySlotView cell = _pressedCell;
            cell.ReleasePointer(evt.pointerId);

            if (_dragActive)
                Drop(evt.position);
            else
                cell.RaiseClicked();

            Reset();
            evt.StopPropagation();
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            // Losing capture mid drag means something else took the pointer. Abandon
            // quietly: nothing has been removed from the source yet, so there is no state
            // to roll back.
            if (_pressedCell != null)
                Reset();
        }

        // --- Drag mechanics ----------------------------------------------------------

        private void BeginDrag()
        {
            _dragActive = true;
            EnsureGhost();

            CraftingUIStyle.SetIcon(_ghostIcon, _context.Theme, _context.Display.GetItemIcon(_dragging.Item));
            _ghostAmount.text = _dragging.Amount > 1
                ? _context.Display.FormatAmount(_dragging.Amount)
                : string.Empty;

            _ghost.style.display = DisplayStyle.Flex;
            _ghost.BringToFront();
        }

        private void MoveGhost(Vector2 position)
        {
            if (_ghost == null)
                return;

            Vector2 local = _root.WorldToLocal(position);
            _ghost.style.left = local.x - _ghost.resolvedStyle.width * 0.5f;
            _ghost.style.top = local.y - _ghost.resolvedStyle.height * 0.5f;
        }

        private void UpdateHover(Vector2 position)
        {
            IItemDropTarget found = FindTarget(position);
            if (ReferenceEquals(found, _hovered))
                return;

            _hovered?.SetDropHighlight(false);
            _hovered = found;
            _hovered?.SetDropHighlight(true);
        }

        private IItemDropTarget FindTarget(Vector2 position)
        {
            // Reverse order, so the most recently registered target wins an overlap.
            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                IItemDropTarget target = _targets[i];
                VisualElement element = target.Element;

                if (element == null || element.resolvedStyle.display == DisplayStyle.None)
                    continue;

                if (!element.worldBound.Contains(position))
                    continue;

                if (!target.CanAccept(in _dragging, _source))
                    continue;

                return target;
            }

            return null;
        }

        private void Drop(Vector2 position)
        {
            IItemDropTarget target = FindTarget(position);
            if (target == null)
                return;

            int moved = target.Accept(in _dragging, _source);
            if (moved > 0)
                Dropped?.Invoke(target, _dragging.WithAmount(moved), moved);
        }

        private void EnsureGhost()
        {
            if (_ghost != null)
                return;

            CraftingUITheme theme = _context.Theme;

            _ghost = new VisualElement { name = "drag-ghost" };
            _ghost.style.position = Position.Absolute;
            _ghost.style.width = 48f;
            _ghost.style.height = 48f;
            _ghost.style.opacity = 0.85f;
            _ghost.style.backgroundColor = theme.SurfaceRaised;
            _ghost.style.alignItems = Align.Center;
            _ghost.style.justifyContent = Justify.Center;
            _ghost.style.display = DisplayStyle.None;
            CraftingUIStyle.SetRadius(_ghost, theme.CornerRadius);
            CraftingUIStyle.SetBorder(_ghost, theme.Accent, theme.BorderWidth);

            // The ghost must never eat the pointer events that are driving the drag.
            _ghost.pickingMode = PickingMode.Ignore;

            _ghostIcon = CraftingUIStyle.Icon(theme, null, 36f);
            _ghostIcon.style.backgroundColor = Color.clear;
            _ghostIcon.pickingMode = PickingMode.Ignore;
            _ghost.Add(_ghostIcon);

            _ghostAmount = CraftingUIStyle.Body(theme, string.Empty);
            _ghostAmount.style.position = Position.Absolute;
            _ghostAmount.style.right = 3f;
            _ghostAmount.style.bottom = 1f;
            _ghostAmount.style.fontSize = theme.FontSmall;
            _ghostAmount.style.unityFontStyleAndWeight = FontStyle.Bold;
            _ghostAmount.pickingMode = PickingMode.Ignore;
            _ghost.Add(_ghostAmount);

            _root.Add(_ghost);
        }

        private void Reset()
        {
            _hovered?.SetDropHighlight(false);
            _hovered = null;

            if (_ghost != null)
                _ghost.style.display = DisplayStyle.None;

            _pressedCell = null;
            _source = null;
            _dragging = ItemStack.Empty;
            _dragActive = false;
            _pointerId = -1;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Reset();
            _targets.Clear();
            Dropped = null;

            _ghost?.RemoveFromHierarchy();
            _ghost = null;
        }
    }
}