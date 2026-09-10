using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoopEngine.CraftingEngine.EditorTools
{
    /// <summary>
    /// Builds a <see cref="CraftingJsonDocument"/> from authored assets.
    /// </summary>
    /// <remarks>
    /// The JSON format does not carry sprite references, so icons are not exported. That is not
    /// a bug to fix here: a sprite is a project asset, not content data, and there is no stable
    /// way to name one in a text file that survives the asset being moved. The importer knows
    /// this and preserves whatever icon an asset already has.
    /// </remarks>
    public static class CraftingJsonExporter
    {
        /// <summary>Every definition in the project.</summary>
        public static CraftingJsonDocument ExportAll(CraftingIssues issues = null)
        {
            return Export(
                CraftingAssetUtility.FindAll<ItemDefinition>(),
                CraftingAssetUtility.FindAll<MachineCategoryDefinition>(),
                CraftingAssetUtility.FindAll<RecipeDefinition>(),
                CraftingAssetUtility.FindAll<MachineDefinition>(),
                CraftingAssetUtility.FindAll<ModifierDefinition>(),
                issues);
        }

        /// <summary>Only what a database asset lists.</summary>
        public static CraftingJsonDocument ExportDatabase(CraftingDatabase database, CraftingIssues issues = null)
        {
            if (database == null)
                return new CraftingJsonDocument();

            return Export(
                new List<ItemDefinition>(database.Items),
                new List<MachineCategoryDefinition>(database.Categories),
                new List<RecipeDefinition>(database.Recipes),
                new List<MachineDefinition>(database.Machines),
                new List<ModifierDefinition>(database.Modifiers),
                issues);
        }

        /// <summary>Only the definitions currently selected in the Project window.</summary>
        public static CraftingJsonDocument ExportSelection(CraftingIssues issues = null)
        {
            var items = new List<ItemDefinition>();
            var categories = new List<MachineCategoryDefinition>();
            var recipes = new List<RecipeDefinition>();
            var machines = new List<MachineDefinition>();
            var modifiers = new List<ModifierDefinition>();

            Object[] selection = Selection.GetFiltered(typeof(CraftingDefinition), SelectionMode.Assets);

            for (int i = 0; i < selection.Length; i++)
            {
                switch (selection[i])
                {
                    case ItemDefinition item: items.Add(item); break;
                    case MachineCategoryDefinition category: categories.Add(category); break;
                    case RecipeDefinition recipe: recipes.Add(recipe); break;
                    case MachineDefinition machine: machines.Add(machine); break;
                    case ModifierDefinition modifier: modifiers.Add(modifier); break;
                }
            }

            return Export(items, categories, recipes, machines, modifiers, issues);
        }

        // --- Core -------------------------------------------------------------------------

        public static CraftingJsonDocument Export(
            List<ItemDefinition> items,
            List<MachineCategoryDefinition> categories,
            List<RecipeDefinition> recipes,
            List<MachineDefinition> machines,
            List<ModifierDefinition> modifiers,
            CraftingIssues issues = null)
        {
            var document = new CraftingJsonDocument
            {
                version = CraftingJsonParser.SupportedVersion,
                items = ConvertItems(items, issues),
                categories = ConvertCategories(categories, issues),
                recipes = ConvertRecipes(recipes, issues),
                machines = ConvertMachines(machines, issues),
                modifiers = ConvertModifiers(modifiers, issues)
            };

            return document;
        }

        private static ItemJson[] ConvertItems(List<ItemDefinition> source, CraftingIssues issues)
        {
            var result = new List<ItemJson>(source?.Count ?? 0);
            if (source == null)
                return result.ToArray();

            for (int i = 0; i < source.Count; i++)
            {
                ItemDefinition item = source[i];
                if (!Usable(item, "item", issues))
                    continue;

                CraftingId[] tags = item.Tags;
                var tagStrings = new string[tags.Length];
                for (int t = 0; t < tags.Length; t++)
                    tagStrings[t] = tags[t].Value;

                result.Add(new ItemJson
                {
                    id = item.RawId,
                    displayName = item.RawDisplayName,
                    description = item.Description,
                    maxStack = item.MaxStack,
                    tags = tagStrings
                });
            }

            return result.ToArray();
        }

        private static CategoryJson[] ConvertCategories(List<MachineCategoryDefinition> source, CraftingIssues issues)
        {
            var result = new List<CategoryJson>(source?.Count ?? 0);
            if (source == null)
                return result.ToArray();

            for (int i = 0; i < source.Count; i++)
            {
                MachineCategoryDefinition category = source[i];
                if (!Usable(category, "category", issues))
                    continue;

                result.Add(new CategoryJson
                {
                    id = category.RawId,
                    displayName = category.RawDisplayName,
                    description = category.Description
                });
            }

            return result.ToArray();
        }

        private static RecipeJson[] ConvertRecipes(List<RecipeDefinition> source, CraftingIssues issues)
        {
            var result = new List<RecipeJson>(source?.Count ?? 0);
            if (source == null)
                return result.ToArray();

            for (int i = 0; i < source.Count; i++)
            {
                RecipeDefinition recipe = source[i];
                if (!Usable(recipe, "recipe", issues))
                    continue;

                result.Add(new RecipeJson
                {
                    id = recipe.RawId,
                    displayName = recipe.RawDisplayName,
                    inputs = ConvertAmounts(recipe.AuthoredInputs, recipe, "input", issues),
                    outputs = ConvertAmounts(recipe.AuthoredOutputs, recipe, "output", issues),
                    craftSeconds = recipe.CraftSeconds,
                    requiredCategory = recipe.RequiredCategory != null ? recipe.RequiredCategory.RawId : string.Empty
                });
            }

            return result.ToArray();
        }

        private static MachineJson[] ConvertMachines(List<MachineDefinition> source, CraftingIssues issues)
        {
            var result = new List<MachineJson>(source?.Count ?? 0);
            if (source == null)
                return result.ToArray();

            for (int i = 0; i < source.Count; i++)
            {
                MachineDefinition machine = source[i];
                if (!Usable(machine, "machine", issues))
                    continue;

                MachineCategoryDefinition[] authored = machine.AuthoredCategories;
                var categories = new List<string>(authored.Length);

                for (int c = 0; c < authored.Length; c++)
                {
                    if (authored[c] == null || string.IsNullOrEmpty(authored[c].RawId))
                    {
                        issues?.Warning($"machine '{machine.RawId}' has an empty category slot; it was not exported.", machine);
                        continue;
                    }

                    categories.Add(authored[c].RawId);
                }

                result.Add(new MachineJson
                {
                    id = machine.RawId,
                    displayName = machine.RawDisplayName,
                    categories = categories.ToArray(),
                    parallelSlots = machine.ParallelSlots,
                    queueCapacity = machine.QueueCapacity,
                    speedMultiplier = machine.SpeedMultiplier,
                    modifierSlots = machine.ModifierSlots,
                    allowHandCraftedRecipes = machine.AllowsHandCraftedRecipes
                });
            }

            return result.ToArray();
        }

        private static ModifierJson[] ConvertModifiers(List<ModifierDefinition> source, CraftingIssues issues)
        {
            var result = new List<ModifierJson>(source?.Count ?? 0);
            if (source == null)
                return result.ToArray();

            for (int i = 0; i < source.Count; i++)
            {
                ModifierDefinition modifier = source[i];
                if (!Usable(modifier, "modifier", issues))
                    continue;

                StatModifierEntry[] entries = modifier.Entries;
                var entryJson = new StatEntryJson[entries.Length];

                for (int e = 0; e < entries.Length; e++)
                {
                    entryJson[e] = new StatEntryJson
                    {
                        stat = CraftingJsonParser.ToJsonName(entries[e].Stat),
                        operation = CraftingJsonParser.ToJsonName(entries[e].Operation),
                        value = entries[e].Value
                    };
                }

                result.Add(new ModifierJson
                {
                    id = modifier.RawId,
                    displayName = modifier.RawDisplayName,
                    carrierItem = modifier.CarrierItem != null ? modifier.CarrierItem.RawId : string.Empty,
                    entries = entryJson,
                    bonusOutputs = ConvertStacks(modifier.BonusOutputs, modifier, issues),
                    allowedCategories = ConvertAllowedCategories(modifier, issues),
                    maxPerMachine = modifier.MaxPerMachine
                });
            }

            return result.ToArray();
        }

        private static string[] ConvertAllowedCategories(ModifierDefinition modifier, CraftingIssues issues)
        {
            // AppliesTo hides the authored list, so it is read back through the serialized field.
            var serialized = new SerializedObject(modifier);
            SerializedProperty list = serialized.FindProperty("_allowedCategories");

            var result = new List<string>(list.arraySize);

            for (int i = 0; i < list.arraySize; i++)
            {
                var category = list.GetArrayElementAtIndex(i).objectReferenceValue as MachineCategoryDefinition;

                if (category == null || string.IsNullOrEmpty(category.RawId))
                {
                    issues?.Warning($"modifier '{modifier.RawId}' has an empty allowed category; it was not exported.", modifier);
                    continue;
                }

                result.Add(category.RawId);
            }

            serialized.Dispose();
            return result.ToArray();
        }

        private static AmountJson[] ConvertAmounts(
            ItemAmount[] amounts,
            CraftingDefinition owner,
            string label,
            CraftingIssues issues)
        {
            var result = new List<AmountJson>(amounts.Length);

            for (int i = 0; i < amounts.Length; i++)
            {
                ItemAmount amount = amounts[i];

                if (amount.Item == null || string.IsNullOrEmpty(amount.Item.RawId))
                {
                    issues?.Warning($"'{owner.RawId}' {label} {i + 1} has no item; it was not exported.", owner);
                    continue;
                }

                result.Add(new AmountJson { item = amount.Item.RawId, amount = amount.Amount });
            }

            return result.ToArray();
        }

        private static AmountJson[] ConvertStacks(ItemStack[] stacks, CraftingDefinition owner, CraftingIssues issues)
        {
            var result = new List<AmountJson>(stacks.Length);

            for (int i = 0; i < stacks.Length; i++)
            {
                if (stacks[i].IsEmpty)
                {
                    issues?.Warning($"'{owner.RawId}' bonus output {i + 1} is empty; it was not exported.", owner);
                    continue;
                }

                result.Add(new AmountJson { item = stacks[i].Item.Value, amount = stacks[i].Amount });
            }

            return result.ToArray();
        }

        private static bool Usable(CraftingDefinition definition, string section, CraftingIssues issues)
        {
            if (definition == null)
                return false;

            if (string.IsNullOrEmpty(definition.RawId))
            {
                issues?.Error($"A {section} asset named '{definition.name}' has no id and was not exported.", definition);
                return false;
            }

            return true;
        }
    }
}