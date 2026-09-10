using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>
    /// One cell of the inventory grid: an icon, a count, and a selection state.
    /// </summary>
    /// <remarks>
    /// Cells are pooled and rebound, never rebuilt, so everything that can change is cached
    /// and compared before it is written. A grid of sixty cells refreshed four times a second
    /// should produce no style writes at all while the player is doing nothing.
    /// </remarks>
    public sealed class InventorySlotView : VisualElement
    {
        private readonly CraftingUITheme _theme;
        private readonly ICraftingDisplayProvider _display;
        private readonly VisualElement _icon;
        private readonly Label _amount;

        private IVisualElementScheduledItem _flash;
        private CraftingId _shownItem;
        private int _shownAmount = -1;
        private bool _selected;

        /// <summary>The stack this cell shows. Empty when the cell is a filler.</summary>
        public ItemStack Stack { get; private set; }

        /// <summary>Position of this cell in the grid, not in the container.</summary>
        public int CellIndex { get; internal set; } = -1;

        public bool IsEmpty => Stack.IsEmpty;

        /// <summary>Raised on click, with this cell. Empty cells report too.</summary>
        public event Action<InventorySlotView> Clicked;

        public InventorySlotView(CraftingUITheme theme, ICraftingDisplayProvider display, float size)
        {
            _theme = theme;
            _display = display;

            style.width = size;
            style.height = size;
            style.marginRight = theme.SpacingTight;
            style.marginBottom = theme.SpacingTight;
            style.backgroundColor = theme.Surface;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            CraftingUIStyle.SetRadius(this, theme.CornerRadius);
            CraftingUIStyle.SetBorder(this, theme.Border, theme.BorderWidth);

            _icon = CraftingUIStyle.Icon(theme, null, size - theme.Spacing * 2f);
            _icon.style.backgroundColor = UnityEngine.Color.clear;
            Add(_icon);

            _amount = CraftingUIStyle.Body(theme, string.Empty);
            _amount.style.position = Position.Absolute;
            _amount.style.right = 4f;
            _amount.style.bottom = 2f;
            _amount.style.fontSize = theme.FontSmall;
            _amount.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            Add(_amount);

            // No click callback here on purpose: the drag controller captures the pointer
            // on press, which suppresses ClickEvent, so it reports clicks through
            // RaiseClicked instead.
        }

        /// <summary>Points the cell at a stack. Pass an empty one to blank it.</summary>
        public void Bind(in ItemStack stack)
        {
            bool sameItem = _shownItem.Equals(stack.Item);
            if (sameItem && _shownAmount == stack.Amount)
            {
                Stack = stack;
                return;
            }

            Stack = stack;

            if (!sameItem)
            {
                _shownItem = stack.Item;
                CraftingUIStyle.SetIcon(_icon, _theme, _display.GetItemIcon(stack.Item));
                _icon.style.backgroundColor = UnityEngine.Color.clear;
                tooltip = stack.IsEmpty ? string.Empty : _display.GetItemName(stack.Item);
            }

            if (_shownAmount != stack.Amount)
            {
                _shownAmount = stack.Amount;

                // A single unit needs no number: the icon already says what it is.
                _amount.text = stack.Amount > 1 ? _display.FormatAmount(stack.Amount) : string.Empty;
            }

            style.backgroundColor = stack.IsEmpty ? _theme.Background : _theme.Surface;
        }

        /// <summary>Reports a click. Called by the drag controller on a release that never moved.</summary>
        internal void RaiseClicked() => Clicked?.Invoke(this);

        /// <summary>Blanks the cell without unbinding it from the grid.</summary>
        public void Clear() => Bind(ItemStack.Empty);

        public void SetSelected(bool selected)
        {
            if (_selected == selected)
                return;

            _selected = selected;
            CraftingUIStyle.SetBorderColor(this, selected ? _theme.Accent : _theme.Border);
            style.borderTopWidth = selected ? _theme.BorderWidth * 2f : _theme.BorderWidth;
            style.borderRightWidth = selected ? _theme.BorderWidth * 2f : _theme.BorderWidth;
            style.borderBottomWidth = selected ? _theme.BorderWidth * 2f : _theme.BorderWidth;
            style.borderLeftWidth = selected ? _theme.BorderWidth * 2f : _theme.BorderWidth;
        }

        /// <summary>
        /// Pulses the cell's background and fades it back. Used to point at what a craft just
        /// consumed or produced.
        /// </summary>
        /// <remarks>
        /// Driven by the panel's own scheduler rather than an Update, so a grid with no
        /// flashes running costs nothing. Starting a second flash cancels the first instead
        /// of stacking two fades on one colour.
        /// </remarks>
        public void Flash(Color color, int durationMs = 320)
        {
            if (durationMs < 32)
                durationMs = 32;

            _flash?.Pause();

            Color resting = Stack.IsEmpty ? _theme.Background : _theme.Surface;
            int elapsed = 0;

            IVisualElementScheduledItem item = null;
            item = schedule.Execute(() =>
            {
                elapsed += 16;
                float t = elapsed / (float)durationMs;

                if (t >= 1f)
                {
                    style.backgroundColor = resting;
                    item.Pause();
                    return;
                }

                style.backgroundColor = Color.Lerp(color, resting, t);
            }).Every(16).ForDuration(durationMs + 32);

            _flash = item;
            style.backgroundColor = color;
        }

        /// <summary>Tints the cell, for hover feedback and drop targets.</summary>
        public void SetHighlight(bool highlighted)
            => style.backgroundColor = highlighted
                ? _theme.SurfaceRaised
                : (Stack.IsEmpty ? _theme.Background : _theme.Surface);
    }
}