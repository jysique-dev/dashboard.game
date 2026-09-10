using UnityEngine;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// The one and only class in the runtime assembly that reads <see cref="Time"/>.
    /// Uses scaled delta time so pausing the game pauses crafting.
    /// </summary>
    public sealed class UnityCraftingClock : ICraftingClock
    {
        private readonly float _maxDelta;
        private double _now;
        private float _delta;

        /// <param name="maxDelta">
        /// Upper bound applied to a single frame's delta. Protects long stalls
        /// (level load, editor pause, alt-tab) from completing every queued job at once.
        /// </param>
        public UnityCraftingClock(float maxDelta = 0.25f)
        {
            _maxDelta = maxDelta <= 0f ? 0.25f : maxDelta;
        }

        public double Now => _now;

        public float DeltaTime => _delta;

        public void Advance()
        {
            float dt = Time.deltaTime;
            if (dt > _maxDelta)
                dt = _maxDelta;

            _delta = dt;
            _now += dt;
        }
    }

    /// <summary>
    /// Clock driven by explicit calls. Used by edit-mode simulation and unit tests
    /// so job progression is fully deterministic.
    /// </summary>
    public sealed class ManualCraftingClock : ICraftingClock
    {
        private double _now;
        private float _delta;
        private float _pending;

        public double Now => _now;

        public float DeltaTime => _delta;

        /// <summary>Queues the delta that the next <see cref="Advance"/> will consume.</summary>
        public void Schedule(float deltaSeconds)
        {
            if (deltaSeconds > 0f)
                _pending += deltaSeconds;
        }

        public void Advance()
        {
            _delta = _pending;
            _now += _pending;
            _pending = 0f;
        }

        /// <summary>Rewinds everything to zero.</summary>
        public void Reset()
        {
            _now = 0d;
            _delta = 0f;
            _pending = 0f;
        }
    }
}