using System;
using UnityEngine.UIElements;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>
    /// The machine's own two containers, side by side: what it eats and what it has made.
    /// </summary>
    /// <remarks>
    /// The containers are passed in rather than read off the machine, because this view has
    /// no business knowing how a <see cref="MachineInstance"/> stores them. Whoever created
    /// the machine already has both references.
    /// <para>
    /// The output grid refuses drops. Putting items into a machine's output would be
    /// indistinguishable from the machine having produced them, and nothing downstream could
    /// tell the difference.
    /// </para>
    /// </remarks>
    public sealed class MachineContainersView : VisualElement, IDisposable
    {
        private readonly InventoryUIContext _context;
        private readonly InventoryGridView _input;
        private readonly InventoryGridView _output;

        private bool _disposed;

        public MachineContainersView(InventoryUIContext context, float cellSize = 48f)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            style.flexDirection = FlexDirection.Row;
            style.marginBottom = context.Theme.Spacing;

            _input = new InventoryGridView(context, CraftingUIStrings.MachineInput, cellSize)
            {
                MinCells = 6
            };
            _input.style.flexGrow = 1f;
            _input.style.marginRight = context.Theme.Spacing;
            Add(_input);

            _output = new InventoryGridView(context, CraftingUIStrings.MachineOutput, cellSize)
            {
                MinCells = 6,
                AllowsDrop = false
            };
            _output.style.flexGrow = 1f;
            Add(_output);
        }

        /// <summary>The input grid. Accepts drops, allows taking items back out.</summary>
        public InventoryGridView Input => _input;

        /// <summary>The output grid. Read only as a destination.</summary>
        public InventoryGridView Output => _output;

        /// <summary>Watches a machine's containers. Pass nulls to show nothing.</summary>
        public void Bind(IItemContainer input, IItemContainer output)
        {
            _input.Bind(input == null ? null : _context.Track(input));
            _output.Bind(output == null ? null : _context.Track(output));
        }

        /// <summary>
        /// Wires quick moves and dragging between these grids and the player's inventory.
        /// </summary>
        public void ConnectTo(InventoryGridView playerGrid, InventoryDragController drag)
        {
            if (drag != null)
            {
                _input.DragController = drag;
                _output.DragController = drag;

                if (playerGrid != null)
                    playerGrid.DragController = drag;
            }

            if (playerGrid == null)
                return;

            // Quick moves point at each other: player sends to input, both machine sides
            // send back to the player.
            playerGrid.QuickMoveTarget = _input.Adapter;
            _input.QuickMoveTarget = playerGrid.Adapter;
            _output.QuickMoveTarget = playerGrid.Adapter;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _input.Dispose();
            _output.Dispose();
        }
    }

    /// <summary>
    /// Makes the upgrade section a drop target: dragging an item onto it installs the
    /// modifier that item provides.
    /// </summary>
    /// <remarks>
    /// This is the piece the machine UI could not supply on its own. Installing a modifier
    /// consumes nothing in the crafting system, so the item has to be removed from its
    /// container first, and only something that holds both sides can do that. This does.
    /// </remarks>
    public sealed class ModifierDropTarget : IItemDropTarget
    {
        private readonly ModifierSlotsView _view;
        private readonly CraftingUITheme _theme;

        public ModifierDropTarget(ModifierSlotsView view, CraftingUITheme theme)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        }

        public VisualElement Element => _view;

        public bool CanAccept(in ItemStack stack, IItemDragSource source)
            => !stack.IsEmpty && source?.Adapter != null && _view.CanInstallFromItem(stack.Item);

        public int Accept(in ItemStack stack, IItemDragSource source)
        {
            if (source?.Adapter == null || stack.IsEmpty)
                return 0;

            // One unit only: a modifier slot takes a single upgrade no matter how big the
            // stack the player happened to be dragging.
            ItemStack one = stack.WithAmount(1);
            if (source.Adapter.Remove(in one) != 1)
                return 0;

            CraftingStatus status = _view.TryInstallFromItem(one.Item);
            if (status == CraftingStatus.Success)
                return 1;

            // Rejected after the fact: hand it straight back.
            source.Adapter.Insert(in one);
            return 0;
        }

        public void SetDropHighlight(bool active)
            => CraftingUIStyle.SetBorderColor(_view, active ? _theme.Accent : _theme.Background);
    }
}