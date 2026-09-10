using UnityEngine.UIElements;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>Where a dragged stack came from.</summary>
    public interface IItemDragSource
    {
        /// <summary>The container the stack is being taken out of.</summary>
        ItemContainerAdapter Adapter { get; }

        /// <summary>False while the source is read only, e.g. a machine's input during a job.</summary>
        bool AllowsDragOut { get; }
    }

    /// <summary>Somewhere a dragged stack can land.</summary>
    /// <remarks>
    /// Hit testing is done against <see cref="Element"/>'s world bounds, so a target is any
    /// rectangle on screen: a grid, an upgrade section, a trash can, a machine in the world
    /// projected onto the panel.
    /// </remarks>
    public interface IItemDropTarget
    {
        /// <summary>The rectangle that accepts the drop.</summary>
        VisualElement Element { get; }

        /// <summary>Whether this stack from this source would be accepted at all.</summary>
        bool CanAccept(in ItemStack stack, IItemDragSource source);

        /// <summary>Takes the stack. Returns how many units actually moved.</summary>
        int Accept(in ItemStack stack, IItemDragSource source);

        /// <summary>Draws or clears the hover feedback while a drag is over this target.</summary>
        void SetDropHighlight(bool active);
    }

    /// <summary>How much of a stack an action moves.</summary>
    public enum TransferAmount
    {
        /// <summary>Everything the source holds of that item.</summary>
        All = 0,

        /// <summary>Half, rounded up, so one unit still moves one.</summary>
        Half = 1,

        /// <summary>A single unit.</summary>
        One = 2
    }

    /// <summary>Stack maths shared by dragging, quick moves and keyboard shortcuts.</summary>
    public static class InventoryTransfer
    {
        /// <summary>Resolves how many units an amount mode means for a given total.</summary>
        public static int Resolve(TransferAmount amount, int available)
        {
            if (available <= 0)
                return 0;

            switch (amount)
            {
                case TransferAmount.One:
                    return 1;

                case TransferAmount.Half:
                    // Rounded up, so a stack of one still moves its one unit.
                    return (available + 1) / 2;

                default:
                    return available;
            }
        }

        /// <summary>Reads the source and builds the stack an action would move.</summary>
        public static ItemStack Slice(ItemContainerAdapter source, in CraftingId item, TransferAmount amount)
        {
            if (source == null || !item.IsValid)
                return ItemStack.Empty;

            int available = source.GetAmount(in item);
            int wanted = Resolve(amount, available);
            return wanted <= 0 ? ItemStack.Empty : new ItemStack(item, wanted);
        }

        /// <summary>
        /// Builds the stack an action would move out of one drawn cell, clamped by what the
        /// container actually holds. With split stacks a cell shows a slice of a holding, so
        /// dragging it must move that slice, not everything of that type.
        /// </summary>
        public static ItemStack SliceFrom(
            ItemContainerAdapter source,
            in ItemStack cellStack,
            TransferAmount amount)
        {
            if (source == null || cellStack.IsEmpty)
                return ItemStack.Empty;

            int available = source.GetAmount(cellStack.Item);
            int cap = cellStack.Amount < available ? cellStack.Amount : available;
            int wanted = Resolve(amount, cap);

            return wanted <= 0 ? ItemStack.Empty : new ItemStack(cellStack.Item, wanted);
        }

        /// <summary>Moves part of an item from one container to another.</summary>
        /// <returns>How many units moved.</returns>
        public static int Move(
            ItemContainerAdapter source,
            ItemContainerAdapter destination,
            in CraftingId item,
            TransferAmount amount)
        {
            ItemStack stack = Slice(source, in item, amount);
            return stack.IsEmpty ? 0 : source.TransferTo(destination, in stack);
        }

        /// <summary>Amount mode implied by the modifier keys held during a pointer event.</summary>
        public static TransferAmount FromModifiers(bool shift, bool control, bool alt)
        {
            if (control || alt)
                return TransferAmount.One;

            return shift ? TransferAmount.Half : TransferAmount.All;
        }
    }
}