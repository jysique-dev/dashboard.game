namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// Anything the crafting system can pull items from or push items into:
    /// a player inventory, a machine buffer, a chest, a debug void.
    /// The host project implements this; the crafting system never knows what
    /// an inventory actually is.
    /// </summary>
    /// <remarks>
    /// Implementations must be synchronous and side-effect free for the query
    /// methods (<see cref="GetAmount"/>, <see cref="CanInsert"/>), because the
    /// crafting system calls them during validation before committing anything.
    /// </remarks>
    public interface IItemContainer
    {
        /// <summary>How many units of <paramref name="item"/> this container currently holds.</summary>
        int GetAmount(in CraftingId item);

        /// <summary>
        /// How many units of <paramref name="stack"/> could be inserted right now.
        /// Return <c>stack.Amount</c> when everything fits, 0 when nothing does.
        /// Must not modify the container.
        /// </summary>
        int GetInsertableAmount(in ItemStack stack);

        /// <summary>
        /// Inserts up to <paramref name="stack"/>. Returns the amount actually inserted,
        /// which may be less than requested. Partial insertion is allowed; the crafting
        /// system validates capacity beforehand when it needs an all-or-nothing guarantee.
        /// </summary>
        int Insert(in ItemStack stack);

        /// <summary>
        /// Removes up to <paramref name="stack"/>. Returns the amount actually removed.
        /// </summary>
        int Remove(in ItemStack stack);
    }

    public static class ItemContainerExtensions
    {
        /// <summary>True when the container holds at least the requested amount.</summary>
        public static bool Has(this IItemContainer container, in ItemStack stack)
            => stack.IsEmpty || container.GetAmount(stack.Item) >= stack.Amount;

        /// <summary>True when the whole stack fits without partial insertion.</summary>
        public static bool CanInsertAll(this IItemContainer container, in ItemStack stack)
            => stack.IsEmpty || container.GetInsertableAmount(stack) >= stack.Amount;
    }
}