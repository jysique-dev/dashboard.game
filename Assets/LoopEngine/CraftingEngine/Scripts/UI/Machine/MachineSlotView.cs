using UnityEngine;
using UnityEngine.UIElements;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>
    /// One machine slot: what it is making, how far along it is, and the two things you can
    /// do about it.
    /// </summary>
    /// <remarks>
    /// Split into two refresh paths on purpose. <see cref="Refresh"/> is the structural one
    /// and runs when an event says the slot changed. <see cref="RefreshProgress"/> runs on
    /// the sampling timer and touches nothing but the bar width and the countdown, because
    /// the crafting system reports no event while a job is simply advancing.
    /// </remarks>
    public sealed class MachineSlotView : VisualElement
    {
        private readonly CraftingUIContext _context;
        private readonly int _index;

        private readonly VisualElement _icon;
        private readonly VisualElement _track;
        private readonly VisualElement _fill;
        private readonly Label _title;
        private readonly Label _state;
        private readonly Label _counter;
        private readonly Label _time;
        private readonly Button _cancel;
        private readonly Button _release;

        // Cached so a 30 Hz refresh does not rewrite text that did not change.
        private RecipeDefinition _shownRecipe;
        private JobState _shownState = (JobState)(-1);
        private CraftingStatus _shownStatus = CraftingStatus.Success;
        private int _shownRemaining = -1;
        private int _shownCompleted = -1;
        private int _shownFillPercent = -1;
        private int _shownSeconds = -1;

        public MachineSlotView(CraftingUIContext context, int index)
        {
            _context = context;
            _index = index;

            CraftingUITheme theme = context.Theme;

            style.backgroundColor = theme.Surface;
            style.marginBottom = theme.SpacingTight;
            style.paddingLeft = theme.Spacing;
            style.paddingRight = theme.Spacing;
            style.paddingTop = theme.SpacingTight;
            style.paddingBottom = theme.SpacingTight;
            CraftingUIStyle.SetRadius(this, theme.CornerRadius);
            CraftingUIStyle.SetBorder(this, theme.Border, theme.BorderWidth);

            VisualElement row = CraftingUIStyle.Row(theme);
            Add(row);

            _icon = CraftingUIStyle.Icon(theme, null);
            _icon.style.marginRight = theme.Spacing;
            row.Add(_icon);

            VisualElement body = CraftingUIStyle.Column();
            body.style.flexGrow = 1f;
            row.Add(body);

            VisualElement titleRow = CraftingUIStyle.Row(theme);
            body.Add(titleRow);

            _title = CraftingUIStyle.Body(theme, CraftingUIStrings.EmptySlot);
            _title.style.flexGrow = 1f;
            _title.style.overflow = Overflow.Hidden;
            titleRow.Add(_title);

            _counter = CraftingUIStyle.Muted(theme, string.Empty);
            titleRow.Add(_counter);

            VisualElement stateRow = CraftingUIStyle.Row(theme);
            stateRow.style.marginTop = 2f;
            body.Add(stateRow);

            _state = CraftingUIStyle.Muted(theme, CraftingUIStrings.Idle);
            _state.style.flexGrow = 1f;
            stateRow.Add(_state);

            _time = CraftingUIStyle.Muted(theme, string.Empty);
            stateRow.Add(_time);

            _track = new VisualElement();
            _track.style.height = theme.ProgressHeight;
            _track.style.marginTop = theme.SpacingTight;
            _track.style.backgroundColor = theme.TrackEmpty;
            _track.style.overflow = Overflow.Hidden;
            CraftingUIStyle.SetRadius(_track, theme.ProgressHeight * 0.5f);
            body.Add(_track);

            _fill = new VisualElement();
            _fill.style.height = Length.Percent(100f);
            _fill.style.width = Length.Percent(0f);
            _fill.style.backgroundColor = theme.Running;
            CraftingUIStyle.SetRadius(_fill, theme.ProgressHeight * 0.5f);
            _track.Add(_fill);

            VisualElement actions = CraftingUIStyle.Row(theme);
            actions.style.marginTop = theme.SpacingTight;
            actions.style.justifyContent = Justify.FlexEnd;
            body.Add(actions);

            _cancel = CraftingUIStyle.Action(theme, CraftingUIStrings.Cancel, OnCancelClicked);
            _release = CraftingUIStyle.Action(theme, CraftingUIStrings.Release, OnReleaseClicked);
            _release.style.marginRight = 0f;
            actions.Add(_cancel);
            actions.Add(_release);

            Refresh();
        }

        /// <summary>Full update. Driven by events, never by the frame.</summary>
        public void Refresh()
        {
            CraftingJobView view = _context.Machines.GetSlot(_index);
            CraftingUITheme theme = _context.Theme;

            if (!ReferenceEquals(_shownRecipe, view.Recipe))
            {
                _shownRecipe = view.Recipe;
                _title.text = view.Recipe == null
                    ? CraftingUIStrings.EmptySlot
                    : _context.Display.GetRecipeName(view.Recipe);
                CraftingUIStyle.SetIcon(_icon, theme, _context.Display.GetRecipeIcon(view.Recipe));
                _title.style.color = view.Recipe == null ? theme.TextMuted : theme.TextPrimary;
            }

            if (_shownState != view.State || _shownStatus != view.LastStatus)
            {
                _shownState = view.State;
                _shownStatus = view.LastStatus;

                Color stateColor = theme.GetStateColor(view.State);
                _fill.style.backgroundColor = stateColor;
                CraftingUIStyle.SetBorderColor(this, view.State == JobState.Empty ? theme.Border : stateColor);

                // A blocked job knows why it stopped, and that is the only useful thing to
                // show: "Blocked" alone tells the player nothing they can act on.
                _state.text = view.State == JobState.Blocked
                    ? _context.Display.GetStatusText(view.LastStatus)
                    : CraftingUIStrings.ForState(view.State);
                _state.style.color = view.State == JobState.Empty ? theme.TextMuted : stateColor;
            }

            if (_shownRemaining != view.Remaining || _shownCompleted != view.Completed)
            {
                _shownRemaining = view.Remaining;
                _shownCompleted = view.Completed;

                if (view.Recipe == null)
                    _counter.text = string.Empty;
                else if (view.Remaining > 0)
                    _counter.text = string.Format(
                        CraftingUIStrings.RepetitionsFormat,
                        _context.Display.FormatAmount(view.Remaining));
                else
                    _counter.text = string.Format(
                        CraftingUIStrings.CompletedFormat,
                        _context.Display.FormatAmount(view.Completed));
            }

            _track.style.display = view.Recipe == null ? DisplayStyle.None : DisplayStyle.Flex;

            RefreshProgress(in view);
            RefreshActions(in view);
        }

        /// <summary>Cheap update for the sampling timer. Bar and countdown only.</summary>
        public void RefreshProgress()
        {
            CraftingJobView view = _context.Machines.GetSlot(_index);
            RefreshProgress(in view);
        }

        private void RefreshProgress(in CraftingJobView view)
        {
            if (view.Recipe == null)
            {
                SetFill(0);
                SetSeconds(-1);
                return;
            }

            // A blocked job is not advancing; pinning the bar full says "waiting to deliver"
            // rather than leaving it frozen at an arbitrary spot.
            float progress = view.State == JobState.Blocked ? 1f : view.Progress;
            SetFill(Mathf.RoundToInt(progress * 100f));

            SetSeconds(view.State == JobState.Running ? Mathf.CeilToInt(view.RemainingSeconds) : -1);
        }

        private void RefreshActions(in CraftingJobView view)
        {
            CraftingUITheme theme = _context.Theme;

            bool canCancel = _context.Machines.CanCancelSlot(_index);
            CraftingUIStyle.SetEnabledLook(_cancel, theme, canCancel);
            _cancel.tooltip = !view.IsActive || canCancel ? string.Empty : CraftingUIStrings.CancelUnavailable;

            bool canRelease = view.State == JobState.Completed || view.State == JobState.Cancelled;
            CraftingUIStyle.SetEnabledLook(_release, theme, canRelease);

            bool anyAction = view.State != JobState.Empty;
            _cancel.style.display = anyAction ? DisplayStyle.Flex : DisplayStyle.None;
            _release.style.display = anyAction ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void SetFill(int percent)
        {
            if (percent < 0)
                percent = 0;
            else if (percent > 100)
                percent = 100;

            if (_shownFillPercent == percent)
                return;

            _shownFillPercent = percent;
            _fill.style.width = Length.Percent(percent);
        }

        private void SetSeconds(int seconds)
        {
            if (_shownSeconds == seconds)
                return;

            _shownSeconds = seconds;
            _time.text = seconds < 0 ? string.Empty : _context.Display.FormatSeconds(seconds);
        }

        private void OnCancelClicked()
        {
            _context.Machines.CancelSlot(_index);
            Refresh();
        }

        private void OnReleaseClicked()
        {
            _context.Machines.ReleaseSlot(_index);
            Refresh();
        }
    }
}