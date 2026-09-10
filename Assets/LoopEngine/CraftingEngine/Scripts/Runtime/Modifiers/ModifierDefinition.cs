using System;
using UnityEngine;

namespace LoopEngine.CraftingEngine
{
    /// <summary>Machine properties an upgrade can change.</summary>
    public enum MachineStat
    {
        /// <summary>Work rate. Higher means shorter craft times.</summary>
        Speed = 0,

        /// <summary>Output quantity multiplier applied to every produced stack.</summary>
        Yield = 1
    }

    /// <summary>How a modifier entry combines with the others.</summary>
    public enum StatOperation
    {
        /// <summary>Summed with every other additive entry, then applied as (1 + total).</summary>
        Additive = 0,

        /// <summary>Multiplied in directly, after all additive entries.</summary>
        Multiplicative = 1
    }

    /// <summary>One stat change contributed by a modifier.</summary>
    [Serializable]
    public struct StatModifierEntry
    {
        [SerializeField] private MachineStat _stat;
        [SerializeField] private StatOperation _operation;
        [SerializeField] private float _value;

        public StatModifierEntry(MachineStat stat, StatOperation operation, float value)
        {
            _stat = stat;
            _operation = operation;
            _value = value;
        }

        public MachineStat Stat => _stat;

        public StatOperation Operation => _operation;

        /// <summary>
        /// For additive entries this is the fraction added: 0.25 means +25%.
        /// For multiplicative entries this is the factor itself: 1.5 means x1.5.
        /// </summary>
        public float Value => _value;
    }

    /// <summary>
    /// An upgrade installed in a machine: a speed module, a yield module, a catalyst that
    /// adds a by-product.
    /// </summary>
    /// <remarks>
    /// The modifier points at the item that carries it, not the other way round, so
    /// <see cref="ItemDefinition"/> stays a plain item with no knowledge of upgrades.
    /// Installing a modifier does not remove its item from any container; take the item out
    /// of the player's inventory yourself, then call
    /// <see cref="MachineInstance.InstallModifier"/>.
    /// </remarks>
    [CreateAssetMenu(fileName = "Modifier_", menuName = LoopRoutes.CraftRoute +"/Modifier", order = 140)]
    public sealed class ModifierDefinition : CraftingDefinition
    {
        private static readonly ItemStack[] EmptyStacks = new ItemStack[0];

        [SerializeField, Tooltip("The item the player installs to get this modifier. Optional: leave empty for built-in upgrades.")]
        private ItemDefinition _carrierItem;

        [SerializeField, Tooltip("Stat changes contributed by this modifier.")]
        private StatModifierEntry[] _entries = Array.Empty<StatModifierEntry>();

        [SerializeField, Tooltip("Extra items produced on every completed craft, on top of the recipe's own outputs.")]
        private ItemAmount[] _bonusOutputs = Array.Empty<ItemAmount>();

        [SerializeField, Tooltip("Machine categories that accept this modifier. Empty means any machine.")]
        private MachineCategoryDefinition[] _allowedCategories = Array.Empty<MachineCategoryDefinition>();

        [SerializeField, Min(1), Tooltip("How many copies of this modifier a single machine may hold.")]
        private int _maxPerMachine = 1;

        [NonSerialized] private ItemStack[] _bakedBonusOutputs;

        public ItemDefinition CarrierItem => _carrierItem;

        /// <summary>The carrier item's id, or <see cref="CraftingId.None"/> for built-in upgrades.</summary>
        public CraftingId CarrierItemId => _carrierItem != null ? _carrierItem.Id : CraftingId.None;

        public StatModifierEntry[] Entries => _entries ?? Array.Empty<StatModifierEntry>();

        /// <summary>Runtime bonus outputs. Built once, never null.</summary>
        public ItemStack[] BonusOutputs
        {
            get
            {
                if (_bakedBonusOutputs == null)
                    _bakedBonusOutputs = ItemAmount.Bake(_bonusOutputs) ?? EmptyStacks;

                return _bakedBonusOutputs;
            }
        }

        public int MaxPerMachine => _maxPerMachine < 1 ? 1 : _maxPerMachine;

        public bool HasBonusOutputs => BonusOutputs.Length > 0;

        /// <summary>True when this modifier may be installed in that machine.</summary>
        public bool AppliesTo(MachineDefinition machine)
        {
            if (machine == null)
                return false;

            if (_allowedCategories == null || _allowedCategories.Length == 0)
                return true;

            for (int i = 0; i < _allowedCategories.Length; i++)
            {
                MachineCategoryDefinition category = _allowedCategories[i];
                if (category == null)
                    continue;

                CraftingId id = category.Id;
                if (machine.HasCategory(in id))
                    return true;
            }

            return false;
        }

        /// <summary>A modifier that changes nothing and adds nothing is dead content.</summary>
        protected override bool ValidateContent()
        {
            bool hasEntries = _entries != null && _entries.Length > 0;
            bool hasBonus = _bonusOutputs != null && _bonusOutputs.Length > 0;

            if (!hasEntries && !hasBonus)
                return false;

            return ItemAmount.AllValid(_bonusOutputs);
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            if (_maxPerMachine < 1)
                _maxPerMachine = 1;

            if (_bonusOutputs != null)
            {
                for (int i = 0; i < _bonusOutputs.Length; i++)
                    _bonusOutputs[i].ClampAmount();
            }

            _bakedBonusOutputs = null;
        }

#if UNITY_EDITOR
        internal void EditorSetContent(
            ItemDefinition carrierItem,
            StatModifierEntry[] entries,
            ItemAmount[] bonusOutputs,
            MachineCategoryDefinition[] allowedCategories,
            int maxPerMachine)
        {
            _carrierItem = carrierItem;
            _entries = entries ?? Array.Empty<StatModifierEntry>();
            _bonusOutputs = bonusOutputs ?? Array.Empty<ItemAmount>();
            _allowedCategories = allowedCategories ?? Array.Empty<MachineCategoryDefinition>();
            _maxPerMachine = maxPerMachine < 1 ? 1 : maxPerMachine;
            _bakedBonusOutputs = null;
        }
#endif
    }
}