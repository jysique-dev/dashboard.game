using System.Collections.Generic;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// Holds every <see cref="MachineCategoryDefinition"/>.
    /// Categories carry no behaviour, so this is a plain lookup with no secondary indices.
    /// It exists because the JSON importer and the editor validator need to resolve a
    /// category by id without walking the asset database.
    /// </summary>
    public sealed class MachineCategoryManager : DefinitionRegistry<MachineCategoryDefinition>
    {
        public MachineCategoryManager(int capacity = 16) : base(capacity) { }

        public MachineCategoryManager(
            IReadOnlyList<MachineCategoryDefinition> categories,
            List<MachineCategoryDefinition> rejected = null)
            : this(categories?.Count ?? 0)
        {
            AddRange(categories, rejected);
        }

        public CraftingStatus Resolve(in CraftingId id, out MachineCategoryDefinition category)
        {
            if (!id.IsValid)
            {
                category = null;
                return CraftingStatus.InvalidRequest;
            }

            return TryGet(in id, out category) ? CraftingStatus.Success : CraftingStatus.UnknownCategory;
        }
    }
}