using LoopEngine.PollingEngine;
using System;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>
    /// One object handed to every panel: the theme they draw with, the provider they read
    /// names from, the adapter they talk to the crafting system through, and the clock that
    /// decides when they sample.
    /// </summary>
    /// <remarks>
    /// The crafting system reports state changes but not progress: a job advances silently
    /// every tick and only raises an event when a repetition lands. So the UI runs on both
    /// legs. Structural changes arrive as events and are coalesced into
    /// <see cref="MachineUIAdapter.Changed"/>; progress and container counts are sampled on a
    /// timer through <see cref="Progress"/> and <see cref="Containers"/>.
    /// </remarks>
    public sealed class CraftingUIContext : IDisposable
    {
        private bool _disposed;

        public CraftingUIContext(
            CraftingSystem _system,
            ICraftingDisplayProvider display,
            CraftingUITheme theme = null)
        {
            if (_system == null)
                throw new ArgumentNullException(nameof(_system));

            Display = display ?? throw new ArgumentNullException(nameof(display));
            Theme = theme ?? CraftingUITheme.Default;
            Machines = new MachineUIAdapter(_system, Display);

            Progress = Poller.Fast;
            Containers = Poller.Slow;
        }

        public CraftingUITheme Theme { get; }

        public ICraftingDisplayProvider Display { get; }

        /// <summary>The only seam between the UI and the crafting system.</summary>
        public MachineUIAdapter Machines { get; }

        /// <summary>Gate for progress bars. Fast, but only useful while a job is active.</summary>
        public Poller Progress;

        /// <summary>
        /// Gate for anything read out of an <see cref="IItemContainer"/>. Containers expose
        /// no change notification, so counts and affordability are polled.
        /// </summary>
        public Poller Containers;

        /// <summary>
        /// Raised on the frames a progress sample is due and at least one job is active.
        /// Slot views are read directly from the adapter, so nothing is allocated.
        /// </summary>
        public event Action ProgressSampled;

        /// <summary>Raised on the frames container counts should be re-read.</summary>
        public event Action ContainersSampled;

        /// <summary>
        /// Drives the whole UI. Call once per frame from wherever the panel lives, with
        /// <c>Time.unscaledDeltaTime</c> so the interface keeps working while the game is
        /// paused.
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            // Structural changes first: a rebuild triggered here should see fresh counts.
            Machines.Pump();

            if (Progress.Tick(deltaSeconds) && Machines.ActiveJobs > 0)
                ProgressSampled?.Invoke();

            if (Containers.Tick(deltaSeconds))
            {
                ContainersSampled?.Invoke();

                // Affordability depends on containers, and nothing announces when they move.
                Machines.MarkDirty(MachineUIDirty.Recipes);
            }
        }

        /// <summary>Suspends both samplers, for a closed or hidden window.</summary>
        public void SetSamplingPaused(bool paused)
        {
            Progress.Paused = paused;
            Containers.Paused = paused;

            if (!paused)
            {
                Progress.RequestImmediate();
                Containers.RequestImmediate();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            ProgressSampled = null;
            ContainersSampled = null;
            Machines.Dispose();
        }
    }
}