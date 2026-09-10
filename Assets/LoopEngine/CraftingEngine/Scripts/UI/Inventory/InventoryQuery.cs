using System;
using System.Collections.Generic;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>How a filtered inventory is ordered.</summary>
    public enum InventorySort
    {
        /// <summary>Catalogue order. Stable, and the cheapest: no sorting happens at all.</summary>
        Catalogue = 0,

        /// <summary>Alphabetical by displayed name.</summary>
        Name = 1,

        /// <summary>Biggest stacks first.</summary>
        AmountDescending = 2,

        /// <summary>Smallest stacks first, for spotting what is running out.</summary>
        AmountAscending = 3
    }

    /// <summary>
    /// Which of a container's items a grid shows and in what order. Produces a list of
    /// catalogue indices; the grid draws that list instead of the container directly.
    /// </summary>
    /// <remarks>
    /// Rebuilt only when the filter changes or the container reports a change, never per
    /// frame. The comparisons are cached instance methods rather than lambdas, so sorting a
    /// filtered inventory allocates nothing beyond the result list's own growth.
    /// </remarks>
    public sealed class InventoryQuery
    {
        private readonly Comparison<int> _byName;
        private readonly Comparison<int> _byAmountDescending;
        private readonly Comparison<int> _byAmountAscending;

        private ItemContainerAdapter _adapter;
        private ICraftingDisplayProvider _display;
        private ItemCatalogue _catalogue;

        private string _search = string.Empty;
        private CraftingId _tag;
        private InventorySort _sort;

        public InventoryQuery()
        {
            _byName = CompareByName;
            _byAmountDescending = CompareByAmountDescending;
            _byAmountAscending = CompareByAmountAscending;
        }

        /// <summary>Raised whenever a criterion changes and the grid should rebuild.</summary>
        public event Action Changed;

        /// <summary>Case-insensitive substring match against the displayed name.</summary>
        public string Search
        {
            get => _search;
            set
            {
                string next = value ?? string.Empty;
                if (string.Equals(_search, next, StringComparison.Ordinal))
                    return;

                _search = next;
                Changed?.Invoke();
            }
        }

        /// <summary>Only items carrying this tag. Invalid id means no tag filter.</summary>
        public CraftingId Tag
        {
            get => _tag;
            set
            {
                if (_tag.Equals(value))
                    return;

                _tag = value;
                Changed?.Invoke();
            }
        }

        public InventorySort Sort
        {
            get => _sort;
            set
            {
                if (_sort == value)
                    return;

                _sort = value;
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// Extra rule applied on top of the built-in ones. Receives the definition and the
        /// current amount; return false to hide the entry.
        /// </summary>
        public Func<ItemDefinition, int, bool> CustomFilter { get; set; }

        /// <summary>True when nothing is being filtered out.</summary>
        public bool IsEmpty => _search.Length == 0 && !_tag.IsValid && CustomFilter == null;

        /// <summary>Clears every criterion, leaving the sort alone.</summary>
        public void Clear()
        {
            if (IsEmpty)
                return;

            _search = string.Empty;
            _tag = CraftingId.None;
            CustomFilter = null;
            Changed?.Invoke();
        }

        /// <summary>Forces subscribers to rebuild, e.g. after changing the custom filter.</summary>
        public void Invalidate() => Changed?.Invoke();

        /// <summary>
        /// Fills <paramref name="results"/> with the catalogue indices to draw, in order.
        /// </summary>
        /// <returns>How many entries were written.</returns>
        public int Build(
            ItemContainerAdapter adapter,
            ICraftingDisplayProvider display,
            List<int> results)
        {
            if (results == null)
                return 0;

            results.Clear();

            if (adapter == null || display == null)
                return 0;

            _adapter = adapter;
            _display = display;
            _catalogue = adapter.Catalogue;

            int occupied = adapter.OccupiedCount;
            for (int rank = 0; rank < occupied; rank++)
            {
                int index = adapter.GetOccupiedIndex(rank);
                if (index < 0)
                    continue;

                if (!Passes(index))
                    continue;

                results.Add(index);
            }

            switch (_sort)
            {
                case InventorySort.Name:
                    results.Sort(_byName);
                    break;

                case InventorySort.AmountDescending:
                    results.Sort(_byAmountDescending);
                    break;

                case InventorySort.AmountAscending:
                    results.Sort(_byAmountAscending);
                    break;
            }

            return results.Count;
        }

        private bool Passes(int index)
        {
            ItemDefinition definition = _catalogue.GetDefinition(index);

            if (_tag.IsValid && (definition == null || !definition.HasTag(in _tag)))
                return false;

            if (_search.Length > 0)
            {
                CraftingId id = _catalogue.GetId(index);
                string name = _display.GetItemName(in id);

                if (name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            if (CustomFilter != null && !CustomFilter(definition, _adapter.GetAmountAt(index)))
                return false;

            return true;
        }

        private int CompareByName(int a, int b)
        {
            CraftingId first = _catalogue.GetId(a);
            CraftingId second = _catalogue.GetId(b);

            return string.Compare(
                _display.GetItemName(in first),
                _display.GetItemName(in second),
                StringComparison.CurrentCultureIgnoreCase);
        }

        private int CompareByAmountDescending(int a, int b)
        {
            int result = _adapter.GetAmountAt(b).CompareTo(_adapter.GetAmountAt(a));

            // Catalogue order as the tie break, so equal stacks never swap places between
            // two refreshes and make the grid flicker.
            return result != 0 ? result : a.CompareTo(b);
        }

        private int CompareByAmountAscending(int a, int b)
        {
            int result = _adapter.GetAmountAt(a).CompareTo(_adapter.GetAmountAt(b));
            return result != 0 ? result : a.CompareTo(b);
        }
    }
}