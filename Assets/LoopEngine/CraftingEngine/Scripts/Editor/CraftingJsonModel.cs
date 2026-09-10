using System;

namespace LoopEngine.CraftingEngine.EditorTools
{
    /// <summary>
    /// Root of a content file. Everything the importer and exporter speak in.
    /// </summary>
    /// <remarks>
    /// Plain classes with public fields, because <c>JsonUtility</c> ignores properties,
    /// readonly fields and dictionaries, and cannot deserialise a bare array at the top level.
    /// Field initialisers are the defaults for anything absent from the file: FromJson builds
    /// the object first and then overwrites only the fields the JSON actually contains.
    ///
    /// These types live in the editor assembly on purpose. Content JSON is an authoring
    /// format; nothing in a build should be able to read or write it.
    /// </remarks>
    [Serializable]
    public sealed class CraftingJsonDocument
    {
        /// <summary>Bump when the shape changes so old files can be migrated.</summary>
        public int version = 1;

        public ItemJson[] items = Array.Empty<ItemJson>();
        public CategoryJson[] categories = Array.Empty<CategoryJson>();
        public RecipeJson[] recipes = Array.Empty<RecipeJson>();
        public MachineJson[] machines = Array.Empty<MachineJson>();
        public ModifierJson[] modifiers = Array.Empty<ModifierJson>();

        public int EntryCount
            => Length(items) + Length(categories) + Length(recipes) + Length(machines) + Length(modifiers);

        public bool IsEmpty => EntryCount == 0;

        private static int Length(Array array) => array?.Length ?? 0;
    }

    /// <summary>An item. Maps to <see cref="ItemDefinition"/>.</summary>
    [Serializable]
    public sealed class ItemJson
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string description = string.Empty;

        /// <summary>Declarative only. 0 means no declared limit.</summary>
        public int maxStack;

        public string[] tags = Array.Empty<string>();
    }

    /// <summary>A machine category. Maps to <see cref="MachineCategoryDefinition"/>.</summary>
    [Serializable]
    public sealed class CategoryJson
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string description = string.Empty;
    }

    /// <summary>An item id with a count. Used on both sides of a recipe.</summary>
    [Serializable]
    public sealed class AmountJson
    {
        public string item = string.Empty;
        public int amount = 1;
    }

    /// <summary>A recipe. Maps to <see cref="RecipeDefinition"/>.</summary>
    [Serializable]
    public sealed class RecipeJson
    {
        public string id = string.Empty;
        public string displayName = string.Empty;

        public AmountJson[] inputs = Array.Empty<AmountJson>();
        public AmountJson[] outputs = Array.Empty<AmountJson>();

        public float craftSeconds = 1f;

        /// <summary>Empty means hand crafted, with no machine involved.</summary>
        public string requiredCategory = string.Empty;
    }

    /// <summary>A machine. Maps to <see cref="MachineDefinition"/>.</summary>
    [Serializable]
    public sealed class MachineJson
    {
        public string id = string.Empty;
        public string displayName = string.Empty;

        public string[] categories = Array.Empty<string>();

        public int parallelSlots = 1;
        public int queueCapacity = 4;
        public float speedMultiplier = 1f;
        public int modifierSlots;
        public bool allowHandCraftedRecipes;
    }

    /// <summary>One stat change inside a modifier.</summary>
    [Serializable]
    public sealed class StatEntryJson
    {
        /// <summary>"speed" or "yield". Case insensitive.</summary>
        public string stat = "speed";

        /// <summary>"additive" or "multiplicative". Case insensitive.</summary>
        public string operation = "additive";

        public float value;
    }

    /// <summary>An upgrade. Maps to <see cref="ModifierDefinition"/>.</summary>
    [Serializable]
    public sealed class ModifierJson
    {
        public string id = string.Empty;
        public string displayName = string.Empty;

        /// <summary>Item id the player installs. Empty for built-in upgrades.</summary>
        public string carrierItem = string.Empty;

        public StatEntryJson[] entries = Array.Empty<StatEntryJson>();
        public AmountJson[] bonusOutputs = Array.Empty<AmountJson>();
        public string[] allowedCategories = Array.Empty<string>();

        public int maxPerMachine = 1;
    }
}