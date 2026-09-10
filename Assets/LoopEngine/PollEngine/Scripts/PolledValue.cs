using System;
using System.Collections.Generic;

namespace LoopEngine.PollingEngine
{
    /// <summary>
    /// A value read from a source that cannot announce its own changes. Samples on a timer
    /// and raises <see cref="Changed"/> only when the result actually differs.
    /// </summary>
    /// <remarks>
    /// This is the answer to any API that exposes a getter and no event: an item container
    /// count, a health value on a third party component, a file timestamp. Polling is the
    /// cost; the change event is what the rest of the code gets to work with.
    /// </remarks>
    public sealed class PolledValue<T>
    {
        private readonly Func<T> _source;
        private readonly IEqualityComparer<T> _comparer;

        private T _current;
        private bool _sampled;

        /// <summary>The gate. Retime or pause it directly.</summary>
        public Poller Poller;

        public PolledValue(Func<T> source, float interval, IEqualityComparer<T> comparer = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _comparer = comparer ?? EqualityComparer<T>.Default;
            Poller = new Poller(interval);
        }

        /// <summary>
        /// Last sampled value. Reading this never samples, so it is safe to call as often as
        /// the UI likes.
        /// </summary>
        public T Current => _current;

        /// <summary>False until the first sample lands.</summary>
        public bool HasValue => _sampled;

        /// <summary>Raised when a sample differs from the previous one. Carries old, then new.</summary>
        public event Action<T, T> Changed;

        /// <summary>
        /// Advances the gate and samples when due.
        /// </summary>
        /// <returns>True when the value changed on this call.</returns>
        public bool Tick(float deltaSeconds) => Poller.Tick(deltaSeconds) && Sample();

        /// <summary>Reads the source right now, ignoring the interval.</summary>
        public bool Sample()
        {
            T next = _source();

            if (_sampled && _comparer.Equals(_current, next))
                return false;

            T previous = _current;
            _current = next;
            _sampled = true;

            Changed?.Invoke(previous, next);
            return true;
        }

        /// <summary>
        /// Adopts a value without raising <see cref="Changed"/>. Use it to seed the initial
        /// state when the UI already drew it once.
        /// </summary>
        public void Prime(T value)
        {
            _current = value;
            _sampled = true;
            Poller.Reset();
        }

        /// <summary>Forgets the last sample, so the next one always reports a change.</summary>
        public void Invalidate()
        {
            _sampled = false;
            Poller.RequestImmediate();
        }
    }
}