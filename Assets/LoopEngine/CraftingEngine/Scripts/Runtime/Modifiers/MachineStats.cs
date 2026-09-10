namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// Final stat values for one machine instance, after every installed modifier.
    /// </summary>
    public readonly struct MachineStats
    {
        /// <summary>A machine with nothing installed.</summary>
        public static readonly MachineStats Neutral = new MachineStats(1f, 1f);

        /// <summary>Work rate. Craft time is divided by this.</summary>
        public readonly float Speed;

        /// <summary>Output quantity factor. Applied to every produced stack.</summary>
        public readonly float Yield;

        public MachineStats(float speed, float yield)
        {
            Speed = speed < 0.01f ? 0.01f : speed;
            Yield = yield < 0f ? 0f : yield;
        }

        /// <summary>True when these stats change nothing, so the fast paths can skip work.</summary>
        public bool IsNeutral => Speed == 1f && Yield == 1f;
    }

    /// <summary>
    /// Accumulates stat entries and resolves them into a <see cref="MachineStats"/>.
    /// A struct so recalculating allocates nothing.
    /// </summary>
    /// <remarks>
    /// Resolution order is fixed: additive entries are summed and applied as (1 + total),
    /// then multiplicative entries are applied on top.
    /// Two +50% modules give x2.0, not x2.25. A +50% module and a x1.5 module give x2.25.
    /// </remarks>
    public struct StatAccumulator
    {
        private float _speedAdditive;
        private float _speedMultiplicative;
        private float _yieldAdditive;
        private float _yieldMultiplicative;
        private bool _started;

        /// <summary>Resets to the neutral state. Call before accumulating.</summary>
        public void Begin()
        {
            _speedAdditive = 0f;
            _speedMultiplicative = 1f;
            _yieldAdditive = 0f;
            _yieldMultiplicative = 1f;
            _started = true;
        }

        /// <summary>Folds in every entry of a modifier.</summary>
        public void Add(ModifierDefinition modifier)
        {
            if (modifier == null)
                return;

            if (!_started)
                Begin();

            StatModifierEntry[] entries = modifier.Entries;
            for (int i = 0; i < entries.Length; i++)
                Add(entries[i]);
        }

        public void Add(in StatModifierEntry entry)
        {
            if (!_started)
                Begin();

            switch (entry.Stat)
            {
                case MachineStat.Speed:
                    if (entry.Operation == StatOperation.Additive)
                        _speedAdditive += entry.Value;
                    else
                        _speedMultiplicative *= entry.Value;
                    break;

                case MachineStat.Yield:
                    if (entry.Operation == StatOperation.Additive)
                        _yieldAdditive += entry.Value;
                    else
                        _yieldMultiplicative *= entry.Value;
                    break;
            }
        }

        /// <summary>Applies an extra factor outside the modifier system, such as a manual bonus.</summary>
        public void MultiplySpeed(float factor)
        {
            if (!_started)
                Begin();

            _speedMultiplicative *= factor;
        }

        public MachineStats Resolve()
        {
            if (!_started)
                return MachineStats.Neutral;

            float speed = (1f + _speedAdditive) * _speedMultiplicative;
            float yield = (1f + _yieldAdditive) * _yieldMultiplicative;
            return new MachineStats(speed, yield);
        }
    }
}