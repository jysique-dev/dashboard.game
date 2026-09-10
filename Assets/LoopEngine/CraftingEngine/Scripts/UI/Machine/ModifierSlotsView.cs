using System.Collections.Generic;
using UnityEngine.UIElements;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>
    /// The upgrade section: one chip per modifier slot the machine declares, filled or empty.
    /// </summary>
    /// <remarks>
    /// Installing is not a button here, and that is deliberate. The crafting system does not
    /// touch containers when a modifier goes in, so the carrier item must be removed from
    /// wherever it came from first. Only the code that owns that container can do it, so this
    /// view exposes <see cref="TryInstallFromItem"/> and lets the inventory drive it.
    /// </remarks>
    public sealed class ModifierSlotsView : VisualElement
    {
        private readonly CraftingUIContext _context;
        private readonly List<VisualElement> _chips = new List<VisualElement>(4);
        private readonly List<Label> _labels = new List<Label>(4);
        private readonly List<Button> _removes = new List<Button>(4);
        private readonly VisualElement _row;
        private readonly Label _header;
        private readonly Label _feedback;

        private int _shownInstalled = -1;

        public ModifierSlotsView(CraftingUIContext context)
        {
            _context = context;
            CraftingUITheme theme = context.Theme;

            style.marginBottom = theme.Spacing;

            // An invisible border by default, so a drop highlight has something to colour
            // without the section shifting by a pixel when it appears.
            CraftingUIStyle.SetRadius(this, theme.CornerRadius);
            CraftingUIStyle.SetBorder(this, theme.Background, theme.BorderWidth);

            VisualElement headerRow = CraftingUIStyle.Row(theme);
            headerRow.style.marginBottom = theme.SpacingTight;
            Add(headerRow);

            Label caption = CraftingUIStyle.Body(theme, CraftingUIStrings.Upgrades);
            caption.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            headerRow.Add(caption);

            _header = CraftingUIStyle.Muted(theme, string.Empty);
            _header.style.marginLeft = theme.SpacingTight;
            _header.style.flexGrow = 1f;
            headerRow.Add(_header);

            _row = CraftingUIStyle.Row(theme);
            _row.style.flexWrap = Wrap.Wrap;
            Add(_row);

            _feedback = CraftingUIStyle.Muted(theme, string.Empty);
            _feedback.style.marginTop = theme.SpacingTight;
            _feedback.style.whiteSpace = WhiteSpace.Normal;
            Add(_feedback);
        }

        /// <summary>Sizes the chips to the machine's declared upgrade slots.</summary>
        public void Rebuild()
        {
            int slots = _context.Machines.ModifierSlots;

            // A machine with no upgrade slots has upgrades disabled by its definition.
            style.display = slots > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (slots == 0)
                return;

            while (_chips.Count < slots)
                CreateChip();

            for (int i = 0; i < _chips.Count; i++)
                _chips[i].style.display = i < slots ? DisplayStyle.Flex : DisplayStyle.None;

            _shownInstalled = -1;
            _feedback.text = string.Empty;
            Refresh();
        }

        /// <summary>Rebinds every chip. Driven by the ModifiersChanged event.</summary>
        public void Refresh()
        {
            MachineUIAdapter machines = _context.Machines;
            int slots = machines.ModifierSlots;
            if (slots == 0)
                return;

            CraftingUITheme theme = _context.Theme;

            for (int i = 0; i < slots && i < _chips.Count; i++)
            {
                ModifierDefinition modifier = machines.GetModifier(i);
                bool filled = modifier != null;

                _labels[i].text = filled
                    ? _context.Display.GetModifierName(modifier)
                    : CraftingUIStrings.EmptyUpgrade;

                _labels[i].style.color = filled ? theme.TextPrimary : theme.TextDisabled;
                CraftingUIStyle.SetBorderColor(_chips[i], filled ? theme.Accent : theme.Border);
                _removes[i].style.display = filled ? DisplayStyle.Flex : DisplayStyle.None;
            }

            int installed = machines.InstalledModifierCount;
            if (_shownInstalled == installed)
                return;

            _shownInstalled = installed;
            _header.text = string.Format(CraftingUIStrings.QueueCountFormat, installed, slots);
        }

        /// <summary>
        /// True when this item maps to an upgrade this machine accepts and there is room for
        /// it. Used by the drop target before it takes the item out of a container.
        /// </summary>
        public bool CanInstallFromItem(in CraftingId itemId)
        {
            MachineUIAdapter machines = _context.Machines;
            if (machines.ModifierSlots == 0 || machines.InstalledModifierCount >= machines.ModifierSlots)
                return false;

            return machines.GetModifierFor(in itemId) != null;
        }

        /// <summary>
        /// Installs the upgrade an item would provide, or reports why it cannot.
        /// The caller is responsible for having removed the item from its container: this
        /// method consumes nothing.
        /// </summary>
        public CraftingStatus TryInstallFromItem(in CraftingId itemId)
        {
            ModifierDefinition modifier = _context.Machines.GetModifierFor(in itemId);
            if (modifier == null)
            {
                ShowFeedback(CraftingUIStrings.NotAnUpgrade, false);
                return CraftingStatus.ModifierRejected;
            }

            return TryInstall(modifier);
        }

        /// <summary>Installs a resolved upgrade. Same ownership caveat as above.</summary>
        public CraftingStatus TryInstall(ModifierDefinition modifier)
        {
            CraftingStatus status = _context.Machines.InstallModifier(modifier);

            if (status == CraftingStatus.Success)
                ShowFeedback(string.Format(
                    CraftingUIStrings.InstalledFormat,
                    _context.Display.GetModifierName(modifier)), true);
            else
                ShowFeedback(_context.Display.GetStatusText(status), false);

            Refresh();
            return status;
        }

        private void CreateChip()
        {
            CraftingUITheme theme = _context.Theme;
            int index = _chips.Count;

            VisualElement chip = CraftingUIStyle.Row(theme);
            chip.style.backgroundColor = theme.Surface;
            chip.style.marginRight = theme.SpacingTight;
            chip.style.marginBottom = theme.SpacingTight;
            chip.style.paddingLeft = theme.Spacing;
            chip.style.paddingRight = theme.SpacingTight;
            chip.style.height = 26f;
            CraftingUIStyle.SetRadius(chip, theme.CornerRadius);
            CraftingUIStyle.SetBorder(chip, theme.Border, theme.BorderWidth);

            Label label = CraftingUIStyle.Body(theme, CraftingUIStrings.EmptyUpgrade);
            chip.Add(label);

            Button remove = CraftingUIStyle.Action(theme, CraftingUIStrings.Remove, () => OnRemove(index));
            remove.style.width = 22f;
            remove.style.height = 20f;
            remove.style.marginLeft = theme.SpacingTight;
            remove.style.marginRight = 0f;
            remove.style.paddingLeft = 0f;
            remove.style.paddingRight = 0f;
            remove.style.fontSize = theme.FontSmall;
            remove.style.color = theme.Cancelled;
            chip.Add(remove);

            _chips.Add(chip);
            _labels.Add(label);
            _removes.Add(remove);
            _row.Add(chip);
        }

        private void OnRemove(int index)
        {
            ModifierDefinition modifier = _context.Machines.GetModifier(index);
            if (modifier == null)
                return;

            // The upgrade is gone, not returned: giving the carrier item back is the host's
            // decision, exactly as taking it away was.
            if (_context.Machines.RemoveModifier(modifier))
            {
                ShowFeedback(string.Format(
                    CraftingUIStrings.RemovedFormat,
                    _context.Display.GetModifierName(modifier)), true);

                Removed?.Invoke(modifier);
            }

            Refresh();
        }

        /// <summary>Raised after an upgrade is taken out, so the host can hand the item back.</summary>
        public event System.Action<ModifierDefinition> Removed;

        private void ShowFeedback(string message, bool positive)
        {
            _feedback.text = message;
            _feedback.style.color = positive ? _context.Theme.Running : _context.Theme.Blocked;
        }
    }
}