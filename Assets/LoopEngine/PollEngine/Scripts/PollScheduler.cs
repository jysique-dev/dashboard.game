using System;

namespace LoopEngine.PollingEngine
{
    /// <summary>
    /// Many gates, one <c>Update</c>. Register callbacks with their own intervals and tick
    /// the scheduler once per frame; each callback fires at its own rate.
    /// </summary>
    /// <remarks>
    /// Entries live in a flat array indexed by handle, so ticking allocates nothing and a
    /// handle stays valid while its entry exists. Slots freed by <see cref="Remove"/> are
    /// reused, and a generation counter keeps a stale handle from touching the new occupant.
    /// </remarks>
    public sealed class PollScheduler
    {
        private struct Entry
        {
            public Poller Poller;
            public Action Callback;
            public int Generation;
            public bool InUse;
        }

        private Entry[] _entries;
        private int _count;
        private int _generationCounter;

        public PollScheduler(int capacity = 8)
        {
            _entries = new Entry[capacity < 1 ? 1 : capacity];
        }

        /// <summary>Registered callbacks, including paused ones.</summary>
        public int Count
        {
            get
            {
                int active = 0;
                for (int i = 0; i < _count; i++)
                {
                    if (_entries[i].InUse)
                        active++;
                }

                return active;
            }
        }

        /// <summary>Pauses every registered gate at once, for a hidden window.</summary>
        public bool Paused { get; set; }

        /// <summary>
        /// Registers a callback that fires every <paramref name="interval"/> seconds.
        /// </summary>
        /// <returns>A handle for pausing, retiming or removing it.</returns>
        public PollHandle Add(float interval, Action callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            int index = FindFreeIndex();

            // Skip 0: it marks PollHandle.None.
            if (++_generationCounter == 0)
                _generationCounter = 1;

            _entries[index] = new Entry
            {
                Poller = new Poller(interval),
                Callback = callback,
                Generation = _generationCounter,
                InUse = true
            };

            return new PollHandle(index, _generationCounter);
        }

        /// <summary>Unregisters a callback. Safe to call with a stale handle.</summary>
        public bool Remove(in PollHandle handle)
        {
            if (!TryResolve(in handle, out int index))
                return false;

            _entries[index].Callback = null;
            _entries[index].InUse = false;
            _entries[index].Generation = 0;

            // Shrink the scan range when the tail was freed.
            while (_count > 0 && !_entries[_count - 1].InUse)
                _count--;

            return true;
        }

        /// <summary>Suspends or resumes one callback without unregistering it.</summary>
        public bool SetPaused(in PollHandle handle, bool paused)
        {
            if (!TryResolve(in handle, out int index))
                return false;

            _entries[index].Poller.Paused = paused;
            if (!paused)
                _entries[index].Poller.RequestImmediate();

            return true;
        }

        /// <summary>Changes how often a callback fires.</summary>
        public bool SetInterval(in PollHandle handle, float interval)
        {
            if (!TryResolve(in handle, out int index))
                return false;

            _entries[index].Poller.Interval = interval;
            return true;
        }

        /// <summary>Makes a callback fire on the next tick regardless of its interval.</summary>
        public bool RequestImmediate(in PollHandle handle)
        {
            if (!TryResolve(in handle, out int index))
                return false;

            _entries[index].Poller.RequestImmediate();
            return true;
        }

        /// <summary>Drops every registration.</summary>
        public void Clear()
        {
            for (int i = 0; i < _count; i++)
                _entries[i] = default;

            _count = 0;
        }

        /// <summary>
        /// Advances every gate and invokes the ones that came due. Call once per frame.
        /// </summary>
        /// <returns>How many callbacks fired.</returns>
        public int Tick(float deltaSeconds)
        {
            if (Paused)
                return 0;

            int fired = 0;

            for (int i = 0; i < _count; i++)
            {
                if (!_entries[i].InUse)
                    continue;

                // Tick through the array, not through a copy: the accumulator has to persist.
                if (!_entries[i].Poller.Tick(deltaSeconds))
                    continue;

                Action callback = _entries[i].Callback;
                if (callback == null)
                    continue;

                callback();
                fired++;
            }

            return fired;
        }

        private bool TryResolve(in PollHandle handle, out int index)
        {
            index = handle.Index;

            if (!handle.IsValid || index < 0 || index >= _count)
                return false;

            return _entries[index].InUse && _entries[index].Generation == handle.Generation;
        }

        private int FindFreeIndex()
        {
            for (int i = 0; i < _count; i++)
            {
                if (!_entries[i].InUse)
                    return i;
            }

            if (_count == _entries.Length)
                Array.Resize(ref _entries, _entries.Length * 2);

            return _count++;
        }
    }

    /// <summary>
    /// Reference to a registration in a <see cref="PollScheduler"/>. The generation counter
    /// makes a handle to a removed entry detectable instead of silently retiming whatever
    /// took its slot.
    /// </summary>
    public readonly struct PollHandle
    {
        public static readonly PollHandle None = default;

        public readonly int Index;
        public readonly int Generation;

        public PollHandle(int index, int generation)
        {
            Index = index;
            Generation = generation;
        }

        /// <summary>Generation 0 is never handed out, so it marks an empty handle.</summary>
        public bool IsValid => Generation != 0;

        public override string ToString() => IsValid ? "poll " + Index + " gen " + Generation : "<none>";
    }
}