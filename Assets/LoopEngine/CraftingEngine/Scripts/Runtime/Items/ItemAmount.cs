using System;
using UnityEngine;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// Authoring counterpart of <see cref="ItemStack"/>: an asset reference plus a count.
    /// Only assets serialize this. At registration time it is baked down to an
    /// <see cref="ItemStack"/> so the runtime tick never touches a UnityEngine.Object.
    /// </summary>
    [Serializable]
    public struct ItemAmount
    {
        [SerializeField] private ItemDefinition _item;
        [SerializeField, Min(1)] private int _amount;

        public ItemAmount(ItemDefinition item, int amount)
        {
            _item = item;
            _amount = amount < 1 ? 1 : amount;
        }

        public ItemDefinition Item => _item;

        public int Amount => _amount < 0 ? 0 : _amount;

        /// <summary>True when the reference is set, its id is usable and the count is positive.</summary>
        public bool IsValid => _item != null && _amount > 0 && _item.Id.IsValid;

        /// <summary>Bakes to the runtime value type. Returns <see cref="ItemStack.Empty"/> when invalid.</summary>
        public ItemStack ToStack() => IsValid ? new ItemStack(_item.Id, _amount) : ItemStack.Empty;

        /// <summary>Clamps the count to at least 1. Called from the owning asset's OnValidate.</summary>
        internal void ClampAmount()
        {
            if (_amount < 1)
                _amount = 1;
        }

        public override string ToString()
            => _item == null ? "<no item>" : _amount + " x " + _item.RawId;

        /// <summary>
        /// Bakes a list of authored amounts into a runtime array.
        /// Returns null when the source is empty, so callers can share a static empty array.
        /// </summary>
        public static ItemStack[] Bake(ItemAmount[] source)
        {
            if (source == null || source.Length == 0)
                return null;

            var baked = new ItemStack[source.Length];
            for (int i = 0; i < source.Length; i++)
                baked[i] = source[i].ToStack();

            return baked;
        }

        /// <summary>True when every entry in the array is usable.</summary>
        public static bool AllValid(ItemAmount[] source)
        {
            if (source == null)
                return true;

            for (int i = 0; i < source.Length; i++)
            {
                if (!source[i].IsValid)
                    return false;
            }

            return true;
        }
    }
}