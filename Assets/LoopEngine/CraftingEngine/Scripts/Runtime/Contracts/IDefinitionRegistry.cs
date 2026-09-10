using System.Collections.Generic;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// Read-only lookup surface shared by ItemManager, RecipeManager and MachineManager.
    /// Consumers depend on this, never on the concrete manager, so any of the three can
    /// be swapped for a test double or a streamed/addressable source later.
    /// </summary>
    /// <typeparam name="T">The definition type this registry holds.</typeparam>
    public interface IDefinitionRegistry<T> where T : CraftingDefinition
    {
        /// <summary>Number of registered definitions.</summary>
        int Count { get; }

        /// <summary>O(1) lookup by id. Returns false and null when absent.</summary>
        bool TryGet(in CraftingId id, out T definition);

        /// <summary>O(1) existence check without touching the out parameter.</summary>
        bool Contains(in CraftingId id);

        /// <summary>
        /// Stable, index-addressable view of everything registered.
        /// Backed by a list built once at registration; safe to iterate without allocating.
        /// </summary>
        IReadOnlyList<T> All { get; }
    }
}