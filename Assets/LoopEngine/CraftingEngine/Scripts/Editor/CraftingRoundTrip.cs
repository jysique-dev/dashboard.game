using System;
using System.Collections.Generic;

namespace LoopEngine.CraftingEngine.EditorTools
{
    /// <summary>
    /// Checks that writing a document and reading it back produces the same thing.
    /// </summary>
    /// <remarks>
    /// Two independent checks, because they fail for different reasons:
    ///
    /// The text check serialises, parses and serialises again, then compares the strings. It
    /// catches anything the JSON layer itself loses, such as a field the parser cannot round
    /// trip or a default that overwrites a real value.
    ///
    /// The semantic check compares the two documents entry by entry and says exactly which
    /// field of which entry changed, which a string diff cannot.
    ///
    /// Neither touches the asset database, so this is safe to run at any time.
    /// </remarks>
    public static class CraftingRoundTrip
    {
        /// <summary>Runs both checks. Returns true when the document survives a round trip.</summary>
        public static bool Verify(CraftingJsonDocument original, CraftingIssues issues)
        {
            if (original == null || issues == null)
                return false;

            string first = CraftingJsonParser.ToJson(original);

            if (!CraftingJsonParser.TryParse(first, out CraftingJsonDocument parsed, issues))
            {
                issues.Error("The exported text could not be parsed back. Export is not round trip safe.");
                return false;
            }

            string second = CraftingJsonParser.ToJson(parsed);

            bool textMatches = string.Equals(first, second, StringComparison.Ordinal);
            if (!textMatches)
                issues.Error("Re-serialising the parsed document produced different text.");

            int differences = Compare(original, parsed, issues);

            if (textMatches && differences == 0)
                issues.Info($"Round trip clean: {original.EntryCount} entry(ies) survived unchanged.");

            return textMatches && differences == 0;
        }

        /// <summary>Compares two documents and reports every difference. Returns how many.</summary>
        public static int Compare(CraftingJsonDocument a, CraftingJsonDocument b, CraftingIssues issues)
        {
            int differences = 0;

            differences += CompareSection(
                a.items, b.items, "item", e => e.id, issues,
                (x, y, id, list) => CompareItem(x, y, id, list));

            differences += CompareSection(
                a.categories, b.categories, "category", e => e.id, issues,
                (x, y, id, list) => CompareCategory(x, y, id, list));

            differences += CompareSection(
                a.recipes, b.recipes, "recipe", e => e.id, issues,
                (x, y, id, list) => CompareRecipe(x, y, id, list));

            differences += CompareSection(
                a.machines, b.machines, "machine", e => e.id, issues,
                (x, y, id, list) => CompareMachine(x, y, id, list));

            differences += CompareSection(
                a.modifiers, b.modifiers, "modifier", e => e.id, issues,
                (x, y, id, list) => CompareModifier(x, y, id, list));

            return differences;
        }

        private static int CompareSection<T>(
            T[] left,
            T[] right,
            string section,
            Func<T, string> getId,
            CraftingIssues issues,
            Func<T, T, string, CraftingIssues, int> compare) where T : class
        {
            left ??= Array.Empty<T>();
            right ??= Array.Empty<T>();

            int differences = 0;

            if (left.Length != right.Length)
            {
                issues.Error($"{section} count changed: {left.Length} written, {right.Length} read back.");
                differences++;
            }

            var rightById = new Dictionary<string, T>(StringComparer.Ordinal);
            for (int i = 0; i < right.Length; i++)
            {
                string id = getId(right[i]);
                if (!string.IsNullOrEmpty(id))
                    rightById[id] = right[i];
            }

            for (int i = 0; i < left.Length; i++)
            {
                string id = getId(left[i]);

                if (!rightById.TryGetValue(id ?? string.Empty, out T match))
                {
                    issues.Error($"{section} '{id}' did not survive the round trip.");
                    differences++;
                    continue;
                }

                differences += compare(left[i], match, id, issues);
            }

            return differences;
        }

        // --- Per type ---------------------------------------------------------------------

        private static int CompareItem(ItemJson a, ItemJson b, string id, CraftingIssues issues)
        {
            int differences = 0;

            differences += Field(a.displayName, b.displayName, "item", id, "displayName", issues);
            differences += Field(a.description, b.description, "item", id, "description", issues);
            differences += Field(a.maxStack, b.maxStack, "item", id, "maxStack", issues);
            differences += Strings(a.tags, b.tags, "item", id, "tags", issues);

            return differences;
        }

        private static int CompareCategory(CategoryJson a, CategoryJson b, string id, CraftingIssues issues)
        {
            int differences = 0;

            differences += Field(a.displayName, b.displayName, "category", id, "displayName", issues);
            differences += Field(a.description, b.description, "category", id, "description", issues);

            return differences;
        }

        private static int CompareRecipe(RecipeJson a, RecipeJson b, string id, CraftingIssues issues)
        {
            int differences = 0;

            differences += Field(a.displayName, b.displayName, "recipe", id, "displayName", issues);
            differences += Field(a.craftSeconds, b.craftSeconds, "recipe", id, "craftSeconds", issues);
            differences += Field(a.requiredCategory, b.requiredCategory, "recipe", id, "requiredCategory", issues);
            differences += Amounts(a.inputs, b.inputs, "recipe", id, "inputs", issues);
            differences += Amounts(a.outputs, b.outputs, "recipe", id, "outputs", issues);

            return differences;
        }

        private static int CompareMachine(MachineJson a, MachineJson b, string id, CraftingIssues issues)
        {
            int differences = 0;

            differences += Field(a.displayName, b.displayName, "machine", id, "displayName", issues);
            differences += Field(a.parallelSlots, b.parallelSlots, "machine", id, "parallelSlots", issues);
            differences += Field(a.queueCapacity, b.queueCapacity, "machine", id, "queueCapacity", issues);
            differences += Field(a.speedMultiplier, b.speedMultiplier, "machine", id, "speedMultiplier", issues);
            differences += Field(a.modifierSlots, b.modifierSlots, "machine", id, "modifierSlots", issues);
            differences += Field(a.allowHandCraftedRecipes, b.allowHandCraftedRecipes, "machine", id, "allowHandCraftedRecipes", issues);
            differences += Strings(a.categories, b.categories, "machine", id, "categories", issues);

            return differences;
        }

        private static int CompareModifier(ModifierJson a, ModifierJson b, string id, CraftingIssues issues)
        {
            int differences = 0;

            differences += Field(a.displayName, b.displayName, "modifier", id, "displayName", issues);
            differences += Field(a.carrierItem, b.carrierItem, "modifier", id, "carrierItem", issues);
            differences += Field(a.maxPerMachine, b.maxPerMachine, "modifier", id, "maxPerMachine", issues);
            differences += Strings(a.allowedCategories, b.allowedCategories, "modifier", id, "allowedCategories", issues);
            differences += Amounts(a.bonusOutputs, b.bonusOutputs, "modifier", id, "bonusOutputs", issues);

            StatEntryJson[] left = a.entries ?? Array.Empty<StatEntryJson>();
            StatEntryJson[] right = b.entries ?? Array.Empty<StatEntryJson>();

            if (left.Length != right.Length)
            {
                issues.Error($"modifier '{id}' entries count changed: {left.Length} to {right.Length}.");
                return differences + 1;
            }

            for (int i = 0; i < left.Length; i++)
            {
                differences += Field(left[i].stat, right[i].stat, "modifier", id, $"entries[{i}].stat", issues);
                differences += Field(left[i].operation, right[i].operation, "modifier", id, $"entries[{i}].operation", issues);
                differences += Field(left[i].value, right[i].value, "modifier", id, $"entries[{i}].value", issues);
            }

            return differences;
        }

        // --- Primitives -------------------------------------------------------------------

        private static int Field<T>(T a, T b, string section, string id, string field, CraftingIssues issues)
        {
            if (EqualityComparer<T>.Default.Equals(a, b))
                return 0;

            issues.Error($"{section} '{id}' field '{field}' changed: '{a}' to '{b}'.");
            return 1;
        }

        private static int Strings(string[] a, string[] b, string section, string id, string field, CraftingIssues issues)
        {
            a ??= Array.Empty<string>();
            b ??= Array.Empty<string>();

            if (a.Length != b.Length)
            {
                issues.Error($"{section} '{id}' field '{field}' length changed: {a.Length} to {b.Length}.");
                return 1;
            }

            int differences = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                {
                    issues.Error($"{section} '{id}' field '{field}[{i}]' changed: '{a[i]}' to '{b[i]}'.");
                    differences++;
                }
            }

            return differences;
        }

        private static int Amounts(AmountJson[] a, AmountJson[] b, string section, string id, string field, CraftingIssues issues)
        {
            a ??= Array.Empty<AmountJson>();
            b ??= Array.Empty<AmountJson>();

            if (a.Length != b.Length)
            {
                issues.Error($"{section} '{id}' field '{field}' length changed: {a.Length} to {b.Length}.");
                return 1;
            }

            int differences = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (!string.Equals(a[i].item, b[i].item, StringComparison.Ordinal))
                {
                    issues.Error($"{section} '{id}' {field}[{i}] item changed: '{a[i].item}' to '{b[i].item}'.");
                    differences++;
                }

                if (a[i].amount != b[i].amount)
                {
                    issues.Error($"{section} '{id}' {field}[{i}] amount changed: {a[i].amount} to {b[i].amount}.");
                    differences++;
                }
            }

            return differences;
        }
    }
}