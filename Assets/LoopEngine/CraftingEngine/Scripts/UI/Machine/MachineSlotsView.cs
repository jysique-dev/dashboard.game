using System.Collections.Generic;
using UnityEngine.UIElements;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>
    /// The slots section: a header line and one <see cref="MachineSlotView"/> per parallel
    /// slot the machine declares.
    /// </summary>
    /// <remarks>
    /// Views are pooled rather than rebuilt. Slot count is fixed per machine definition, so
    /// switching between two furnaces of the same type reuses everything and only the
    /// contents change.
    /// </remarks>
    public sealed class MachineSlotsView : VisualElement
    {
        private readonly CraftingUIContext _context;
        private readonly List<MachineSlotView> _views = new List<MachineSlotView>(4);
        private readonly VisualElement _list;
        private readonly Label _header;
        private readonly Button _cancelAll;
        private readonly Button _releaseAll;

        private int _shownActive = -1;
        private int _shownCount = -1;

        public MachineSlotsView(CraftingUIContext context)
        {
            _context = context;
            CraftingUITheme theme = context.Theme;

            style.marginBottom = theme.Spacing;

            VisualElement headerRow = CraftingUIStyle.Row(theme);
            headerRow.style.marginBottom = theme.SpacingTight;
            Add(headerRow);

            Label caption = CraftingUIStyle.Body(theme, CraftingUIStrings.Slots);
            caption.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            headerRow.Add(caption);

            _header = CraftingUIStyle.Muted(theme, string.Empty);
            _header.style.marginLeft = theme.SpacingTight;
            _header.style.flexGrow = 1f;
            headerRow.Add(_header);

            _releaseAll = CraftingUIStyle.Action(theme, CraftingUIStrings.ReleaseAll, OnReleaseAll);
            _cancelAll = CraftingUIStyle.Action(theme, CraftingUIStrings.CancelAll, OnCancelAll);
            _cancelAll.style.marginRight = 0f;
            headerRow.Add(_releaseAll);
            headerRow.Add(_cancelAll);

            _list = CraftingUIStyle.Column("slot-list");
            Add(_list);
        }

        /// <summary>Matches the view count to the selected machine, then fills them.</summary>
        public void Rebuild()
        {
            int required = _context.Machines.SlotCount;

            while (_views.Count < required)
            {
                var view = new MachineSlotView(_context, _views.Count);
                _views.Add(view);
                _list.Add(view);
            }

            for (int i = 0; i < _views.Count; i++)
                _views[i].style.display = i < required ? DisplayStyle.Flex : DisplayStyle.None;

            Refresh();
        }

        /// <summary>Structural update of every visible slot. Event driven.</summary>
        public void Refresh()
        {
            int count = _context.Machines.SlotCount;
            for (int i = 0; i < count && i < _views.Count; i++)
                _views[i].Refresh();

            RefreshHeader(count);
        }

        /// <summary>Bar and countdown only. Runs on the sampling timer.</summary>
        public void RefreshProgress()
        {
            int count = _context.Machines.SlotCount;
            for (int i = 0; i < count && i < _views.Count; i++)
                _views[i].RefreshProgress();
        }

        private void RefreshHeader(int count)
        {
            int active = _context.Machines.ActiveJobs;
            if (_shownActive == active && _shownCount == count)
                return;

            _shownActive = active;
            _shownCount = count;
            _header.text = string.Format(CraftingUIStrings.SlotCountFormat, active, count);

            CraftingUIStyle.SetEnabledLook(_cancelAll, _context.Theme, active > 0);
            CraftingUIStyle.SetEnabledLook(_releaseAll, _context.Theme, count > active);
        }

        private void OnCancelAll()
        {
            _context.Machines.CancelAllJobs();
            Refresh();
        }

        private void OnReleaseAll()
        {
            _context.Machines.ReleaseFinishedSlots();
            Refresh();
        }
    }
}