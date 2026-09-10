using System.Collections.Generic;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// A dictionary-backed container with an optional per-item cap.
    /// Reference implementation and test double, not the inventory your game should ship:
    /// it has no slots, no ordering and no UI notion. Use it to get running in five minutes,
    /// then implement <see cref="IItemContainer"/> over your real inventory.
    /// </summary>
    public sealed class SimpleItemContainer : IItemContainer
    {
        private readonly Dictionary<CraftingId, int> _amounts;
        private readonly int _capacityPerItem;

        /// <param name="capacityPerItem">Maximum units of any single item. 0 means unlimited.</param>
        public SimpleItemContainer(int capacityPerItem = 0, int expectedItemTypes = 16)
        {
            _capacityPerItem = capacityPerItem < 0 ? 0 : capacityPerItem;
            _amounts = new Dictionary<CraftingId, int>(expectedItemTypes, CraftingIdComparer.Instance);
        }

        /// <summary>Distinct item types currently held.</summary>
        public int DistinctItems => _amounts.Count;

        /// <summary>Item ids currently present. Iterate without allocating.</summary>
        public Dictionary<CraftingId, int>.KeyCollection Items => _amounts.Keys;

        public int GetAmount(in CraftingId item)
            => _amounts.TryGetValue(item, out int amount) ? amount : 0;

        public int GetInsertableAmount(in ItemStack stack)
        {
            if (stack.IsEmpty)
                return 0;

            if (_capacityPerItem == 0)
                return stack.Amount;

            int free = _capacityPerItem - GetAmount(stack.Item);
            if (free <= 0)
                return 0;

            return free < stack.Amount ? free : stack.Amount;
        }

        public int Insert(in ItemStack stack)
        {
            int insertable = GetInsertableAmount(in stack);
            if (insertable <= 0)
                return 0;

            _amounts[stack.Item] = GetAmount(stack.Item) + insertable;
            return insertable;
        }

        public int Remove(in ItemStack stack)
        {
            if (stack.IsEmpty)
                return 0;

            int held = GetAmount(stack.Item);
            if (held <= 0)
                return 0;

            int removed = held < stack.Amount ? held : stack.Amount;
            int left = held - removed;

            if (left > 0)
                _amounts[stack.Item] = left;
            else
                _amounts.Remove(stack.Item);

            return removed;
        }

        /// <summary>Convenience for setup and tests.</summary>
        public void Set(in CraftingId item, int amount)
        {
            if (!item.IsValid)
                return;

            if (amount > 0)
                _amounts[item] = amount;
            else
                _amounts.Remove(item);
        }

        public void Clear() => _amounts.Clear();

        /// <summary>
        /// Every item and its amount, one per line, sorted so two dumps of the same contents
        /// read the same way. Ids only: the container has no registry, so it cannot resolve
        /// display names. Use <see cref="ToString(IDefinitionRegistry{ItemDefinition})"/>
        /// when you want readable names.
        /// </summary>
        /// <remarks>
        /// Debug output. It allocates a string and sorts the keys, so keep it out of Update.
        /// </remarks>
        public override string ToString() => ToString(null);

        /// <summary>
        /// Same dump, resolving display names through a registry.
        /// Ids that the registry does not know are printed raw and marked, which is usually
        /// how you find out an item was removed from the database while a save still holds it.
        /// </summary>
        public string ToString(IDefinitionRegistry<ItemDefinition> items)
        {
            if (_amounts.Count == 0)
                return "SimpleItemContainer: empty";

            var keys = new List<CraftingId>(_amounts.Count);
            foreach (CraftingId id in _amounts.Keys)
                keys.Add(id);

            keys.Sort(CompareByValue);

            var builder = new System.Text.StringBuilder(32 + keys.Count * 24);
            builder.Append("SimpleItemContainer: ")
                   .Append(keys.Count)
                   .Append(" item type(s)");

            if (_capacityPerItem > 0)
                builder.Append(", cap ").Append(_capacityPerItem).Append(" each");

            for (int i = 0; i < keys.Count; i++)
            {
                CraftingId id = keys[i];
                builder.AppendLine().Append("  ");

                if (items != null && items.TryGet(in id, out ItemDefinition item))
                    builder.Append(item.DisplayName);
                else if (items != null)
                    builder.Append(id.Value).Append(" <unregistered>");
                else
                    builder.Append(id.Value);

                builder.Append(" x ").Append(_amounts[id]);
            }

            return builder.ToString();
        }

        private static int CompareByValue(CraftingId a, CraftingId b)
            => string.CompareOrdinal(a.Value, b.Value);
    }
}