using System.Collections.Generic;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>
    /// The set of items the inventory UI is able to see. An <see cref="IItemContainer"/>
    /// cannot list its own contents, so this is the list of questions the UI knows how to ask
    /// it.
    /// </summary>
    /// <remarks>
    /// Shared between every container adapter on purpose: the player inventory, a machine's
    /// input and its output all poll the same ids, so they share one array and one lookup
    /// instead of three copies. An item missing from the catalogue is invisible to the UI even
    /// though the container holds it, which is the honest cost of an interface that does not
    /// enumerate.
    /// </remarks>
    public sealed class ItemCatalogue
    {
        private readonly CraftingId[] _ids;
        private readonly ItemDefinition[] _definitions;
        private readonly Dictionary<CraftingId, int> _index;

        public ItemCatalogue(IReadOnlyList<ItemDefinition> items)
        {
            int source = items?.Count ?? 0;
            var ids = new List<CraftingId>(source);
            var definitions = new List<ItemDefinition>(source);

            _index = new Dictionary<CraftingId, int>(source, CraftingIdComparer.Instance);

            for (int i = 0; i < source; i++)
            {
                ItemDefinition item = items[i];
                if (item == null || !item.Id.IsValid || _index.ContainsKey(item.Id))
                    continue;

                _index.Add(item.Id, ids.Count);
                ids.Add(item.Id);
                definitions.Add(item);
            }

            _ids = ids.ToArray();
            _definitions = definitions.ToArray();
        }

        /// <summary>How many distinct items the UI can track.</summary>
        public int Count => _ids.Length;

        public CraftingId GetId(int index) => _ids[index];

        public ItemDefinition GetDefinition(int index) => _definitions[index];

        /// <summary>Position of an id in the catalogue, or -1 when it is not tracked.</summary>
        public int IndexOf(in CraftingId id)
            => id.IsValid && _index.TryGetValue(id, out int index) ? index : -1;

        public bool Contains(in CraftingId id) => IndexOf(in id) >= 0;
    }
}