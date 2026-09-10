using System;
using UnityEngine;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// A single craftable or consumable thing: an ingot, a tomato, a cooked stew.
    /// The system draws no distinction between "item" and "food"; use tags for that.
    /// </summary>
    [CreateAssetMenu(fileName = "Item_", menuName = LoopRoutes.CraftRoute + "/Item", order = 100)]
    public sealed class ItemDefinition : CraftingDefinition
    {
        private static readonly CraftingId[] EmptyTags = new CraftingId[0];

        [SerializeField, TextArea(2, 5)]
        private string _description = string.Empty;

        [SerializeField, Tooltip("Optional. The crafting system never reads this; it is here for your UI.")]
        private Sprite _icon;

        [SerializeField, Min(0), Tooltip("Declarative only. 0 means unlimited. Enforcement belongs to your IItemContainer.")]
        private int _maxStack;

        [SerializeField, Tooltip("Free-form labels: food, metal, perishable, tier2... Used for filtering and for machine rules.")]
        private string[] _tags = Array.Empty<string>();

        [NonSerialized] private CraftingId[] _tagIds;

        public string Description => _description ?? string.Empty;

        public Sprite Icon => _icon;

        /// <summary>Zero means the system imposes no ceiling.</summary>
        public int MaxStack => _maxStack;

        /// <summary>Hashed tags, built once on first access.</summary>
        public CraftingId[] Tags
        {
            get
            {
                if (_tagIds == null)
                    BuildTags();

                return _tagIds;
            }
        }

        /// <summary>O(n) over a handful of tags. Fine for validation, avoid in a per-frame loop.</summary>
        public bool HasTag(in CraftingId tag)
        {
            if (!tag.IsValid)
                return false;

            CraftingId[] tags = Tags;
            for (int i = 0; i < tags.Length; i++)
            {
                if (tags[i].Equals(tag))
                    return true;
            }

            return false;
        }

        public bool HasTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return false;

            var id = new CraftingId(tag);
            return HasTag(in id);
        }

        protected override bool ValidateContent() => true;

        private void BuildTags()
        {
            if (_tags == null || _tags.Length == 0)
            {
                _tagIds = EmptyTags;
                return;
            }

            // Count valid entries first so the final array has no holes.
            int valid = 0;
            for (int i = 0; i < _tags.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(_tags[i]))
                    valid++;
            }

            if (valid == 0)
            {
                _tagIds = EmptyTags;
                return;
            }

            var built = new CraftingId[valid];
            int cursor = 0;
            for (int i = 0; i < _tags.Length; i++)
            {
                string raw = _tags[i];
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                built[cursor++] = new CraftingId(raw.Trim());
            }

            _tagIds = built;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            _tagIds = null;
        }

#if UNITY_EDITOR
        /// <summary>Editor-only content mutation, used by the edit-mode toolkit and JSON importer.</summary>
        internal void EditorSetContent(string description, Sprite icon, int maxStack, string[] tags)
        {
            _description = description ?? string.Empty;
            _icon = icon;
            _maxStack = maxStack < 0 ? 0 : maxStack;
            _tags = tags ?? Array.Empty<string>();
            _tagIds = null;
        }
#endif
    }
}