using System;
using UnityEngine;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// A transformation: consume the inputs, wait, produce the outputs.
    /// Works identically for smelting an ingot and for cooking a stew; the only
    /// difference is which machine category is required.
    /// </summary>
    [CreateAssetMenu( fileName = "Recipe_", menuName = LoopRoutes.CraftRoute + "/Recipe", order = 110)]
    public sealed class RecipeDefinition : CraftingDefinition
    {
        private static readonly ItemStack[] EmptyStacks = new ItemStack[0];

        [SerializeField, Tooltip("Consumed when the job starts. May be empty for recipes that create from nothing.")]
        private ItemAmount[] _inputs = Array.Empty<ItemAmount>();

        [SerializeField, Tooltip("Produced when the job completes. At least one entry is required.")]
        private ItemAmount[] _outputs = Array.Empty<ItemAmount>();

        [SerializeField, Min(0f), Tooltip("Seconds of work at 1x speed. Zero completes on the next tick.")]
        private float _craftSeconds = 1f;

        [SerializeField, Tooltip("Machine capability required. Leave empty for hand crafting, with no machine involved.")]
        private MachineCategoryDefinition _requiredCategory;

        [NonSerialized] private ItemStack[] _bakedInputs;
        [NonSerialized] private ItemStack[] _bakedOutputs;

        /// <summary>Authored inputs, for the editor and the validator.</summary>
        public ItemAmount[] AuthoredInputs => _inputs ?? Array.Empty<ItemAmount>();

        /// <summary>Authored outputs, for the editor and the validator.</summary>
        public ItemAmount[] AuthoredOutputs => _outputs ?? Array.Empty<ItemAmount>();

        /// <summary>Runtime inputs. Built once, never null.</summary>
        public ItemStack[] Inputs
        {
            get
            {
                if (_bakedInputs == null)
                    _bakedInputs = ItemAmount.Bake(_inputs) ?? EmptyStacks;

                return _bakedInputs;
            }
        }

        /// <summary>Runtime outputs. Built once, never null.</summary>
        public ItemStack[] Outputs
        {
            get
            {
                if (_bakedOutputs == null)
                    _bakedOutputs = ItemAmount.Bake(_outputs) ?? EmptyStacks;

                return _bakedOutputs;
            }
        }

        /// <summary>Base duration in seconds, before any speed modifier.</summary>
        public float CraftSeconds => _craftSeconds < 0f ? 0f : _craftSeconds;

        /// <summary>Null when the recipe needs no machine at all.</summary>
        public MachineCategoryDefinition RequiredCategory => _requiredCategory;

        /// <summary>The required category's id, or <see cref="CraftingId.None"/> for hand crafting.</summary>
        public CraftingId RequiredCategoryId
            => _requiredCategory != null ? _requiredCategory.Id : CraftingId.None;

        /// <summary>True when no machine is needed.</summary>
        public bool IsHandCrafted => _requiredCategory == null;

        /// <summary>True when this recipe produces the given item.</summary>
        public bool Produces(in CraftingId item)
        {
            ItemStack[] outputs = Outputs;
            for (int i = 0; i < outputs.Length; i++)
            {
                if (outputs[i].Item.Equals(item))
                    return true;
            }

            return false;
        }

        /// <summary>True when this recipe consumes the given item.</summary>
        public bool Consumes(in CraftingId item)
        {
            ItemStack[] inputs = Inputs;
            for (int i = 0; i < inputs.Length; i++)
            {
                if (inputs[i].Item.Equals(item))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// A recipe is only registrable when every referenced item exists and it produces
        /// something. A recipe with no output would consume inputs forever for nothing.
        /// </summary>
        protected override bool ValidateContent()
        {
            if (_outputs == null || _outputs.Length == 0)
                return false;

            return ItemAmount.AllValid(_inputs) && ItemAmount.AllValid(_outputs);
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            ClampAll(_inputs);
            ClampAll(_outputs);

            if (_craftSeconds < 0f)
                _craftSeconds = 0f;

            _bakedInputs = null;
            _bakedOutputs = null;
        }

        private static void ClampAll(ItemAmount[] amounts)
        {
            if (amounts == null)
                return;

            for (int i = 0; i < amounts.Length; i++)
                amounts[i].ClampAmount();
        }

#if UNITY_EDITOR
        internal void EditorSetContent(
            ItemAmount[] inputs,
            ItemAmount[] outputs,
            float craftSeconds,
            MachineCategoryDefinition requiredCategory)
        {
            _inputs = inputs ?? Array.Empty<ItemAmount>();
            _outputs = outputs ?? Array.Empty<ItemAmount>();
            _craftSeconds = craftSeconds < 0f ? 0f : craftSeconds;
            _requiredCategory = requiredCategory;
            _bakedInputs = null;
            _bakedOutputs = null;
        }
#endif
    }
}