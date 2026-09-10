using LoopEngine.PollingEngine;
using System;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>Signature of a per-item change report. Carries the new stack and the old count.</summary>
    public delegate void ItemContainerEntryChanged(
        ItemContainerAdapter source,
        in ItemStack current,
        int previousAmount);

    /// <summary>
    /// Turns an <see cref="IItemContainer"/> into something a UI can list and subscribe to.
    /// </summary>
    /// <remarks>
    /// The container exposes four methods, none of which enumerates and none of which
    /// announces a change. So the adapter asks about every id in the catalogue on a timer and
    /// compares the answers with the previous ones. That is the whole trick, and its cost is
    /// one <c>GetAmount</c> per tracked item per sample.
    /// <para>
    /// The change events it raises look exactly like what an event-driven container would
    /// emit. If one ever grows real notifications, this class changes and nothing above it
    /// does.
    /// </para>
    /// </remarks>
    public sealed class ItemContainerAdapter
    {
        private readonly int[] _amounts;
        private readonly int[] _occupied;

        private int _occupiedCount;
        private int _cursor;

        /// <summary>The sampling gate. Retime or pause it directly.</summary>
        public Poller Poller;

        /// <summary>
        /// Items examined per sample. Zero scans the whole catalogue every time, which is
        /// what you want below a few hundred items. Above that, a smaller number spreads the
        /// work across frames at the cost of noticing a change up to
        /// <c>Count / MaxScansPerSample</c> samples late.
        /// </summary>
        public int MaxScansPerSample;

        public ItemContainerAdapter(IItemContainer container, ItemCatalogue catalogue, float interval = 0.25f)
        {
            Container = container ?? throw new ArgumentNullException(nameof(container));
            Catalogue = catalogue ?? throw new ArgumentNullException(nameof(catalogue));

            _amounts = new int[catalogue.Count];
            _occupied = new int[catalogue.Count];
            Poller = new Poller(interval);
        }

        /// <summary>The wrapped container. Exposed so callers can still use it directly.</summary>
        public IItemContainer Container { get; }

        public ItemCatalogue Catalogue { get; }

        /// <summary>How many tracked items the container currently holds at least one of.</summary>
        public int OccupiedCount => _occupiedCount;

        /// <summary>Samples taken since this adapter was created.</summary>
        public long SampleCount { get; private set; }

        /// <summary>Individual GetAmount calls made. This is the cost of polling, measured.</summary>
        public long ScanCount { get; private set; }

        /// <summary>Samples that found something. The ratio against SampleCount is the waste.</summary>
        public long ChangeCount { get; private set; }

        /// <summary>Zeroes the counters, for measuring one scene or one minute in isolation.</summary>
        public void ResetCounters()
        {
            SampleCount = 0;
            ScanCount = 0;
            ChangeCount = 0;
        }

        /// <summary>Raised once per sample in which anything at all changed.</summary>
        public event Action<ItemContainerAdapter> Changed;

        /// <summary>Raised per item whose count moved. Fires before <see cref="Changed"/>.</summary>
        public event ItemContainerEntryChanged EntryChanged;

        // --- Reads -------------------------------------------------------------------

        /// <summary>Last sampled count for a catalogue position.</summary>
        public int GetAmountAt(int catalogueIndex)
            => catalogueIndex >= 0 && catalogueIndex < _amounts.Length ? _amounts[catalogueIndex] : 0;

        /// <summary>Last sampled count for an id. Zero when the id is not tracked.</summary>
        public int GetAmount(in CraftingId id) => GetAmountAt(Catalogue.IndexOf(in id));

        /// <summary>
        /// The nth non-empty entry, in catalogue order. Stable between samples as long as
        /// nothing changed, which is what lets a grid keep its rows in place.
        /// </summary>
        public ItemStack GetOccupied(int rank)
        {
            if (rank < 0 || rank >= _occupiedCount)
                return ItemStack.Empty;

            int index = _occupied[rank];
            return new ItemStack(Catalogue.GetId(index), _amounts[index]);
        }

        /// <summary>Catalogue position of the nth non-empty entry, or -1.</summary>
        public int GetOccupiedIndex(int rank)
            => rank >= 0 && rank < _occupiedCount ? _occupied[rank] : -1;

        /// <summary>Definition behind the nth non-empty entry, or null.</summary>
        public ItemDefinition GetOccupiedDefinition(int rank)
        {
            int index = GetOccupiedIndex(rank);
            return index < 0 ? null : Catalogue.GetDefinition(index);
        }

        // --- Sampling ----------------------------------------------------------------

        /// <summary>Advances the gate and samples when due.</summary>
        /// <returns>True when something changed on this call.</returns>
        public bool Tick(float deltaSeconds) => Poller.Tick(deltaSeconds) && Sample();

        /// <summary>Reads the container now, ignoring the interval.</summary>
        /// <returns>True when something changed.</returns>
        public bool Sample()
        {
            int total = _amounts.Length;
            if (total == 0)
                return false;

            int budget = MaxScansPerSample > 0 && MaxScansPerSample < total ? MaxScansPerSample : total;
            bool changed = false;

            SampleCount++;
            ScanCount += budget;

            for (int scanned = 0; scanned < budget; scanned++)
            {
                int index = _cursor;

                // Round robin, so a partial budget still covers everything over time.
                _cursor++;
                if (_cursor >= total)
                    _cursor = 0;

                CraftingId id = Catalogue.GetId(index);
                int next = Container.GetAmount(in id);
                int previous = _amounts[index];

                if (next == previous)
                    continue;

                _amounts[index] = next;
                changed = true;
                EntryChanged?.Invoke(this, new ItemStack(id, next), previous);
            }

            if (!changed)
                return false;

            ChangeCount++;
            RebuildOccupied();
            Changed?.Invoke(this);
            return true;
        }

        /// <summary>
        /// Re-reads one item immediately. Used after a write, so the UI does not show a stale
        /// count for up to a full interval after the player moved something.
        /// </summary>
        public bool Resample(in CraftingId id)
        {
            int index = Catalogue.IndexOf(in id);
            if (index < 0)
                return false;

            int next = Container.GetAmount(in id);
            int previous = _amounts[index];
            if (next == previous)
                return false;

            _amounts[index] = next;
            EntryChanged?.Invoke(this, new ItemStack(id, next), previous);
            RebuildOccupied();
            Changed?.Invoke(this);
            return true;
        }

        /// <summary>Forgets every sampled count, so the next sample reports everything.</summary>
        public void Invalidate()
        {
            Array.Clear(_amounts, 0, _amounts.Length);
            _occupiedCount = 0;
            _cursor = 0;
            Poller.RequestImmediate();
        }

        // --- Writes ------------------------------------------------------------------

        /// <summary>How much of a stack would fit. Straight through to the container.</summary>
        public int GetInsertableAmount(in ItemStack stack) => Container.GetInsertableAmount(in stack);

        /// <summary>Inserts and immediately re-reads the affected item.</summary>
        public int Insert(in ItemStack stack)
        {
            int inserted = Container.Insert(in stack);
            if (inserted > 0)
                Resample(stack.Item);

            return inserted;
        }

        /// <summary>Removes and immediately re-reads the affected item.</summary>
        public int Remove(in ItemStack stack)
        {
            int removed = Container.Remove(in stack);
            if (removed > 0)
                Resample(stack.Item);

            return removed;
        }

        /// <summary>
        /// Moves as much of a stack as fits into another container, removing only what the
        /// destination actually accepted. Both sides are re-read.
        /// </summary>
        /// <returns>How many units moved.</returns>
        public int TransferTo(ItemContainerAdapter destination, in ItemStack stack)
        {
            if (destination == null || destination == this || stack.IsEmpty)
                return 0;

            int available = GetAmount(stack.Item);
            if (available <= 0)
                return 0;

            int wanted = stack.Amount < available ? stack.Amount : available;
            ItemStack attempt = stack.WithAmount(wanted);

            // Ask before taking: a partial insert with the source already emptied would lose
            // items, and the container contract allows partial inserts.
            int fits = destination.GetInsertableAmount(in attempt);
            if (fits <= 0)
                return 0;

            if (fits < wanted)
                attempt = stack.WithAmount(fits);

            int taken = Remove(in attempt);
            if (taken <= 0)
                return 0;

            ItemStack moving = stack.WithAmount(taken);
            int placed = destination.Insert(in moving);

            // The destination changed its mind between the question and the insert.
            if (placed < taken)
                Insert(stack.WithAmount(taken - placed));

            return placed;
        }

        private void RebuildOccupied()
        {
            _occupiedCount = 0;
            for (int i = 0; i < _amounts.Length; i++)
            {
                if (_amounts[i] > 0)
                    _occupied[_occupiedCount++] = i;
            }
        }
    }
}