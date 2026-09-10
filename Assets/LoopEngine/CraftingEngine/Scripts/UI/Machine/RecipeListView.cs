using System.Collections.Generic;
using UnityEngine.UIElements;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>
    /// The recipe section: everything this machine is capable of running, with what the
    /// input container can currently pay for marked, and the two ways to launch one.
    /// </summary>
    /// <remarks>
    /// The row set is stable per machine, because a machine's runnable recipes come from its
    /// definition and do not change at runtime. Only affordability moves, so a refresh
    /// re-evaluates counts and toggles visibility instead of rebuilding elements.
    /// </remarks>
    public sealed class RecipeListView : VisualElement
    {
        /// <summary>Meaning "as many as the container can pay for" in the count selector.</summary>
        private const int MaxCount = -1;

        private static readonly int[] CountChoices = { 1, 5, 25, MaxCount };

        private readonly CraftingUIContext _context;
        private readonly List<RecipeRowView> _rows = new List<RecipeRowView>(16);
        private readonly List<Button> _countButtons = new List<Button>(4);
        private readonly ScrollView _scroll;
        private readonly Label _empty;
        private readonly Label _feedback;
        private readonly Button _filter;

        private int _count = 1;
        private bool _affordableOnly;
        private int _boundRecipes;

        public RecipeListView(CraftingUIContext context)
        {
            _context = context;
            CraftingUITheme theme = context.Theme;

            style.flexGrow = 1f;
            style.marginBottom = theme.Spacing;

            VisualElement headerRow = CraftingUIStyle.Row(theme);
            headerRow.style.marginBottom = theme.SpacingTight;
            Add(headerRow);

            Label caption = CraftingUIStyle.Body(theme, CraftingUIStrings.Recipes);
            caption.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            caption.style.flexGrow = 1f;
            headerRow.Add(caption);

            _filter = CraftingUIStyle.Action(theme, CraftingUIStrings.ShowAll, ToggleFilter);
            _filter.style.marginRight = 0f;
            headerRow.Add(_filter);

            VisualElement countRow = CraftingUIStyle.Row(theme);
            countRow.style.marginBottom = theme.SpacingTight;
            Add(countRow);

            for (int i = 0; i < CountChoices.Length; i++)
            {
                int choice = CountChoices[i];
                Button button = CraftingUIStyle.Action(
                    theme,
                    choice == MaxCount ? CraftingUIStrings.Max : "x" + choice,
                    () => SetCount(choice));

                _countButtons.Add(button);
                countRow.Add(button);
            }

            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.style.flexGrow = 1f;
            Add(_scroll);

            _empty = CraftingUIStyle.Muted(theme, CraftingUIStrings.NoRecipes);
            _empty.style.marginTop = theme.Spacing;
            Add(_empty);

            _feedback = CraftingUIStyle.Muted(theme, string.Empty);
            _feedback.style.marginTop = theme.SpacingTight;
            _feedback.style.whiteSpace = WhiteSpace.Normal;
            Add(_feedback);

            RefreshCountButtons();
        }

        /// <summary>Binds the rows to whatever the selected machine can run.</summary>
        public void Rebuild()
        {
            IReadOnlyList<RecipeDefinition> recipes = _context.Machines.GetRunnableRecipes();
            _boundRecipes = recipes.Count;

            while (_rows.Count < _boundRecipes)
            {
                var row = new RecipeRowView(_context, OnRun);
                _rows.Add(row);
                _scroll.Add(row);
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                if (i < _boundRecipes)
                {
                    _rows[i].Bind(recipes[i]);
                }
                else
                {
                    _rows[i].Bind(null);
                    _rows[i].style.display = DisplayStyle.None;
                }
            }

            _feedback.text = string.Empty;
            _empty.style.display = _boundRecipes == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _scroll.style.display = _boundRecipes == 0 ? DisplayStyle.None : DisplayStyle.Flex;

            Refresh();
        }

        /// <summary>
        /// Re-evaluates what is affordable. Driven by the container sampler, not by events:
        /// an <see cref="IItemContainer"/> announces nothing when its contents move.
        /// </summary>
        public void Refresh()
        {
            int visible = 0;

            for (int i = 0; i < _boundRecipes && i < _rows.Count; i++)
            {
                RecipeRowView row = _rows[i];
                row.RefreshAffordability();

                bool show = !_affordableOnly || row.AffordableCount > 0;
                row.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;

                if (show)
                    visible++;
            }

            if (_boundRecipes == 0)
                return;

            bool nothing = visible == 0;
            _empty.text = _affordableOnly ? CraftingUIStrings.NoneAffordable : CraftingUIStrings.NoRecipes;
            _empty.style.display = nothing ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnRun(RecipeDefinition recipe, bool enqueue)
        {
            if (recipe == null)
                return;

            int count = ResolveCount(recipe);
            if (count <= 0)
            {
                ShowFeedback(CraftingUIStrings.NoneAffordable, false);
                return;
            }

            CraftingId id = recipe.Id;
            string name = _context.Display.GetRecipeName(recipe);

            CraftingStatus status = enqueue
                ? _context.Machines.Enqueue(in id, count, out _)
                : _context.Machines.StartCraft(in id, count);

            if (status == CraftingStatus.Success)
            {
                string format = enqueue ? CraftingUIStrings.QueuedFormat : CraftingUIStrings.CraftedFormat;
                ShowFeedback(string.Format(format, count + " x " + name), true);
            }
            else
            {
                ShowFeedback(_context.Display.GetStatusText(status), false);
            }

            Refresh();
        }

        private int ResolveCount(RecipeDefinition recipe)
        {
            if (_count != MaxCount)
                return _count;

            // Resolved at click time, not at selection time: the container may have moved.
            CraftingId id = recipe.Id;
            return _context.Machines.GetMaxCraftableCount(in id);
        }

        private void SetCount(int count)
        {
            if (_count == count)
                return;

            _count = count;
            RefreshCountButtons();
        }

        private void RefreshCountButtons()
        {
            CraftingUITheme theme = _context.Theme;

            for (int i = 0; i < _countButtons.Count; i++)
            {
                bool selected = CountChoices[i] == _count;
                _countButtons[i].style.backgroundColor = selected ? theme.Accent : theme.SurfaceRaised;
                _countButtons[i].style.color = selected ? theme.Background : theme.TextPrimary;
            }
        }

        private void ToggleFilter()
        {
            _affordableOnly = !_affordableOnly;
            _filter.text = _affordableOnly ? CraftingUIStrings.ShowAffordable : CraftingUIStrings.ShowAll;
            Refresh();
        }

        private void ShowFeedback(string message, bool positive)
        {
            _feedback.text = message;
            _feedback.style.color = positive ? _context.Theme.Running : _context.Theme.Blocked;
        }
    }
}