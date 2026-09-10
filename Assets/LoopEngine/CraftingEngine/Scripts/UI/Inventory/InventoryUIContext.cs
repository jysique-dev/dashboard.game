using System;
using System.Collections.Generic;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>
    /// What every inventory view is handed: the catalogue of trackable items, the display
    /// provider, the theme, and the container adapters being watched.
    /// </summary>
    /// <remarks>
    /// Independent from <see cref="CraftingUIContext"/> on purpose. A game can show an
    /// inventory with no crafting UI in sight, and the two are joined only when a machine
    /// window opens next to one. They share a display provider and a theme when it suits;
    /// neither needs the other to exist.
    /// </remarks>
    public sealed class InventoryUIContext : IDisposable
    {
        private readonly List<ItemContainerAdapter> _adapters = new List<ItemContainerAdapter>(4);
        private bool _disposed;

        public InventoryUIContext(
            ItemCatalogue catalogue,
            ICraftingDisplayProvider display,
            CraftingUITheme theme = null)
        {
            Catalogue = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
            Display = display ?? throw new ArgumentNullException(nameof(display));
            Theme = theme ?? CraftingUITheme.Default;
        }

        /// <summary>
        /// Convenience for the common case: one catalogue and one provider built from the
        /// same list of item assets.
        /// </summary>
        public static InventoryUIContext FromItems(
            IReadOnlyList<ItemDefinition> items,
            CraftingUITheme theme = null)
        {
            return new InventoryUIContext(
                new ItemCatalogue(items),
                new DefaultCraftingDisplayProvider(items),
                theme);
        }

        /// <summary>
        /// Builds everything from a crafting database, which is where the item assets already
        /// live. Preferred over <see cref="FromItems"/>: one source of truth instead of a
        /// hand-kept list that can drift from the database it mirrors.
        /// </summary>
        public static InventoryUIContext FromDatabase(CraftingDatabase database, CraftingUITheme theme = null)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            return FromItems(database.Items, theme);
        }

        public ItemCatalogue Catalogue { get; }

        public ICraftingDisplayProvider Display { get; }

        public CraftingUITheme Theme { get; }

        /// <summary>Every container currently being sampled.</summary>
        public IReadOnlyList<ItemContainerAdapter> Adapters => _adapters;

        /// <summary>Raised after a sampling pass in which at least one container changed.</summary>
        public event Action<ItemContainerAdapter> ContainerChanged;

        /// <summary>
        /// Starts watching a container. Samples once immediately, so a view built right after
        /// this call already has contents to draw.
        /// </summary>
        public ItemContainerAdapter Track(IItemContainer container, float interval = 0.25f)
        {
            if (container == null)
                throw new ArgumentNullException(nameof(container));

            ItemContainerAdapter existing = Find(container);
            if (existing != null)
                return existing;

            var adapter = new ItemContainerAdapter(container, Catalogue, interval);
            adapter.Changed += OnAdapterChanged;
            _adapters.Add(adapter);
            adapter.Sample();
            return adapter;
        }

        /// <summary>Stops watching a container.</summary>
        public bool Untrack(ItemContainerAdapter adapter)
        {
            if (adapter == null || !_adapters.Remove(adapter))
                return false;

            adapter.Changed -= OnAdapterChanged;
            return true;
        }

        /// <summary>The adapter watching a container, or null.</summary>
        public ItemContainerAdapter Find(IItemContainer container)
        {
            for (int i = 0; i < _adapters.Count; i++)
            {
                if (ReferenceEquals(_adapters[i].Container, container))
                    return _adapters[i];
            }

            return null;
        }

        /// <summary>Advances every adapter's sampler. Call once per frame.</summary>
        public void Tick(float deltaSeconds)
        {
            for (int i = 0; i < _adapters.Count; i++)
                _adapters[i].Tick(deltaSeconds);
        }

        /// <summary>Suspends or resumes sampling on every adapter.</summary>
        public void SetSamplingPaused(bool paused)
        {
            for (int i = 0; i < _adapters.Count; i++)
            {
                _adapters[i].Poller.Paused = paused;
                if (!paused)
                    _adapters[i].Poller.RequestImmediate();
            }
        }

        /// <summary>Forces every adapter to report its full contents on the next sample.</summary>
        public void InvalidateAll()
        {
            for (int i = 0; i < _adapters.Count; i++)
                _adapters[i].Invalidate();
        }

        private void OnAdapterChanged(ItemContainerAdapter adapter) => ContainerChanged?.Invoke(adapter);

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            ContainerChanged = null;

            for (int i = 0; i < _adapters.Count; i++)
                _adapters[i].Changed -= OnAdapterChanged;

            _adapters.Clear();
        }
    }
}