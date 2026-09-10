using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.CraftingEngine.EditorTools
{
    /// <summary>
    /// Reads and writes content JSON, and checks that a parsed document makes sense on its own
    /// terms before anything touches the asset database.
    /// </summary>
    /// <remarks>
    /// <c>JsonUtility</c> reports malformed JSON as a single exception with no line or column,
    /// so syntax errors are surfaced as one message with the parser's own text. Everything the
    /// parser can check itself, it reports precisely: by section and entry index, with the id
    /// when there is one.
    /// </remarks>
    public static class CraftingJsonParser
    {
        /// <summary>Highest document version this parser understands.</summary>
        public const int SupportedVersion = 1;

        /// <summary>
        /// Parses a document. Returns false when the text is not valid JSON or the root object
        /// is missing; structural problems are reported through <paramref name="issues"/> without
        /// failing the parse, so the caller can decide what to do with a partially usable file.
        /// </summary>
        public static bool TryParse(string json, out CraftingJsonDocument document, CraftingIssues issues)
        {
            document = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                issues?.Error("The file is empty.");
                return false;
            }

            try
            {
                document = JsonUtility.FromJson<CraftingJsonDocument>(json);
            }
            catch (Exception exception)
            {
                // JsonUtility gives no position information, so the raw message is the most
                // useful thing available.
                issues?.Error("Malformed JSON: " + exception.Message);
                return false;
            }

            if (document == null)
            {
                issues?.Error("The root of the file is not a JSON object.");
                return false;
            }

            if (document.version > SupportedVersion)
            {
                issues?.Warning(
                    $"File declares version {document.version}; this parser understands up to "
                    + $"{SupportedVersion}. Unknown fields are ignored silently.");
            }

            if (document.IsEmpty)
                issues?.Warning("The document parsed correctly but contains no entries.");

            return true;
        }

        /// <summary>Writes a document. Pretty printed by default so files stay diffable.</summary>
        public static string ToJson(CraftingJsonDocument document, bool prettyPrint = true)
            => document == null ? string.Empty : JsonUtility.ToJson(document, prettyPrint);

        // --- Structural validation -------------------------------------------------------

        /// <summary>
        /// Checks the document against itself: ids present, unique per section, amounts sane,
        /// enum spellings understood, and references pointing at something.
        /// </summary>
        /// <remarks>
        /// References that do not resolve inside the document are reported as Info, not Error:
        /// a file that only adds recipes is legitimate as long as the items already exist as
        /// assets. The importer resolves against the project in session 7 and turns anything
        /// still unresolved into an error there.
        /// </remarks>
        public static void ValidateStructure(CraftingJsonDocument document, CraftingIssues issues)
        {
            if (document == null || issues == null)
                return;

            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            var categoryIds = new HashSet<string>(StringComparer.Ordinal);

            ValidateIds(document.items, "items", issues, itemIds, entry => entry.id);
            ValidateIds(document.categories, "categories", issues, categoryIds, entry => entry.id);
            ValidateIds(document.recipes, "recipes", issues, null, entry => entry.id);
            ValidateIds(document.machines, "machines", issues, null, entry => entry.id);
            ValidateIds(document.modifiers, "modifiers", issues, null, entry => entry.id);

            ValidateRecipes(document, issues, itemIds, categoryIds);
            ValidateMachines(document, issues, categoryIds);
            ValidateModifiers(document, issues, itemIds, categoryIds);
        }

        private static void ValidateIds<T>(
            T[] entries,
            string section,
            CraftingIssues issues,
            HashSet<string> collector,
            Func<T, string> getId)
        {
            if (entries == null)
                return;

            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < entries.Length; i++)
            {
                string id = getId(entries[i]);

                if (string.IsNullOrWhiteSpace(id))
                {
                    issues.Error($"{section}[{i}] has no id. It cannot be imported.");
                    continue;
                }

                if (!seen.Add(id))
                    issues.Error($"{section}[{i}] repeats the id '{id}' inside this file. Only the first is imported.");

                CraftingIdentityValidator.ValidateFormat(id, issues);
                collector?.Add(id);
            }
        }

        private static void ValidateRecipes(
            CraftingJsonDocument document,
            CraftingIssues issues,
            HashSet<string> itemIds,
            HashSet<string> categoryIds)
        {
            if (document.recipes == null)
                return;

            for (int i = 0; i < document.recipes.Length; i++)
            {
                RecipeJson recipe = document.recipes[i];
                string label = $"recipes[{i}] '{recipe.id}'";

                if (recipe.outputs == null || recipe.outputs.Length == 0)
                    issues.Error($"{label} has no outputs. A recipe that produces nothing is rejected.");

                ValidateAmounts(recipe.inputs, label + " inputs", issues, itemIds);
                ValidateAmounts(recipe.outputs, label + " outputs", issues, itemIds);

                if (recipe.craftSeconds < 0f)
                    issues.Error($"{label} has a negative duration ({recipe.craftSeconds}).");

                if (!string.IsNullOrEmpty(recipe.requiredCategory) && !categoryIds.Contains(recipe.requiredCategory))
                {
                    issues.Info(
                        $"{label} requires category '{recipe.requiredCategory}', which is not in this file. "
                        + "It must already exist as an asset.");
                }
            }
        }

        private static void ValidateAmounts(
            AmountJson[] amounts,
            string label,
            CraftingIssues issues,
            HashSet<string> itemIds)
        {
            if (amounts == null)
                return;

            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < amounts.Length; i++)
            {
                AmountJson amount = amounts[i];

                if (string.IsNullOrWhiteSpace(amount.item))
                {
                    issues.Error($"{label}[{i}] has no item id.");
                    continue;
                }

                if (amount.amount < 1)
                    issues.Error($"{label}[{i}] has an amount of {amount.amount}; it must be at least 1.");

                if (!seen.Add(amount.item))
                    issues.Warning($"{label}[{i}] repeats '{amount.item}'. The entries are not merged.");

                if (!itemIds.Contains(amount.item))
                    issues.Info($"{label}[{i}] references '{amount.item}', which is not in this file.");
            }
        }

        private static void ValidateMachines(
            CraftingJsonDocument document,
            CraftingIssues issues,
            HashSet<string> categoryIds)
        {
            if (document.machines == null)
                return;

            for (int i = 0; i < document.machines.Length; i++)
            {
                MachineJson machine = document.machines[i];
                string label = $"machines[{i}] '{machine.id}'";

                bool hasCategory = machine.categories != null && machine.categories.Length > 0;

                if (!hasCategory && !machine.allowHandCraftedRecipes)
                {
                    issues.Error(
                        $"{label} provides no category and does not allow hand crafted recipes, "
                        + "so it could never run anything.");
                }

                if (machine.parallelSlots < 1)
                    issues.Error($"{label} has {machine.parallelSlots} parallel slots; at least 1 is required.");

                if (machine.queueCapacity < 0)
                    issues.Error($"{label} has a negative queue capacity.");

                if (machine.speedMultiplier <= 0f)
                    issues.Error($"{label} has a speed multiplier of {machine.speedMultiplier}.");

                if (machine.modifierSlots < 0)
                    issues.Error($"{label} has a negative modifier slot count.");

                if (machine.categories == null)
                    continue;

                for (int c = 0; c < machine.categories.Length; c++)
                {
                    string category = machine.categories[c];

                    if (string.IsNullOrWhiteSpace(category))
                    {
                        issues.Warning($"{label} categories[{c}] is empty.");
                        continue;
                    }

                    if (!categoryIds.Contains(category))
                        issues.Info($"{label} provides '{category}', which is not in this file.");
                }
            }
        }

        private static void ValidateModifiers(
            CraftingJsonDocument document,
            CraftingIssues issues,
            HashSet<string> itemIds,
            HashSet<string> categoryIds)
        {
            if (document.modifiers == null)
                return;

            for (int i = 0; i < document.modifiers.Length; i++)
            {
                ModifierJson modifier = document.modifiers[i];
                string label = $"modifiers[{i}] '{modifier.id}'";

                bool hasEntries = modifier.entries != null && modifier.entries.Length > 0;
                bool hasBonus = modifier.bonusOutputs != null && modifier.bonusOutputs.Length > 0;

                if (!hasEntries && !hasBonus)
                    issues.Error($"{label} changes no stat and adds no output, so it does nothing.");

                if (modifier.maxPerMachine < 1)
                    issues.Error($"{label} has maxPerMachine of {modifier.maxPerMachine}; at least 1 is required.");

                if (!string.IsNullOrEmpty(modifier.carrierItem) && !itemIds.Contains(modifier.carrierItem))
                    issues.Info($"{label} is carried by '{modifier.carrierItem}', which is not in this file.");

                ValidateAmounts(modifier.bonusOutputs, label + " bonusOutputs", issues, itemIds);

                if (modifier.entries != null)
                {
                    for (int e = 0; e < modifier.entries.Length; e++)
                        ValidateStatEntry(modifier.entries[e], $"{label} entries[{e}]", issues);
                }

                if (modifier.allowedCategories == null)
                    continue;

                for (int c = 0; c < modifier.allowedCategories.Length; c++)
                {
                    string category = modifier.allowedCategories[c];
                    if (!string.IsNullOrWhiteSpace(category) && !categoryIds.Contains(category))
                        issues.Info($"{label} allows '{category}', which is not in this file.");
                }
            }
        }

        private static void ValidateStatEntry(StatEntryJson entry, string label, CraftingIssues issues)
        {
            if (!TryParseStat(entry.stat, out MachineStat stat))
            {
                issues.Error($"{label} has an unknown stat '{entry.stat}'. Expected 'speed' or 'yield'.");
                return;
            }

            if (!TryParseOperation(entry.operation, out StatOperation operation))
            {
                issues.Error(
                    $"{label} has an unknown operation '{entry.operation}'. "
                    + "Expected 'additive' or 'multiplicative'.");
                return;
            }

            if (operation == StatOperation.Multiplicative && entry.value <= 0f)
                issues.Error($"{label} multiplies {stat} by {entry.value}; the factor must be above zero.");

            if (operation == StatOperation.Additive && entry.value <= -1f)
                issues.Error($"{label} adds {entry.value} to {stat}, driving the result to zero or below.");
        }

        // --- Enum spelling ----------------------------------------------------------------

        /// <summary>Accepts "speed" or "yield", in any casing.</summary>
        public static bool TryParseStat(string value, out MachineStat stat)
            => Enum.TryParse(value, true, out stat) && Enum.IsDefined(typeof(MachineStat), stat);

        /// <summary>Accepts "additive" or "multiplicative", in any casing.</summary>
        public static bool TryParseOperation(string value, out StatOperation operation)
            => Enum.TryParse(value, true, out operation) && Enum.IsDefined(typeof(StatOperation), operation);

        /// <summary>Lowercase spelling used when writing files.</summary>
        public static string ToJsonName(MachineStat stat) => stat.ToString().ToLowerInvariant();

        /// <summary>Lowercase spelling used when writing files.</summary>
        public static string ToJsonName(StatOperation operation) => operation.ToString().ToLowerInvariant();
    }
}