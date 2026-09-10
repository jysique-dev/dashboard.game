using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>
    /// A container drawn as a grid of cells. One cell per item the container holds, plus
    /// filler cells so the grid does not resize every time a stack runs out.
    /// </summary>
    /// <remarks>
    /// Selection is tracked by item id, not by cell position. Cells are ordered by catalogue
    /// position and a stack disappearing shifts everything after it, so a remembered index
    /// would silently start pointing at a different item.
    /// </remarks>
    public sealed class InventoryGridView : VisualElement, IDisposable, IItemDragSource, IItemDropTarget
    {
        private readonly InventoryUIContext _context;
        private readonly List<InventorySlotView> _cells = new List<InventorySlotView>(32);
        private readonly VisualElement _grid;
        private readonly Label _header;
        private readonly Label _empty;
        private readonly float _cellSize;

        private readonly List<int> _queryBuffer = new List<int>(32);
        private readonly List<int> _visible = new List<int>(32);
        private readonly List<int> _visibleAmounts = new List<int>(32);

        private ItemContainerAdapter _adapter;
        private InventoryDragController _drag;
        private InventoryQuery _query;
        private CraftingId _selected;
        private int _shownOccupied = -1;
        private bool _disposed;

        /// <summary>Cells drawn even when the container holds less. Keeps the layout still.</summary>
        public int MinCells { get; set; } = 12;

        /// <summary>Hard ceiling on drawn cells. Zero means no ceiling.</summary>
        public int MaxCells { get; set; }

        /// <summary>
        /// Splits a holding across several cells according to <see cref="ItemDefinition.MaxStack"/>,
        /// so 130 units of something that stacks to 64 draws as 64, 64 and 2.
        /// </summary>
        /// <remarks>
        /// Presentation only. The crafting system calls MaxStack declarative and leaves
        /// enforcement to the container, so this never refuses anything: if a container
        /// accepts 130 in one place, the grid simply draws it in three.
        /// </remarks>
        public bool SplitStacks { get; set; } = true;

        /// <summary>Ceiling on drawn cells when splitting, so a stack limit of 1 cannot flood the grid.</summary>
        public int SplitCellLimit { get; set; } = 512;

        /// <summary>False to forbid taking items out, e.g. a machine's output being watched.</summary>
        public bool AllowsDragOut { get; set; } = true;

        /// <summary>False to forbid dropping items in.</summary>
        public bool AllowsDrop { get; set; } = true;

        /// <summary>
        /// Where a quick move sends items. Set it to the other side of the window so a
        /// shortcut can shuttle a stack without dragging it.
        /// </summary>
        public ItemContainerAdapter QuickMoveTarget { get; set; }

        /// <summary>The selected item, or <see cref="CraftingId.None"/>.</summary>
        public CraftingId SelectedItem => _selected;

        /// <summary>The selected stack as last sampled. Empty when nothing is selected.</summary>
        public ItemStack SelectedStack
            => _adapter == null || !_selected.IsValid
                ? ItemStack.Empty
                : new ItemStack(_selected, _adapter.GetAmount(in _selected));

        /// <summary>The container being drawn, or null.</summary>
        public ItemContainerAdapter Adapter => _adapter;

        /// <summary>
        /// Filter and order. Null draws everything the container holds, in catalogue order.
        /// </summary>
        public InventoryQuery Query
        {
            get => _query;
            set
            {
                if (ReferenceEquals(_query, value))
                    return;

                if (_query != null)
                    _query.Changed -= Refresh;

                _query = value;

                if (_query != null)
                    _query.Changed += Refresh;

                Refresh();
            }
        }

        /// <summary>Card shown while the pointer rests on a cell. Null disables tooltips.</summary>
        public ItemTooltipView Tooltip { get; set; }

        /// <summary>Raised when the selection changes, including when it is cleared.</summary>
        public event Action<InventoryGridView> SelectionChanged;

        /// <summary>Raised on every click, before the selection is applied.</summary>
        public event Action<InventoryGridView, InventorySlotView> CellClicked;

        /// <summary>
        /// Makes the grid draggable and droppable. Set it before the first refresh, or the
        /// cells already created will not be attached.
        /// </summary>
        public InventoryDragController DragController
        {
            get => _drag;
            set
            {
                if (ReferenceEquals(_drag, value))
                    return;

                if (_drag != null)
                {
                    _drag.UnregisterTarget(this);
                    for (int i = 0; i < _cells.Count; i++)
                        _drag.DetachSource(_cells[i]);
                }

                _drag = value;

                if (_drag == null)
                    return;

                _drag.RegisterTarget(this);
                for (int i = 0; i < _cells.Count; i++)
                    _drag.AttachSource(_cells[i], this);
            }
        }

        public InventoryGridView(InventoryUIContext context, string title, float cellSize = 56f)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _cellSize = cellSize;

            CraftingUITheme theme = context.Theme;
            style.marginBottom = theme.Spacing;

            VisualElement headerRow = CraftingUIStyle.Row(theme);
            headerRow.style.marginBottom = theme.SpacingTight;
            Add(headerRow);

            Label caption = CraftingUIStyle.Body(theme, title ?? string.Empty);
            caption.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            caption.style.flexGrow = 1f;
            headerRow.Add(caption);

            _header = CraftingUIStyle.Muted(theme, string.Empty);
            headerRow.Add(_header);

            _grid = new VisualElement();
            _grid.style.flexDirection = FlexDirection.Row;
            _grid.style.flexWrap = Wrap.Wrap;
            Add(_grid);

            _empty = CraftingUIStyle.Muted(theme, CraftingUIStrings.InventoryEmpty);
            Add(_empty);
        }

        /// <summary>Points the grid at a container. Pass null to show nothing.</summary>
        public void Bind(ItemContainerAdapter adapter)
        {
            if (ReferenceEquals(_adapter, adapter))
                return;

            if (_adapter != null)
                _adapter.Changed -= OnContainerChanged;

            _adapter = adapter;

            if (_adapter != null)
                _adapter.Changed += OnContainerChanged;

            _shownOccupied = -1;
            SetSelected(CraftingId.None);
            Refresh();
        }

        /// <summary>
        /// Redraws the cells. Called when the adapter reports a change, so it runs at the
        /// sampling rate at worst and not at all while the container is still.
        /// </summary>
        public void Refresh()
        {
            int shown = BuildVisible();

            int wanted = shown > MinCells ? shown : MinCells;
            if (MaxCells > 0 && wanted > MaxCells)
                wanted = MaxCells;

            while (_cells.Count < wanted)
                CreateCell();

            for (int i = 0; i < _cells.Count; i++)
            {
                InventorySlotView cell = _cells[i];

                if (i >= wanted)
                {
                    cell.style.display = DisplayStyle.None;
                    continue;
                }

                cell.style.display = DisplayStyle.Flex;
                cell.Bind(i < shown ? GetVisibleStack(i) : ItemStack.Empty);
                cell.SetSelected(_selected.IsValid && cell.Stack.Item.Equals(_selected));
            }

            if (_shownOccupied != shown)
            {
                _shownOccupied = shown;
                // Cells, not types: with splitting on, one type can occupy several.
                _header.text = shown > 0 ? shown + CraftingUIStrings.ItemCellsSuffix : string.Empty;

                bool filtering = _query != null && !_query.IsEmpty;
                _empty.text = filtering ? CraftingUIStrings.NoMatches : CraftingUIStrings.InventoryEmpty;
                _empty.style.display = shown == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            // The selected stack may have been spent entirely while it was selected.
            if (_selected.IsValid && _adapter != null && _adapter.GetAmount(in _selected) <= 0)
                SetSelected(CraftingId.None);
        }

        /// <summary>Selects an item, or clears the selection with an invalid id.</summary>
        public void SetSelected(in CraftingId item)
        {
            if (_selected.Equals(item))
                return;

            _selected = item;

            for (int i = 0; i < _cells.Count; i++)
                _cells[i].SetSelected(item.IsValid && _cells[i].Stack.Item.Equals(item));

            SelectionChanged?.Invoke(this);
        }

        /// <summary>Clears the selection.</summary>
        public void ClearSelection() => SetSelected(CraftingId.None);

        /// <summary>
        /// Pulses every cell currently showing an item. Does nothing when the item is not on
        /// screen, which happens when a craft spent the last of it before the container was
        /// sampled again.
        /// </summary>
        /// <returns>How many cells flashed.</returns>
        public int Flash(in CraftingId item, UnityEngine.Color color, int durationMs = 320)
        {
            if (!item.IsValid)
                return 0;

            int flashed = 0;
            for (int i = 0; i < _cells.Count; i++)
            {
                InventorySlotView cell = _cells[i];
                if (cell.style.display == DisplayStyle.None || !cell.Stack.Item.Equals(item))
                    continue;

                cell.Flash(color, durationMs);
                flashed++;
            }

            return flashed;
        }

        /// <summary>Rebuilds the list of catalogue indices to draw.</summary>
        private int BuildVisible()
        {
            _queryBuffer.Clear();
            _visible.Clear();
            _visibleAmounts.Clear();

            if (_adapter == null)
                return 0;

            if (_query != null)
            {
                _query.Build(_adapter, _context.Display, _queryBuffer);
            }
            else
            {
                int occupied = _adapter.OccupiedCount;
                for (int rank = 0; rank < occupied; rank++)
                {
                    int index = _adapter.GetOccupiedIndex(rank);
                    if (index >= 0)
                        _queryBuffer.Add(index);
                }
            }

            int ceiling = MaxCells > 0 ? MaxCells : SplitCellLimit;

            for (int i = 0; i < _queryBuffer.Count; i++)
            {
                int index = _queryBuffer[i];
                int amount = _adapter.GetAmountAt(index);
                if (amount <= 0)
                    continue;

                int max = MaxStackOf(index);

                if (!SplitStacks || max <= 0 || amount <= max)
                {
                    Append(index, amount);
                    if (_visible.Count >= ceiling)
                        break;

                    continue;
                }

                int left = amount;
                while (left > 0 && _visible.Count < ceiling)
                {
                    int slice = left > max ? max : left;
                    Append(index, slice);
                    left -= slice;
                }

                if (_visible.Count >= ceiling)
                    break;
            }

            return _visible.Count;
        }

        private void Append(int index, int amount)
        {
            _visible.Add(index);
            _visibleAmounts.Add(amount);
        }

        private int MaxStackOf(int index)
        {
            ItemDefinition definition = _context.Catalogue.GetDefinition(index);
            return definition == null ? 0 : definition.MaxStack;
        }

        private ItemStack GetVisibleStack(int position)
            => new ItemStack(_context.Catalogue.GetId(_visible[position]), _visibleAmounts[position]);

        private void CreateCell()
        {
            var cell = new InventorySlotView(_context.Theme, _context.Display, _cellSize)
            {
                CellIndex = _cells.Count
            };

            cell.Clicked += OnCellClicked;
            cell.RegisterCallback<PointerEnterEvent>(OnCellPointerEnter);
            cell.RegisterCallback<PointerMoveEvent>(OnCellPointerMove);
            cell.RegisterCallback<PointerLeaveEvent>(OnCellPointerLeave);
            _cells.Add(cell);
            _grid.Add(cell);

            _drag?.AttachSource(cell, this);
        }

        private void OnCellClicked(InventorySlotView cell)
        {
            CellClicked?.Invoke(this, cell);

            // Clicking the selected cell again clears it, which is how every inventory the
            // player has ever used behaves.
            if (cell.IsEmpty || cell.Stack.Item.Equals(_selected))
                SetSelected(CraftingId.None);
            else
                SetSelected(cell.Stack.Item);
        }

        private void OnContainerChanged(ItemContainerAdapter adapter) => Refresh();

        // --- Tooltip -----------------------------------------------------------------

        private void OnCellPointerEnter(PointerEnterEvent evt)
        {
            if (Tooltip == null || !(evt.currentTarget is InventorySlotView cell) || cell.IsEmpty)
                return;

            Tooltip.Show(cell.Stack, evt.position);
        }

        private void OnCellPointerMove(PointerMoveEvent evt)
        {
            // Only reposition: showing again on every move would rebind the card constantly.
            if (Tooltip != null && Tooltip.IsVisible)
                Tooltip.MoveTo(evt.position);
        }

        private void OnCellPointerLeave(PointerLeaveEvent evt) => Tooltip?.Hide();

        // --- Drag source -------------------------------------------------------------

        ItemContainerAdapter IItemDragSource.Adapter => _adapter;

        bool IItemDragSource.AllowsDragOut => AllowsDragOut && _adapter != null;

        // --- Drop target -------------------------------------------------------------

        VisualElement IItemDropTarget.Element => _grid;

        bool IItemDropTarget.CanAccept(in ItemStack stack, IItemDragSource source)
        {
            if (!AllowsDrop || _adapter == null || stack.IsEmpty)
                return false;

            // Dropping onto the grid it came from is a no-op, not an error.
            if (source != null && ReferenceEquals(source.Adapter, _adapter))
                return false;

            return _adapter.GetInsertableAmount(in stack) > 0;
        }

        int IItemDropTarget.Accept(in ItemStack stack, IItemDragSource source)
        {
            if (source?.Adapter == null || _adapter == null)
                return 0;

            return source.Adapter.TransferTo(_adapter, in stack);
        }

        void IItemDropTarget.SetDropHighlight(bool active)
            => CraftingUIStyle.SetBorderColor(this, active ? _context.Theme.Accent : _context.Theme.Border);

        // --- Quick move --------------------------------------------------------------

        /// <summary>
        /// Sends part of an item to <see cref="QuickMoveTarget"/> without dragging.
        /// </summary>
        /// <returns>How many units moved.</returns>
        public int QuickMove(in CraftingId item, TransferAmount amount = TransferAmount.All)
        {
            if (_adapter == null || QuickMoveTarget == null || !AllowsDragOut)
                return 0;

            return InventoryTransfer.Move(_adapter, QuickMoveTarget, in item, amount);
        }

        /// <summary>Quick moves whatever is selected.</summary>
        public int QuickMoveSelection(TransferAmount amount = TransferAmount.All)
            => _selected.IsValid ? QuickMove(in _selected, amount) : 0;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_adapter != null)
                _adapter.Changed -= OnContainerChanged;

            for (int i = 0; i < _cells.Count; i++)
            {
                _cells[i].Clicked -= OnCellClicked;
                _cells[i].UnregisterCallback<PointerEnterEvent>(OnCellPointerEnter);
                _cells[i].UnregisterCallback<PointerMoveEvent>(OnCellPointerMove);
                _cells[i].UnregisterCallback<PointerLeaveEvent>(OnCellPointerLeave);
                _drag?.DetachSource(_cells[i]);
            }

            if (_query != null)
                _query.Changed -= Refresh;

            _query = null;
            Tooltip = null;

            _drag?.UnregisterTarget(this);
            _drag = null;

            SelectionChanged = null;
            CellClicked = null;
            _adapter = null;
        }
    }
}