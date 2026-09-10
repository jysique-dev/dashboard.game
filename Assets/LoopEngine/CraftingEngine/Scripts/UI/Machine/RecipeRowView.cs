using System;
using UnityEngine.UIElements;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>
    /// One recipe: what it eats, what it makes, how many times you could afford it, and the
    /// two ways to run it.
    /// </summary>
    /// <remarks>
    /// Bound once per recipe and then only re-evaluated for affordability. Ingredient chips
    /// are rebuilt in <see cref="Bind"/> and never touched again, because a recipe's inputs
    /// are baked data that cannot change at runtime.
    /// </remarks>
    public sealed class RecipeRowView : VisualElement
    {
        private readonly CraftingUIContext _context;
        private readonly Action<RecipeDefinition, bool> _onRun;

        private readonly VisualElement _icon;
        private readonly VisualElement _inputs;
        private readonly VisualElement _outputs;
        private readonly Label _name;
        private readonly Label _affordable;
        private readonly Button _craft;
        private readonly Button _enqueue;

        private int _shownAffordable = -1;

        /// <summary>The recipe this row currently shows, or null.</summary>
        public RecipeDefinition Recipe { get; private set; }

        /// <summary>How many repetitions the input container could pay for. Updated on refresh.</summary>
        public int AffordableCount { get; private set; }

        /// <param name="onRun">Called with the recipe and true when the click was Queue.</param>
        public RecipeRowView(CraftingUIContext context, Action<RecipeDefinition, bool> onRun)
        {
            _context = context;
            _onRun = onRun;

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

            _icon = CraftingUIStyle.Icon(theme, null, theme.IconSize * 0.8f);
            _icon.style.marginRight = theme.Spacing;
            row.Add(_icon);

            VisualElement body = CraftingUIStyle.Column();
            body.style.flexGrow = 1f;
            row.Add(body);

            VisualElement titleRow = CraftingUIStyle.Row(theme);
            body.Add(titleRow);

            _name = CraftingUIStyle.Body(theme, string.Empty);
            _name.style.flexGrow = 1f;
            _name.style.overflow = Overflow.Hidden;
            titleRow.Add(_name);

            _affordable = CraftingUIStyle.Muted(theme, string.Empty);
            titleRow.Add(_affordable);

            VisualElement flowRow = CraftingUIStyle.Row(theme);
            flowRow.style.marginTop = 2f;
            flowRow.style.flexWrap = Wrap.Wrap;
            body.Add(flowRow);

            _inputs = CraftingUIStyle.Row(theme, "inputs");
            flowRow.Add(_inputs);

            Label arrow = CraftingUIStyle.Muted(theme, "  \u2192  ");
            flowRow.Add(arrow);

            _outputs = CraftingUIStyle.Row(theme, "outputs");
            flowRow.Add(_outputs);

            VisualElement actions = CraftingUIStyle.Row(theme);
            actions.style.marginLeft = theme.Spacing;
            row.Add(actions);

            _craft = CraftingUIStyle.Action(theme, CraftingUIStrings.Craft, () => _onRun?.Invoke(Recipe, false));
            _enqueue = CraftingUIStyle.Action(theme, CraftingUIStrings.Enqueue, () => _onRun?.Invoke(Recipe, true));
            _enqueue.style.marginRight = 0f;
            actions.Add(_craft);
            actions.Add(_enqueue);
        }

        /// <summary>Points the row at a recipe and rebuilds its static parts.</summary>
        public void Bind(RecipeDefinition recipe)
        {
            if (ReferenceEquals(Recipe, recipe))
                return;

            Recipe = recipe;
            _shownAffordable = -1;

            if (recipe == null)
            {
                _name.text = string.Empty;
                _inputs.Clear();
                _outputs.Clear();
                return;
            }

            _name.text = _context.Display.GetRecipeName(recipe);
            CraftingUIStyle.SetIcon(_icon, _context.Theme, _context.Display.GetRecipeIcon(recipe));

            BuildChips(_inputs, recipe.Inputs);
            BuildChips(_outputs, recipe.Outputs);
        }

        /// <summary>
        /// Re-reads how many times this recipe could run. Sampled, not event driven: the
        /// containers it depends on announce nothing when their contents move.
        /// </summary>
        public void RefreshAffordability()
        {
            if (Recipe == null)
                return;

            CraftingId id = Recipe.Id;
            AffordableCount = _context.Machines.GetMaxCraftableCount(in id);

            if (_shownAffordable == AffordableCount)
                return;

            _shownAffordable = AffordableCount;

            CraftingUITheme theme = _context.Theme;
            bool affordable = AffordableCount > 0;

            _affordable.text = affordable
                ? string.Format(CraftingUIStrings.AffordableFormat, _context.Display.FormatAmount(AffordableCount))
                : CraftingUIStrings.NoneAffordable;
            _affordable.style.color = affordable ? theme.Running : theme.TextDisabled;
            _name.style.color = affordable ? theme.TextPrimary : theme.TextDisabled;

            CraftingUIStyle.SetEnabledLook(_craft, theme, affordable && _context.Machines.HasFreeSlot);
            CraftingUIStyle.SetEnabledLook(_enqueue, theme, affordable && _context.Machines.QueueCapacity > 0);
        }

        private void BuildChips(VisualElement parent, ItemStack[] stacks)
        {
            parent.Clear();
            if (stacks == null)
                return;

            CraftingUITheme theme = _context.Theme;

            for (int i = 0; i < stacks.Length; i++)
            {
                if (stacks[i].IsEmpty)
                    continue;

                VisualElement chip = CraftingUIStyle.Row(theme);
                chip.style.marginRight = theme.SpacingTight;

                VisualElement icon = CraftingUIStyle.Icon(theme, _context.Display.GetItemIcon(stacks[i].Item), 18f);
                chip.Add(icon);

                Label amount = CraftingUIStyle.Muted(
                    theme,
                    string.Format(CraftingUIStrings.AmountFormat, _context.Display.FormatAmount(stacks[i].Amount)));
                amount.style.marginLeft = 2f;
                chip.Add(amount);

                chip.tooltip = _context.Display.GetItemName(stacks[i].Item);
                parent.Add(chip);
            }
        }
    }
}