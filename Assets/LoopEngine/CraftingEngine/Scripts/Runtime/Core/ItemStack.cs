using System;
using System.Runtime.CompilerServices;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// A quantity of one item, by id. Pure runtime value type: no asset references,
    /// no allocations, safe to store in arrays and pass by <c>in</c>.
    /// </summary>
    /// <remarks>
    /// Authoring assets do not serialize this type. Recipes serialize an
    /// <c>ItemAmount</c> (ItemDefinition reference + count, added in session 3) and
    /// bake it down to <see cref="ItemStack"/> when the registry is built.
    /// </remarks>
    public readonly struct ItemStack : IEquatable<ItemStack>
    {
        /// <summary>An empty stack. Equivalent to "nothing".</summary>
        public static readonly ItemStack Empty = default;

        public readonly CraftingId Item;
        public readonly int Amount;

        public ItemStack(CraftingId item, int amount)
        {
            Item = item;
            Amount = amount < 0 ? 0 : amount;
        }

        /// <summary>True when there is no item or the count is zero.</summary>
        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Amount <= 0 || !Item.IsValid;
        }

        /// <summary>Same item, different count.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemStack WithAmount(int amount) => new ItemStack(Item, amount);

        /// <summary>Same item, count increased by <paramref name="delta"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemStack Add(int delta) => new ItemStack(Item, Amount + delta);

        /// <summary>Same item, count reduced by <paramref name="delta"/>, clamped at zero.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemStack Subtract(int delta) => new ItemStack(Item, Amount - delta);

        /// <summary>Multiplies the count, used by batch crafting and yield modifiers.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ItemStack Scale(int multiplier) => new ItemStack(Item, Amount * (multiplier < 0 ? 0 : multiplier));

        /// <summary>True when both stacks hold the same item id, regardless of count.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SameItem(in ItemStack other) => Item.Equals(other.Item);

        public bool Equals(ItemStack other) => Amount == other.Amount && Item.Equals(other.Item);

        public override bool Equals(object obj) => obj is ItemStack other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Item.Hash * 397) ^ Amount;
            }
        }

        public override string ToString() => IsEmpty ? "<empty>" : Amount + " x " + Item.Value;

        public static bool operator ==(ItemStack a, ItemStack b) => a.Equals(b);

        public static bool operator !=(ItemStack a, ItemStack b) => !a.Equals(b);
    }
}