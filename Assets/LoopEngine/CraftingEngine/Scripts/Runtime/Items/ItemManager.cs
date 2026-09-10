using System.Collections.Generic;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// Owns every <see cref="ItemDefinition"/> in play and answers lookups by id, index or tag.
    /// Plain C# on purpose: no MonoBehaviour, no singleton, no scene dependency.
    /// The facade in session 9 builds it; nothing else needs to know it exists.
    /// </summary>
    public sealed class ItemManager : DefinitionRegistry<ItemDefinition>
    {
        private static readonly ItemDefinition[] EmptyItems = new ItemDefinition[0];

        private readonly Dictionary<CraftingId, List<ItemDefinition>> _byTag;

        public ItemManager(int capacity = 64) : base(capacity)
        {
            _byTag = new Dictionary<CraftingId, List<ItemDefinition>>(16, CraftingIdComparer.Instance);
        }

        /// <summary>Builds a populated manager in one call.</summary>
        public ItemManager(IReadOnlyList<ItemDefinition> items, List<ItemDefinition> rejected = null)
            : this(items?.Count ?? 0)
        {
            AddRange(items, rejected);
        }

        /// <summary>
        /// Every item carrying the tag, in registration order.
        /// Returns an empty array (never null) when the tag is unused.
        /// The returned list is the live index, so do not mutate it.
        /// </summary>
        public IReadOnlyList<ItemDefinition> GetByTag(in CraftingId tag)
        {
            if (tag.IsValid && _byTag.TryGetValue(tag, out List<ItemDefinition> bucket))
                return bucket;

            return EmptyItems;
        }

        public IReadOnlyList<ItemDefinition> GetByTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return EmptyItems;

            var id = new CraftingId(tag);
            return GetByTag(in id);
        }

        /// <summary>Number of distinct tags currently indexed.</summary>
        public int TagCount => _byTag.Count;

        /// <summary>Iterates the distinct tags. Useful for the editor filter dropdown.</summary>
        public Dictionary<CraftingId, List<ItemDefinition>>.KeyCollection Tags => _byTag.Keys;

        /// <summary>
        /// Resolves an id and reports why it failed, so callers can surface a precise status
        /// instead of a bare false.
        /// </summary>
        public CraftingStatus Resolve(in CraftingId id, out ItemDefinition item)
        {
            if (!id.IsValid)
            {
                item = null;
                return CraftingStatus.InvalidRequest;
            }

            return TryGet(in id, out item) ? CraftingStatus.Success : CraftingStatus.UnknownItem;
        }

        /// <summary>True when the stack references a registered item and carries a positive count.</summary>
        public bool IsValidStack(in ItemStack stack) => !stack.IsEmpty && Contains(stack.Item);

        protected override void OnAdded(ItemDefinition definition, int index)
        {
            CraftingId[] tags = definition.Tags;
            for (int i = 0; i < tags.Length; i++)
            {
                CraftingId tag = tags[i];
                if (!tag.IsValid)
                    continue;

                if (!_byTag.TryGetValue(tag, out List<ItemDefinition> bucket))
                {
                    bucket = new List<ItemDefinition>(4);
                    _byTag.Add(tag, bucket);
                }

                // Guards against an asset listing the same tag twice.
                if (!bucket.Contains(definition))
                    bucket.Add(definition);
            }
        }

        protected override void OnCleared()
        {
            _byTag.Clear();
        }
    }
}