using UnityEngine.UIElements;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>
    /// Identity strip at the top of the panel: which machine this is and how loaded it is.
    /// </summary>
    /// <remarks>
    /// Resolved stats such as speed and yield are not shown here. They live on
    /// <c>MachineInstance.Stats</c> and belong with the upgrades that change them, so they
    /// arrive with the modifiers section.
    /// </remarks>
    public sealed class MachineHeaderView : VisualElement
    {
        private readonly CraftingUIContext _context;
        private readonly VisualElement _icon;
        private readonly Label _name;
        private readonly Label _subtitle;

        private MachineDefinition _shownDefinition;
        private string _shownSubtitle;

        public MachineHeaderView(CraftingUIContext context)
        {
            _context = context;
            CraftingUITheme theme = context.Theme;

            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.marginBottom = theme.Spacing;
            style.paddingBottom = theme.Spacing;
            style.borderBottomWidth = theme.BorderWidth;
            style.borderBottomColor = theme.Border;

            _icon = CraftingUIStyle.Icon(theme, null);
            _icon.style.marginRight = theme.Spacing;
            Add(_icon);

            VisualElement text = CraftingUIStyle.Column();
            text.style.flexGrow = 1f;
            Add(text);

            _name = CraftingUIStyle.Title(theme, CraftingUIStrings.NoMachine);
            _name.style.marginBottom = 0f;
            text.Add(_name);

            _subtitle = CraftingUIStyle.Muted(theme, string.Empty);
            text.Add(_subtitle);
        }

        /// <summary>Called when the selection changes and whenever the queue moves.</summary>
        public void Refresh()
        {
            MachineUIAdapter machines = _context.Machines;
            MachineDefinition definition = machines.Definition;

            if (!ReferenceEquals(_shownDefinition, definition))
            {
                _shownDefinition = definition;
                _name.text = definition == null
                    ? CraftingUIStrings.NoMachine
                    : _context.Display.GetMachineName(definition);
                CraftingUIStyle.SetIcon(_icon, _context.Theme, _context.Display.GetMachineIcon(definition));
            }

            string subtitle = BuildSubtitle(machines);
            if (subtitle == _shownSubtitle)
                return;

            _shownSubtitle = subtitle;
            _subtitle.text = subtitle;
        }

        private string BuildSubtitle(MachineUIAdapter machines)
        {
            if (!machines.HasMachine)
                return string.Empty;

            string slots = string.Format(
                CraftingUIStrings.SlotCountFormat,
                machines.ActiveJobs,
                machines.SlotCount);

            if (machines.QueueCapacity <= 0)
                return slots;

            return slots + "  ·  " + CraftingUIStrings.Queue + " " +
                   machines.QueuedCount + "/" + machines.QueueCapacity;
        }
    }
}