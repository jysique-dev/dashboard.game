using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// Shared storage and lookup for every definition manager.
    /// Keeps a dense list (stable index order) plus an id to index map, so callers can
    /// use either an id (O(1) hash lookup) or an index (O(1) array access) without
    /// duplicating the definitions.
    /// </summary>
    /// <remarks>
    /// Not thread safe. Build it once during initialisation and treat it as read-only
    /// afterwards, or guard mutation yourself.
    /// </remarks>
    public abstract class DefinitionRegistry<T> : IDefinitionRegistry<T> where T : CraftingDefinition
    {
        private readonly Dictionary<CraftingId, int> _indexById;
        private readonly List<T> _definitions;

        protected DefinitionRegistry(int capacity = 64)
        {
            if (capacity < 0)
                capacity = 0;

            _definitions = new List<T>(capacity);
            _indexById = new Dictionary<CraftingId, int>(capacity, CraftingIdComparer.Instance);
        }

        public int Count => _definitions.Count;

        /// <summary>Registration order view. Indices stay valid until <see cref="Clear"/>.</summary>
        public IReadOnlyList<T> All => _definitions;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(in CraftingId id, out T definition)
        {
            if (_indexById.TryGetValue(id, out int index))
            {
                definition = _definitions[index];
                return true;
            }

            definition = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(in CraftingId id) => _indexById.ContainsKey(id);

        /// <summary>Resolves an id to its dense index, for hot loops that cache handles.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetIndex(in CraftingId id, out int index) => _indexById.TryGetValue(id, out index);

        /// <summary>Direct index access. Throws only on a genuinely out of range index.</summary>
        public T GetAt(int index) => _definitions[index];

        /// <summary>Convenience lookup from a raw authoring string. Hashes on every call.</summary>
        public bool TryGet(string rawId, out T definition)
        {
            if (string.IsNullOrEmpty(rawId))
            {
                definition = null;
                return false;
            }

            var id = new CraftingId(rawId);
            return TryGet(in id, out definition);
        }

        /// <summary>
        /// Registers a definition. Rejects nulls, blank ids, content that fails its own
        /// validation, and ids that are already taken (first registration wins).
        /// </summary>
        public CraftingStatus Add(T definition)
        {
            if (definition == null)
                return CraftingStatus.InvalidRequest;

            CraftingId id = definition.Id;
            if (!id.IsValid || !definition.IsValid)
                return CraftingStatus.InvalidRequest;

            if (_indexById.ContainsKey(id))
                return CraftingStatus.DuplicateId;

            int index = _definitions.Count;
            _definitions.Add(definition);
            _indexById.Add(id, index);
            OnAdded(definition, index);
            return CraftingStatus.Success;
        }

        /// <summary>
        /// Bulk registration. Returns how many were accepted.
        /// Pass <paramref name="rejected"/> to collect what was skipped, for the editor validator.
        /// </summary>
        public int AddRange(IReadOnlyList<T> definitions, List<T> rejected = null)
        {
            if (definitions == null)
                return 0;

            int added = 0;
            for (int i = 0; i < definitions.Count; i++)
            {
                if (Add(definitions[i]) == CraftingStatus.Success)
                    added++;
                else
                    rejected?.Add(definitions[i]);
            }

            return added;
        }

        /// <summary>Drops everything. Previously handed out indices become meaningless.</summary>
        public void Clear()
        {
            _definitions.Clear();
            _indexById.Clear();
            OnCleared();
        }

        /// <summary>Hook for subclasses that maintain secondary indices.</summary>
        protected virtual void OnAdded(T definition, int index) { }

        /// <summary>Hook for subclasses to drop their secondary indices.</summary>
        protected virtual void OnCleared() { }
    }
}