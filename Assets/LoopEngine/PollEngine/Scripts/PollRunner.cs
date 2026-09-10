using UnityEngine;

namespace LoopEngine.PollingEngine
{
    /// <summary>
    /// Optional driver: puts a <see cref="PollScheduler"/> on a scene object so plain C#
    /// services can register callbacks without owning an <c>Update</c> of their own.
    /// </summary>
    /// <remarks>
    /// The only file in this module that references Unity. The engine itself works fine
    /// without it: call <see cref="PollScheduler.Tick"/> from wherever you already have a
    /// frame loop.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("LoopEngine/Polling/Poll Runner")]
    public sealed class PollRunner : MonoBehaviour
    {
        [SerializeField, Tooltip("Use unscaled time, so polling keeps working while the game is paused.")]
        private bool _unscaledTime = true;

        [SerializeField, Min(1), Tooltip("Initial number of registration slots. Grows on demand.")]
        private int _initialCapacity = 8;

        private PollScheduler _scheduler;

        /// <summary>The scheduler. Created on first access, so Awake order does not matter.</summary>
        public PollScheduler Scheduler => _scheduler ?? (_scheduler = new PollScheduler(_initialCapacity));

        /// <summary>Whether the frame delta ignores <c>Time.timeScale</c>.</summary>
        public bool UnscaledTime
        {
            get => _unscaledTime;
            set => _unscaledTime = value;
        }

        /// <summary>Registers a callback. Shorthand for <c>Scheduler.Add</c>.</summary>
        public PollHandle Add(float interval, System.Action callback) => Scheduler.Add(interval, callback);

        /// <summary>Unregisters a callback. Shorthand for <c>Scheduler.Remove</c>.</summary>
        public bool Remove(in PollHandle handle) => Scheduler.Remove(in handle);

        private void Update()
        {
            Scheduler.Tick(_unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        private void OnDisable()
        {
            // Resuming requests an immediate sample per entry, so nothing shows stale data
            // on the frame the object comes back.
            Scheduler.Paused = true;
        }

        private void OnEnable()
        {
            Scheduler.Paused = false;
        }

        private void OnDestroy()
        {
            _scheduler?.Clear();
        }
    }
}