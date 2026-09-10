using System.Collections.Generic;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// Owns every <see cref="ModifierDefinition"/> and answers the one question the game
    /// actually asks: "the player dropped this item into the upgrade slot, what does it do?"
    /// </summary>
    public sealed class ModifierManager : DefinitionRegistry<ModifierDefinition>
    {
        private static readonly ModifierDefinition[] EmptyModifiers = new ModifierDefinition[0];

        private readonly Dictionary<CraftingId, List<ModifierDefinition>> _byCarrierItem;

        public ModifierManager(int capacity = 32) : base(capacity)
        {
            _byCarrierItem = new Dictionary<CraftingId, List<ModifierDefinition>>(capacity, CraftingIdComparer.Instance);
        }

        public ModifierManager(IReadOnlyList<ModifierDefinition> modifiers, List<ModifierDefinition> rejected = null)
            : this(modifiers?.Count ?? 0)
        {
            AddRange(modifiers, rejected);
        }

        /// <summary>
        /// Modifiers carried by an item. Usually one, but nothing stops a designer from
        /// making the same item mean different things in different machines.
        /// </summary>
        public IReadOnlyList<ModifierDefinition> GetByCarrierItem(in CraftingId itemId)
        {
            if (itemId.IsValid && _byCarrierItem.TryGetValue(itemId, out List<ModifierDefinition> bucket))
                return bucket;

            return EmptyModifiers;
        }

        /// <summary>
        /// The modifier an item carries for a specific machine, or null when the item is not
        /// an upgrade or is not accepted there.
        /// </summary>
        public ModifierDefinition FindFor(in CraftingId itemId, MachineDefinition machine)
        {
            IReadOnlyList<ModifierDefinition> candidates = GetByCarrierItem(in itemId);
            for (int i = 0; i < candidates.Count; i++)
            {
                ModifierDefinition modifier = candidates[i];
                if (modifier.AppliesTo(machine))
                    return modifier;
            }

            return null;
        }

        public CraftingStatus Resolve(in CraftingId id, out ModifierDefinition modifier)
        {
            if (!id.IsValid)
            {
                modifier = null;
                return CraftingStatus.InvalidRequest;
            }

            return TryGet(in id, out modifier) ? CraftingStatus.Success : CraftingStatus.ModifierRejected;
        }

        protected override void OnAdded(ModifierDefinition definition, int index)
        {
            CraftingId carrier = definition.CarrierItemId;
            if (!carrier.IsValid)
                return;

            if (!_byCarrierItem.TryGetValue(carrier, out List<ModifierDefinition> bucket))
            {
                bucket = new List<ModifierDefinition>(2);
                _byCarrierItem.Add(carrier, bucket);
            }

            if (!bucket.Contains(definition))
                bucket.Add(definition);
        }

        protected override void OnCleared()
        {
            _byCarrierItem.Clear();
        }
    }
}