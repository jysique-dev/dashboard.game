using System;
using UnityEngine.UIElements;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>
    /// One pending order: where it sits in line, what it is, and the controls to move it or
    /// drop it.
    /// </summary>
    /// <remarks>
    /// Reordering is done by logical position, because that is what
    /// <c>CraftingQueue.TryMove</c> takes. Removal is done by ticket, because a position can
    /// shift between drawing the row and clicking it, while a ticket cannot.
    /// </remarks>
    public sealed class QueueRowView : VisualElement
    {
        private readonly CraftingUIContext _context;
        private readonly Action<int, int> _onMove;
        private readonly Action<int> _onRemove;

        private readonly Label _position;
        private readonly VisualElement _icon;
        private readonly Label _name;
        private readonly Label _count;
        private readonly Button _up;
        private readonly Button _down;
        private readonly Button _remove;

        private RecipeDefinition _shownRecipe;
        private int _shownCount = -1;
        private int _shownIndex = -1;

        /// <summary>Logical position in the queue, 0 being next in line.</summary>
        public int Index { get; private set; } = -1;

        /// <summary>Ticket of the bound order, or 0 when the row is unbound.</summary>
        public int Ticket { get; private set; }

        public QueueRowView(CraftingUIContext context, Action<int, int> onMove, Action<int> onRemove)
        {
            _context = context;
            _onMove = onMove;
            _onRemove = onRemove;

            CraftingUITheme theme = context.Theme;

            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.backgroundColor = theme.Surface;
            style.marginBottom = 2f;
            style.paddingLeft = theme.SpacingTight;
            style.paddingRight = theme.SpacingTight;
            style.paddingTop = 2f;
            style.paddingBottom = 2f;
            CraftingUIStyle.SetRadius(this, theme.CornerRadius);

            _position = CraftingUIStyle.Muted(theme, string.Empty);
            _position.style.width = 22f;
            Add(_position);

            _icon = CraftingUIStyle.Icon(theme, null, 22f);
            _icon.style.marginRight = theme.SpacingTight;
            Add(_icon);

            _name = CraftingUIStyle.Body(theme, string.Empty);
            _name.style.flexGrow = 1f;
            _name.style.overflow = Overflow.Hidden;
            Add(_name);

            _count = CraftingUIStyle.Muted(theme, string.Empty);
            _count.style.marginRight = theme.SpacingTight;
            Add(_count);

            _up = CompactButton(theme, CraftingUIStrings.MoveUp, () => _onMove?.Invoke(Index, Index - 1));
            _down = CompactButton(theme, CraftingUIStrings.MoveDown, () => _onMove?.Invoke(Index, Index + 1));
            _remove = CompactButton(theme, CraftingUIStrings.Remove, () => _onRemove?.Invoke(Ticket));
            _remove.style.color = theme.Cancelled;
            _remove.style.marginRight = 0f;

            Add(_up);
            Add(_down);
            Add(_remove);
        }

        /// <summary>Points the row at an order. Pass an invalid entry to blank it.</summary>
        public void Bind(in QueuedCraft entry, int index, int queueLength)
        {
            Index = index;
            Ticket = entry.Ticket;

            if (!entry.IsValid)
            {
                _shownRecipe = null;
                _shownCount = -1;
                _shownIndex = -1;
                _name.text = string.Empty;
                _count.text = string.Empty;
                _position.text = string.Empty;
                return;
            }

            if (!ReferenceEquals(_shownRecipe, entry.Recipe))
            {
                _shownRecipe = entry.Recipe;
                _name.text = _context.Display.GetRecipeName(entry.Recipe);
                CraftingUIStyle.SetIcon(_icon, _context.Theme, _context.Display.GetRecipeIcon(entry.Recipe));
            }

            if (_shownCount != entry.Count)
            {
                _shownCount = entry.Count;
                _count.text = string.Format(
                    CraftingUIStrings.AmountFormat,
                    _context.Display.FormatAmount(entry.Count));
            }

            if (_shownIndex != index)
            {
                _shownIndex = index;
                _position.text = string.Format(CraftingUIStrings.PositionFormat, index + 1);
            }

            // The front entry is the one about to start; marking it explains why it is the
            // one the stall message is talking about.
            _name.style.color = index == 0 ? _context.Theme.Accent : _context.Theme.TextPrimary;

            CraftingUIStyle.SetEnabledLook(_up, _context.Theme, index > 0);
            CraftingUIStyle.SetEnabledLook(_down, _context.Theme, index < queueLength - 1);
        }

        private static Button CompactButton(CraftingUITheme theme, string text, Action onClick)
        {
            Button button = CraftingUIStyle.Action(theme, text, onClick);
            button.style.width = 24f;
            button.style.height = 22f;
            button.style.paddingLeft = 0f;
            button.style.paddingRight = 0f;
            button.style.marginRight = 2f;
            button.style.fontSize = theme.FontSmall;
            return button;
        }
    }
}