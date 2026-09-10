namespace LoopEngine.PollingEngine
{
    /// <summary>
    /// A fixed-rate gate. Feed it a delta and it tells you whether this is a frame where you
    /// should do the expensive thing.
    /// </summary>
    /// <remarks>
    /// A struct with no dependency on Unity, so it can be unit tested with a fake delta and
    /// copied into a job or a plain C# service without dragging an engine reference along.
    /// Copy semantics are deliberate: two copies keep separate accumulators.
    /// </remarks>
    public struct Poller
    {
        private float _accumulated;

        /// <summary>Seconds between samples. Zero or less samples on every call.</summary>
        public float Interval;

        /// <summary>While true, <see cref="Tick"/> never reports a sample.</summary>
        public bool Paused;

        public Poller(float interval)
        {
            Interval = interval;
            Paused = false;
            _accumulated = 0f;
        }

        /// <summary>Every call. For things that must follow the frame exactly.</summary>
        public static Poller EveryFrame => new Poller(0f);

        /// <summary>Roughly 30 samples a second. Smooth enough for a moving bar.</summary>
        public static Poller Fast => new Poller(1f / 30f);

        /// <summary>Four samples a second. Enough for counts and availability checks.</summary>
        public static Poller Slow => new Poller(0.25f);

        /// <summary>Once a second. For summaries and totals nobody watches closely.</summary>
        public static Poller Lazy => new Poller(1f);

        /// <summary>Samples per second, or zero when sampling every frame.</summary>
        public float Rate => Interval <= 0f ? 0f : 1f / Interval;

        /// <summary>How far the accumulator is toward the next sample, 0 to 1.</summary>
        public float Fill => Interval <= 0f ? 1f : _accumulated / Interval;

        /// <summary>
        /// Advances the gate. Returns true on the frames the caller should sample.
        /// </summary>
        public bool Tick(float deltaSeconds)
        {
            if (Paused)
                return false;

            if (Interval <= 0f)
                return true;

            if (deltaSeconds > 0f)
                _accumulated += deltaSeconds;

            if (_accumulated < Interval)
                return false;

            // Subtract rather than zero, so a heavy frame does not swallow a sample.
            // Then clamp, so returning from a long stall does not fire a burst of them.
            _accumulated -= Interval;
            if (_accumulated > Interval)
                _accumulated = Interval;

            return true;
        }

        /// <summary>Makes the next <see cref="Tick"/> report a sample no matter the delta.</summary>
        public void RequestImmediate() => _accumulated = Interval <= 0f ? 0f : Interval;

        /// <summary>Drops accumulated time. The next sample is a full interval away.</summary>
        public void Reset() => _accumulated = 0f;
    }
}