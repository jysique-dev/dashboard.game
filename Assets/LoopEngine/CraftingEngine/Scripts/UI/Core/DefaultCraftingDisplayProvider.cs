using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>
    /// A display provider built on a flat catalogue of item assets. Replace it with your own
    /// implementation when the project already has a localisation table or an item database.
    /// </summary>
    /// <remarks>
    /// Lookups go through a dictionary keyed with <see cref="CraftingIdComparer"/>, which
    /// reuses the id's precomputed FNV hash instead of re-hashing the string on every access.
    /// Names come from the asset's object name, because no definition type declares a display
    /// name field. Override <see cref="GetItemName"/> in a subclass to plug in localisation.
    /// </remarks>
    public class DefaultCraftingDisplayProvider : ICraftingDisplayProvider
    {
        private const string UnknownLabel = "???";

        private readonly Dictionary<CraftingId, ItemDefinition> _items;
        private readonly StringBuilder _builder = new StringBuilder(32);
        private readonly Dictionary<string, string> _nameCache = new Dictionary<string, string>(64);

        /// <summary>Removes an author prefix such as "Item_" from asset names when true.</summary>
        public bool StripAssetPrefix { get; set; } = true;

        /// <summary>
        /// When true, an id missing from the catalogue shows its authoring string rather than
        /// a placeholder. Easier to debug, and usually readable enough to ship.
        /// </summary>
        public bool FallBackToRawId { get; set; } = true;

        public DefaultCraftingDisplayProvider(IReadOnlyList<ItemDefinition> items)
        {
            int capacity = items?.Count ?? 0;
            _items = new Dictionary<CraftingId, ItemDefinition>(capacity, CraftingIdComparer.Instance);

            if (items == null)
                return;

            for (int i = 0; i < items.Count; i++)
                Register(items[i]);
        }

        /// <summary>Adds an item to the catalogue. Later entries win on a duplicate id.</summary>
        public void Register(ItemDefinition item)
        {
            if (item == null || !item.Id.IsValid)
                return;

            _items[item.Id] = item;
        }

        /// <summary>Drops an item from the catalogue.</summary>
        public bool Unregister(ItemDefinition item)
            => item != null && item.Id.IsValid && _items.Remove(item.Id);

        /// <summary>Every item this provider can name and draw.</summary>
        public IReadOnlyCollection<ItemDefinition> CatalogueItems => _items.Values;

        public ItemDefinition ResolveItem(in CraftingId item)
        {
            if (!item.IsValid)
                return null;

            return _items.TryGetValue(item, out ItemDefinition definition) ? definition : null;
        }

        public virtual string GetItemName(in CraftingId item)
        {
            ItemDefinition definition = ResolveItem(in item);
            if (definition != null)
                return Nicify(definition.name);

            if (FallBackToRawId && item.IsValid)
                return Nicify(item.Value);

            return UnknownLabel;
        }

        public virtual Sprite GetItemIcon(in CraftingId item)
        {
            ItemDefinition definition = ResolveItem(in item);
            return definition == null ? null : definition.Icon;
        }

        public virtual string GetRecipeName(RecipeDefinition recipe)
            => recipe == null ? UnknownLabel : Nicify(recipe.name);

        public virtual Sprite GetRecipeIcon(RecipeDefinition recipe)
        {
            if (recipe == null)
                return null;

            // Recipes carry no sprite of their own, so the first output stands in for it.
            ItemStack[] outputs = recipe.Outputs;
            for (int i = 0; i < outputs.Length; i++)
            {
                if (outputs[i].IsEmpty)
                    continue;

                Sprite icon = GetItemIcon(outputs[i].Item);
                if (icon != null)
                    return icon;
            }

            return null;
        }

        public virtual string GetMachineName(MachineDefinition machine)
            => machine == null ? UnknownLabel : Nicify(machine.name);

        public virtual Sprite GetMachineIcon(MachineDefinition machine)
            => machine == null ? null : machine.Icon;

        public virtual string GetModifierName(ModifierDefinition modifier)
            => modifier == null ? UnknownLabel : Nicify(modifier.name);

        public virtual string GetStatusText(CraftingStatus status)
            => CraftingStatusText.Get(status);

        public virtual string FormatAmount(int amount)
        {
            if (amount < 1000)
                return amount.ToString();

            if (amount < 1000000)
                return (amount / 1000f).ToString("0.#") + "k";

            return (amount / 1000000f).ToString("0.#") + "M";
        }

        public virtual string FormatSeconds(float seconds)
        {
            if (seconds < 0f)
                seconds = 0f;

            if (seconds < 10f)
                return seconds.ToString("0.0") + "s";

            if (seconds < 60f)
                return Mathf.CeilToInt(seconds) + "s";

            int total = Mathf.CeilToInt(seconds);
            return total / 60 + ":" + (total % 60).ToString("00");
        }

        /// <summary>
        /// Turns "Item_IronIngot" into "Iron Ingot". Cached, because labels are re-read every
        /// time a row is bound and the answer never changes for a given input.
        /// </summary>
        protected string Nicify(string assetName)
        {
            if (string.IsNullOrEmpty(assetName))
                return UnknownLabel;

            if (_nameCache.TryGetValue(assetName, out string cached))
                return cached;

            string working = assetName;
            if (StripAssetPrefix)
            {
                int underscore = working.IndexOf('_');
                if (underscore >= 0 && underscore < working.Length - 1)
                    working = working.Substring(underscore + 1);
            }

            string result = SplitPascalCase(working);
            _nameCache[assetName] = result;
            return result;
        }

        /// <summary>Inserts spaces between words. Returns the input untouched when it has none.</summary>
        protected string SplitPascalCase(string source)
        {
            if (string.IsNullOrEmpty(source))
                return UnknownLabel;

            bool needsSplit = false;
            for (int i = 1; i < source.Length; i++)
            {
                if (char.IsUpper(source[i]) && !char.IsUpper(source[i - 1]))
                {
                    needsSplit = true;
                    break;
                }
            }

            if (!needsSplit)
                return source;

            _builder.Clear();
            _builder.Append(source[0]);

            for (int i = 1; i < source.Length; i++)
            {
                char c = source[i];
                if (char.IsUpper(c) && !char.IsUpper(source[i - 1]) && source[i - 1] != ' ')
                    _builder.Append(' ');

                _builder.Append(c);
            }

            return _builder.ToString();
        }
    }
}