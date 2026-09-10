using System.Collections.Generic;
using UnityEngine.UIElements;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>
    /// The queue section: pending orders in line order, the controls to reorder them, and
    /// what the machine does when the front of the line cannot start.
    /// </summary>
    /// <remarks>
    /// Rows are pooled to the machine's declared capacity and rebound on every refresh, since
    /// a reorder or a cancellation shifts every position after it. The capacity is small by
    /// construction, so rebinding the lot is cheaper than tracking which rows moved.
    /// </remarks>
    public sealed class QueueListView : VisualElement
    {
        private static readonly QueueStallPolicy[] Policies =
        {
            QueueStallPolicy.Wait,
            QueueStallPolicy.SkipToNext,
            QueueStallPolicy.Drop
        };

        private readonly CraftingUIContext _context;
        private readonly List<QueueRowView> _rows = new List<QueueRowView>(8);
        private readonly List<Button> _policyButtons = new List<Button>(3);
        private readonly VisualElement _list;
        private readonly VisualElement _policyRow;
        private readonly Label _header;
        private readonly Label _empty;
        private readonly Label _stall;
        private readonly Button _clear;

        private int _shownCount = -1;
        private CraftingStatus _shownStall = CraftingStatus.Success;
        private QueueStallPolicy _shownPolicy = (QueueStallPolicy)(-1);

        public QueueListView(CraftingUIContext context)
        {
            _context = context;
            CraftingUITheme theme = context.Theme;

            style.marginBottom = theme.Spacing;

            VisualElement headerRow = CraftingUIStyle.Row(theme);
            headerRow.style.marginBottom = theme.SpacingTight;
            Add(headerRow);

            Label caption = CraftingUIStyle.Body(theme, CraftingUIStrings.Queue);
            caption.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            headerRow.Add(caption);

            _header = CraftingUIStyle.Muted(theme, string.Empty);
            _header.style.marginLeft = theme.SpacingTight;
            _header.style.flexGrow = 1f;
            headerRow.Add(_header);

            _clear = CraftingUIStyle.Action(theme, CraftingUIStrings.ClearQueue, OnClear);
            _clear.style.marginRight = 0f;
            headerRow.Add(_clear);

            _policyRow = CraftingUIStyle.Row(theme);
            _policyRow.style.marginBottom = theme.SpacingTight;
            Add(_policyRow);

            for (int i = 0; i < Policies.Length; i++)
            {
                QueueStallPolicy policy = Policies[i];
                Button button = CraftingUIStyle.Action(
                    theme,
                    CraftingUIStrings.ForPolicy(policy),
                    () => SetPolicy(policy));

                button.tooltip = CraftingUIStrings.HintForPolicy(policy);
                _policyButtons.Add(button);
                _policyRow.Add(button);
            }

            _list = CraftingUIStyle.Column("queue-list");
            Add(_list);

            _empty = CraftingUIStyle.Muted(theme, CraftingUIStrings.QueueEmpty);
            Add(_empty);

            _stall = CraftingUIStyle.Muted(theme, string.Empty);
            _stall.style.marginTop = theme.SpacingTight;
            _stall.style.whiteSpace = WhiteSpace.Normal;
            _stall.style.color = theme.Blocked;
            Add(_stall);
        }

        /// <summary>Sizes the row pool to the machine's queue capacity.</summary>
        public void Rebuild()
        {
            int capacity = _context.Machines.QueueCapacity;

            // A machine with QueueCapacity 0 has queueing disabled by its definition.
            bool hasQueue = capacity > 0;
            _policyRow.style.display = hasQueue ? DisplayStyle.Flex : DisplayStyle.None;
            _clear.style.display = hasQueue ? DisplayStyle.Flex : DisplayStyle.None;
            _list.style.display = hasQueue ? DisplayStyle.Flex : DisplayStyle.None;
            _empty.text = hasQueue ? CraftingUIStrings.QueueEmpty : CraftingUIStrings.NoQueue;

            while (_rows.Count < capacity)
            {
                var row = new QueueRowView(_context, OnMove, OnRemove);
                _rows.Add(row);
                _list.Add(row);
            }

            _shownCount = -1;
            _shownPolicy = (QueueStallPolicy)(-1);
            Refresh();
        }

        /// <summary>Rebinds every row. Driven by Enqueued and Dequeued events.</summary>
        public void Refresh()
        {
            MachineUIAdapter machines = _context.Machines;
            int count = machines.QueuedCount;
            int capacity = machines.QueueCapacity;

            for (int i = 0; i < _rows.Count; i++)
            {
                bool visible = i < count;
                _rows[i].style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

                if (visible)
                    _rows[i].Bind(machines.GetQueued(i), i, count);
                else
                    _rows[i].Bind(QueuedCraft.Empty, i, count);
            }

            _empty.style.display = count == 0 ? DisplayStyle.Flex : DisplayStyle.None;

            if (_shownCount != count)
            {
                _shownCount = count;
                _header.text = capacity > 0
                    ? string.Format(CraftingUIStrings.QueueCountFormat, count, capacity)
                    : string.Empty;

                CraftingUIStyle.SetEnabledLook(_clear, _context.Theme, count > 0);
            }

            RefreshPolicy();
            RefreshStall(count);
        }

        private void RefreshPolicy()
        {
            QueueStallPolicy policy = _context.Machines.StallPolicy;
            if (_shownPolicy == policy)
                return;

            _shownPolicy = policy;
            CraftingUITheme theme = _context.Theme;

            for (int i = 0; i < _policyButtons.Count; i++)
            {
                bool selected = Policies[i] == policy;
                _policyButtons[i].style.backgroundColor = selected ? theme.Accent : theme.SurfaceRaised;
                _policyButtons[i].style.color = selected ? theme.Background : theme.TextPrimary;
            }
        }

        private void RefreshStall(int count)
        {
            CraftingStatus status = _context.Machines.LastQueueStatus;

            // Only the front entry stalls, so with an empty queue the last status is history.
            bool show = count > 0 && status != CraftingStatus.Success && CraftingStatusText.IsPlayerFacing(status);

            if (_shownStall == status && !show)
            {
                _stall.style.display = DisplayStyle.None;
                return;
            }

            _shownStall = status;
            _stall.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            _stall.text = show ? _context.Display.GetStatusText(status) : string.Empty;
        }

        private void OnMove(int from, int to)
        {
            if (_context.Machines.MoveQueued(from, to))
                Refresh();
        }

        private void OnRemove(int ticket)
        {
            if (_context.Machines.CancelQueued(ticket))
                Refresh();
        }

        private void OnClear()
        {
            _context.Machines.ClearQueue();
            Refresh();
        }

        private void SetPolicy(QueueStallPolicy policy)
        {
            _context.Machines.StallPolicy = policy;
            RefreshPolicy();
        }
    }
}