using System.Collections.Generic;
using UnityEngine.UIElements;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>
    /// Search field, sort cycle and tag filters for one <see cref="InventoryQuery"/>.
    /// </summary>
    /// <remarks>
    /// The tag buttons are built from the catalogue, not authored: every distinct tag on
    /// every tracked item becomes a filter. A project that adds a new tag to an item asset
    /// gets the button for free.
    /// </remarks>
    public sealed class InventoryToolbarView : VisualElement
    {
        private static readonly InventorySort[] SortCycle =
        {
            InventorySort.Catalogue,
            InventorySort.Name,
            InventorySort.AmountDescending,
            InventorySort.AmountAscending
        };

        private readonly InventoryUIContext _context;
        private readonly InventoryQuery _query;
        private readonly List<Button> _tagButtons = new List<Button>(8);
        private readonly List<CraftingId> _tags = new List<CraftingId>(8);
        private readonly TextField _search;
        private readonly Button _sort;

        public InventoryToolbarView(InventoryUIContext context, InventoryQuery query, int maxTagButtons = 8)
        {
            _context = context;
            _query = query;

            CraftingUITheme theme = context.Theme;
            style.marginBottom = theme.SpacingTight;

            VisualElement row = CraftingUIStyle.Row(theme);
            Add(row);

            _search = new TextField { value = string.Empty };
            _search.style.flexGrow = 1f;
            _search.style.marginLeft = 0f;
            _search.style.marginRight = theme.SpacingTight;
            _search.style.height = 26f;
            ApplyFieldStyle(_search, theme);
            _search.RegisterValueChangedCallback(evt => _query.Search = evt.newValue);
            row.Add(_search);

            _sort = CraftingUIStyle.Action(theme, CraftingUIStrings.ForSort(query.Sort), CycleSort);
            _sort.style.marginRight = 0f;
            row.Add(_sort);

            VisualElement tagRow = CraftingUIStyle.Row(theme);
            tagRow.style.flexWrap = Wrap.Wrap;
            tagRow.style.marginTop = theme.SpacingTight;
            Add(tagRow);

            CollectTags(maxTagButtons);
            BuildTagButtons(tagRow, theme);
            RefreshTagButtons();
        }

        /// <summary>Clears the search box and any tag filter.</summary>
        public void ClearFilters()
        {
            _search.SetValueWithoutNotify(string.Empty);
            _query.Clear();
            RefreshTagButtons();
        }

        private void CollectTags(int limit)
        {
            ItemCatalogue catalogue = _context.Catalogue;

            for (int i = 0; i < catalogue.Count && _tags.Count < limit; i++)
            {
                ItemDefinition definition = catalogue.GetDefinition(i);
                if (definition == null)
                    continue;

                CraftingId[] tags = definition.Tags;
                for (int t = 0; t < tags.Length && _tags.Count < limit; t++)
                {
                    if (!Contains(tags[t]))
                        _tags.Add(tags[t]);
                }
            }
        }

        private bool Contains(in CraftingId tag)
        {
            for (int i = 0; i < _tags.Count; i++)
            {
                if (_tags[i].Equals(tag))
                    return true;
            }

            return false;
        }

        private void BuildTagButtons(VisualElement parent, CraftingUITheme theme)
        {
            if (_tags.Count == 0)
            {
                parent.style.display = DisplayStyle.None;
                return;
            }

            for (int i = 0; i < _tags.Count; i++)
            {
                CraftingId tag = _tags[i];
                Button button = CraftingUIStyle.Action(theme, tag.Value, () => ToggleTag(tag));
                button.style.height = 22f;
                button.style.fontSize = theme.FontSmall;
                button.style.marginBottom = 2f;
                _tagButtons.Add(button);
                parent.Add(button);
            }
        }

        private void ToggleTag(CraftingId tag)
        {
            // Clicking the active tag clears the filter instead of reapplying it.
            _query.Tag = _query.Tag.Equals(tag) ? CraftingId.None : tag;
            RefreshTagButtons();
        }

        private void RefreshTagButtons()
        {
            CraftingUITheme theme = _context.Theme;

            for (int i = 0; i < _tagButtons.Count; i++)
            {
                bool active = _query.Tag.Equals(_tags[i]);
                _tagButtons[i].style.backgroundColor = active ? theme.Accent : theme.SurfaceRaised;
                _tagButtons[i].style.color = active ? theme.Background : theme.TextPrimary;
            }
        }

        private void CycleSort()
        {
            int index = 0;
            for (int i = 0; i < SortCycle.Length; i++)
            {
                if (SortCycle[i] == _query.Sort)
                {
                    index = i;
                    break;
                }
            }

            InventorySort next = SortCycle[(index + 1) % SortCycle.Length];
            _query.Sort = next;
            _sort.text = CraftingUIStrings.ForSort(next);
        }

        private static void ApplyFieldStyle(TextField field, CraftingUITheme theme)
        {
            field.style.color = theme.TextPrimary;
            CraftingUIStyle.SetRadius(field, theme.CornerRadius);

            // The text input is a child element, so the visible surface is styled there.
            VisualElement input = field.Q(TextField.textInputUssName);
            if (input == null)
                return;

            input.style.backgroundColor = theme.Surface;
            input.style.color = theme.TextPrimary;
            input.style.fontSize = theme.FontBody;
            CraftingUIStyle.SetRadius(input, theme.CornerRadius);
            CraftingUIStyle.SetBorder(input, theme.Border, theme.BorderWidth);
        }
    }
}