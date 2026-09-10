using UnityEngine;
using UnityEngine.UIElements;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>
    /// A floating card describing the item under the pointer: name, count, description and
    /// tags.
    /// </summary>
    /// <remarks>
    /// Built instead of using UI Toolkit's own <c>tooltip</c> string because that one shows
    /// plain text after a delay and cannot draw an icon or wrap a description. One instance
    /// per screen, moved and rebound rather than recreated.
    /// </remarks>
    public sealed class ItemTooltipView : VisualElement
    {
        private const float PointerOffset = 16f;

        private readonly CraftingUITheme _theme;
        private readonly ICraftingDisplayProvider _display;
        private readonly ItemCatalogue _catalogue;
        private readonly VisualElement _icon;
        private readonly Label _name;
        private readonly Label _amount;
        private readonly Label _description;
        private readonly Label _tags;

        private readonly Label _held;

        private System.Func<CraftingId, int> _totalProvider;
        private CraftingId _shownItem;
        private int _shownMaxStack;

        public ItemTooltipView(InventoryUIContext context)
        {
            _theme = context.Theme;
            _display = context.Display;
            _catalogue = context.Catalogue;

            style.position = Position.Absolute;
            style.maxWidth = 260f;
            style.paddingLeft = _theme.Spacing;
            style.paddingRight = _theme.Spacing;
            style.paddingTop = _theme.SpacingTight;
            style.paddingBottom = _theme.SpacingTight;
            style.backgroundColor = _theme.SurfaceRaised;
            style.display = DisplayStyle.None;
            CraftingUIStyle.SetRadius(this, _theme.CornerRadius);
            CraftingUIStyle.SetBorder(this, _theme.Border, _theme.BorderWidth);

            // A tooltip that swallowed pointer events would flicker: entering it would leave
            // the cell that asked for it.
            pickingMode = PickingMode.Ignore;

            VisualElement header = CraftingUIStyle.Row(_theme);
            Add(header);

            _icon = CraftingUIStyle.Icon(_theme, null, 28f);
            _icon.style.marginRight = _theme.SpacingTight;
            _icon.pickingMode = PickingMode.Ignore;
            header.Add(_icon);

            _name = CraftingUIStyle.Body(_theme, string.Empty);
            _name.style.unityFontStyleAndWeight = FontStyle.Bold;
            _name.style.flexGrow = 1f;
            header.Add(_name);

            _amount = CraftingUIStyle.Muted(_theme, string.Empty);
            _amount.style.marginLeft = _theme.Spacing;
            header.Add(_amount);

            _held = CraftingUIStyle.Muted(_theme, string.Empty);
            _held.style.marginTop = 2f;
            Add(_held);

            _description = CraftingUIStyle.Muted(_theme, string.Empty);
            _description.style.whiteSpace = WhiteSpace.Normal;
            _description.style.marginTop = _theme.SpacingTight;
            Add(_description);

            _tags = CraftingUIStyle.Muted(_theme, string.Empty);
            _tags.style.whiteSpace = WhiteSpace.Normal;
            _tags.style.marginTop = 2f;
            _tags.style.color = _theme.Accent;
            Add(_tags);
        }

        /// <summary>
        /// Optional lookup for the total held of an item, so the card can say how much there
        /// is beyond the cell being hovered. Point it at a container adapter.
        /// </summary>
        public System.Func<CraftingId, int> TotalProvider
        {
            get => _totalProvider;
            set => _totalProvider = value;
        }

        /// <summary>True while the tooltip is on screen.</summary>
        public bool IsVisible => style.display == DisplayStyle.Flex;

        /// <summary>Shows the card for a stack, positioned near a world-space point.</summary>
        public void Show(in ItemStack stack, Vector2 worldPosition)
        {
            if (stack.IsEmpty)
            {
                Hide();
                return;
            }

            if (!_shownItem.Equals(stack.Item))
            {
                _shownItem = stack.Item;
                Rebind(stack.Item);
            }

            // The header count is this cell's slice; the line below is the whole holding,
            // which is what the player actually wants to know when stacks are split.
            _amount.text = _shownMaxStack > 0
                ? string.Format(CraftingUIStrings.StackOfFormat,
                    _display.FormatAmount(stack.Amount),
                    _display.FormatAmount(_shownMaxStack))
                : string.Format(CraftingUIStrings.AmountFormat, _display.FormatAmount(stack.Amount));

            if (_totalProvider != null)
            {
                int held = _totalProvider(stack.Item);
                bool showHeld = held > stack.Amount;
                _held.text = showHeld
                    ? string.Format(CraftingUIStrings.HeldFormat, _display.FormatAmount(held))
                    : string.Empty;
                _held.style.display = showHeld ? DisplayStyle.Flex : DisplayStyle.None;
            }
            else
            {
                _held.style.display = DisplayStyle.None;
            }

            style.display = DisplayStyle.Flex;
            BringToFront();
            MoveTo(worldPosition);
        }

        /// <summary>Repositions without rebinding, for a pointer moving across one cell.</summary>
        public void MoveTo(Vector2 worldPosition)
        {
            if (parent == null)
                return;

            Vector2 local = parent.WorldToLocal(worldPosition);
            float width = resolvedStyle.width;
            float height = resolvedStyle.height;

            float x = local.x + PointerOffset;
            float y = local.y + PointerOffset;

            // Flip to the other side of the pointer rather than let the card run off screen.
            float maxX = parent.resolvedStyle.width - width;
            float maxY = parent.resolvedStyle.height - height;

            if (width > 0f && x > maxX)
                x = local.x - width - PointerOffset;

            if (height > 0f && y > maxY)
                y = local.y - height - PointerOffset;

            style.left = x < 0f ? 0f : x;
            style.top = y < 0f ? 0f : y;
        }

        public void Hide()
        {
            style.display = DisplayStyle.None;
            _shownItem = CraftingId.None;
        }

        private void Rebind(in CraftingId item)
        {
            _name.text = _display.GetItemName(in item);
            CraftingUIStyle.SetIcon(_icon, _theme, _display.GetItemIcon(in item));

            int index = _catalogue.IndexOf(in item);
            ItemDefinition definition = index < 0 ? null : _catalogue.GetDefinition(index);
            _shownMaxStack = definition == null ? 0 : definition.MaxStack;

            if (definition == null)
            {
                _description.style.display = DisplayStyle.None;
                _tags.style.display = DisplayStyle.None;
                return;
            }

            string description = definition.Description;
            _description.text = description;
            _description.style.display = description.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            _tags.text = BuildTags(definition);
            _tags.style.display = _tags.text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static string BuildTags(ItemDefinition definition)
        {
            CraftingId[] tags = definition.Tags;
            if (tags.Length == 0)
                return string.Empty;

            // Tags keep their authoring string, so they are readable without a lookup.
            string result = tags[0].Value;
            for (int i = 1; i < tags.Length; i++)
                result += "  " + tags[i].Value;

            return result;
        }
    }
}