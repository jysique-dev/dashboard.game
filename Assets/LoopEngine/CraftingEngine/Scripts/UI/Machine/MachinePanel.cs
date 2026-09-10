using System;
using UnityEngine.UIElements;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>
    /// The machine window. Owns the header and the slots section, and is the only element
    /// subscribed to the adapter: sections are refreshed by their parent, never by
    /// listening on their own.
    /// </summary>
    /// <remarks>
    /// One subscriber instead of six keeps the update order deterministic and makes the
    /// teardown a single <see cref="Dispose"/> instead of six chances to leak a handler.
    /// Later sections (recipes, queue, modifiers) are added to <see cref="Sections"/> and
    /// refreshed from <see cref="OnChanged"/>.
    /// </remarks>
    public sealed class MachinePanel : VisualElement, IDisposable
    {
        private readonly CraftingUIContext _context;
        private readonly MachineHeaderView _header;
        private readonly MachineSlotsView _slots;
        private readonly ModifierSlotsView _modifiers;
        private readonly QueueListView _queue;
        private readonly RecipeListView _recipes;
        private readonly Label _empty;

        private bool _disposed;

        public MachinePanel(CraftingUIContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            CraftingUITheme theme = context.Theme;

            style.backgroundColor = theme.Background;
            style.paddingLeft = theme.SpacingLoose;
            style.paddingRight = theme.SpacingLoose;
            style.paddingTop = theme.SpacingLoose;
            style.paddingBottom = theme.SpacingLoose;
            style.minWidth = 320f;
            CraftingUIStyle.SetRadius(this, theme.CornerRadius);
            CraftingUIStyle.SetBorder(this, theme.Border, theme.BorderWidth);

            _header = new MachineHeaderView(context);
            Add(_header);

            Sections = CraftingUIStyle.Column("sections");
            Sections.style.flexGrow = 1f;
            Add(Sections);

            _slots = new MachineSlotsView(context);
            Sections.Add(_slots);

            _modifiers = new ModifierSlotsView(context);
            Sections.Add(_modifiers);

            _queue = new QueueListView(context);
            Sections.Add(_queue);

            _recipes = new RecipeListView(context);
            Sections.Add(_recipes);

            _empty = CraftingUIStyle.Muted(theme, CraftingUIStrings.NoMachine);
            _empty.style.marginTop = theme.Spacing;
            Add(_empty);

            _context.Machines.MachineChanged += OnMachineChanged;
            _context.Machines.Changed += OnChanged;
            _context.ProgressSampled += OnProgressSampled;

            ApplySelection();
        }

        /// <summary>Where extra sections attach, in display order.</summary>
        public VisualElement Sections { get; }

        /// <summary>
        /// The upgrade section. Exposed because installing an upgrade means removing an item
        /// from a container, which only the host can do.
        /// </summary>
        public ModifierSlotsView Modifiers => _modifiers;

        private void OnMachineChanged(MachineInstance machine) => ApplySelection();

        private void ApplySelection()
        {
            bool has = _context.Machines.HasMachine;

            Sections.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
            _empty.style.display = has ? DisplayStyle.None : DisplayStyle.Flex;

            _header.Refresh();
            if (!has)
                return;

            _slots.Rebuild();
            _modifiers.Rebuild();
            _queue.Rebuild();
            _recipes.Rebuild();
        }

        private void OnChanged(MachineUIDirty flags)
        {
            if ((flags & MachineUIDirty.Machine) != 0)
            {
                ApplySelection();
                return;
            }

            if ((flags & (MachineUIDirty.Slots | MachineUIDirty.Modifiers)) != 0)
                _slots.Refresh();

            if ((flags & MachineUIDirty.Modifiers) != 0)
                _modifiers.Refresh();

            // Slots move too: a free slot decides whether Craft is clickable at all.
            if ((flags & (MachineUIDirty.Recipes | MachineUIDirty.Slots | MachineUIDirty.Queue)) != 0)
                _recipes.Refresh();

            // Slots matter to the queue too: a job finishing is what lets the front start,
            // and that is when the stall reason stops being true.
            if ((flags & (MachineUIDirty.Queue | MachineUIDirty.Slots)) != 0)
            {
                _queue.Refresh();
                _header.Refresh();
            }
        }

        private void OnProgressSampled() => _slots.RefreshProgress();

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _context.Machines.MachineChanged -= OnMachineChanged;
            _context.Machines.Changed -= OnChanged;
            _context.ProgressSampled -= OnProgressSampled;
        }
    }
}