using System.Collections.Generic;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// Drives every registered machine from one place. One clock advance, one loop, one
    /// Update in your game instead of N MonoBehaviours each with their own.
    /// </summary>
    /// <remarks>
    /// Not a MonoBehaviour. Session 9's facade wraps it in one; keeping it plain means the
    /// same code runs in unit tests with a <see cref="ManualCraftingClock"/>.
    /// </remarks>
    public sealed class CraftingTicker
    {
        private readonly List<MachineInstance> _machines;
        private readonly ICraftingClock _clock;

        private bool _ticking;
        private readonly List<MachineInstance> _pendingAdds;
        private readonly List<MachineInstance> _pendingRemovals;

        public CraftingTicker(ICraftingClock clock, int capacity = 64)
        {
            _clock = clock ?? new UnityCraftingClock();
            _machines = new List<MachineInstance>(capacity);
            _pendingAdds = new List<MachineInstance>(4);
            _pendingRemovals = new List<MachineInstance>(4);
        }

        public ICraftingClock Clock => _clock;

        public int MachineCount => _machines.Count;

        /// <summary>
        /// Machines currently driven. Order is not stable: removal swaps with the last entry.
        /// </summary>
        public IReadOnlyList<MachineInstance> Machines => _machines;

        /// <summary>
        /// Starts driving a machine. Registering during a tick is deferred to the end of it,
        /// so a job that spawns a machine cannot corrupt the loop.
        /// </summary>
        public bool Register(MachineInstance machine)
        {
            if (machine == null)
                return false;

            if (_ticking)
            {
                if (_machines.Contains(machine) || _pendingAdds.Contains(machine))
                    return false;

                _pendingAdds.Add(machine);
                return true;
            }

            if (_machines.Contains(machine))
                return false;

            _machines.Add(machine);
            return true;
        }

        /// <summary>Stops driving a machine. Its jobs keep their state; they simply stop advancing.</summary>
        public bool Unregister(MachineInstance machine)
        {
            if (machine == null)
                return false;

            if (_ticking)
            {
                if (!_machines.Contains(machine))
                    return false;

                _pendingRemovals.Add(machine);
                return true;
            }

            return RemoveImmediate(machine);
        }

        /// <summary>
        /// Advances the clock and steps every machine once.
        /// Call exactly once per frame. Allocates nothing.
        /// </summary>
        public void Tick()
        {
            _clock.Advance();
            Tick(_clock.DeltaTime);
        }

        /// <summary>
        /// Steps every machine with an explicit delta, bypassing the clock.
        /// Useful for fixed-step simulation and for tests.
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || _machines.Count == 0)
            {
                FlushPending();
                return;
            }

            _ticking = true;

            for (int i = 0; i < _machines.Count; i++)
                _machines[i].Tick(deltaSeconds);

            _ticking = false;
            FlushPending();
        }

        /// <summary>Frees finished slots on every machine. Optional housekeeping pass.</summary>
        public int ReleaseFinishedSlots()
        {
            int released = 0;
            for (int i = 0; i < _machines.Count; i++)
                released += _machines[i].ReleaseFinishedSlots();

            return released;
        }

        /// <summary>Drops every machine. Their jobs are left untouched.</summary>
        public void Clear()
        {
            if (_ticking)
                return;

            _machines.Clear();
            _pendingAdds.Clear();
            _pendingRemovals.Clear();
        }

        private void FlushPending()
        {
            if (_pendingRemovals.Count > 0)
            {
                for (int i = 0; i < _pendingRemovals.Count; i++)
                    RemoveImmediate(_pendingRemovals[i]);

                _pendingRemovals.Clear();
            }

            if (_pendingAdds.Count > 0)
            {
                for (int i = 0; i < _pendingAdds.Count; i++)
                {
                    MachineInstance machine = _pendingAdds[i];
                    if (!_machines.Contains(machine))
                        _machines.Add(machine);
                }

                _pendingAdds.Clear();
            }
        }

        private bool RemoveImmediate(MachineInstance machine)
        {
            int index = _machines.IndexOf(machine);
            if (index < 0)
                return false;

            // Swap with the last element: removal is O(1) and order is not meaningful.
            int last = _machines.Count - 1;
            _machines[index] = _machines[last];
            _machines.RemoveAt(last);
            return true;
        }
    }
}