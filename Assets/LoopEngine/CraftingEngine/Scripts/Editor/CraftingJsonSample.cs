using System.IO;
using UnityEditor;
using UnityEngine;

namespace LoopEngine.CraftingEngine.EditorTools
{
    /// <summary>
    /// A complete, valid example document, plus the menu entries to write one out and to check
    /// a file without importing it.
    /// </summary>
    /// <remarks>
    /// The example is built in code rather than stored as a text asset so it can never drift
    /// from the DTOs: if a field is renamed, this stops compiling.
    /// </remarks>
    public static class CraftingJsonSample
    {
        private const string MenuRoot = LoopRoutes.CraftingToolRoute + "/JSON/";

        /// <summary>Builds a small but complete document exercising every section.</summary>
        public static CraftingJsonDocument Build()
        {
            return new CraftingJsonDocument
            {
                version = CraftingJsonParser.SupportedVersion,

                items = new[]
                {
                    new ItemJson
                    {
                        id = "item.iron_ore",
                        displayName = "Iron Ore",
                        description = "Raw ore, mined from surface deposits.",
                        maxStack = 100,
                        tags = new[] { "ore", "metal" }
                    },
                    new ItemJson
                    {
                        id = "item.coal",
                        displayName = "Coal",
                        description = "Burns hot enough to smelt iron.",
                        maxStack = 100,
                        tags = new[] { "fuel" }
                    },
                    new ItemJson
                    {
                        id = "item.iron_ingot",
                        displayName = "Iron Ingot",
                        maxStack = 50,
                        tags = new[] { "metal", "refined" }
                    },
                    new ItemJson
                    {
                        id = "item.speed_module",
                        displayName = "Speed Module",
                        maxStack = 10,
                        tags = new[] { "upgrade" }
                    }
                },

                categories = new[]
                {
                    new CategoryJson
                    {
                        id = "category.furnace",
                        displayName = "Furnace",
                        description = "Anything that applies heat to raw materials."
                    }
                },

                recipes = new[]
                {
                    new RecipeJson
                    {
                        id = "recipe.iron_ingot",
                        displayName = "Iron Ingot",
                        inputs = new[]
                        {
                            new AmountJson { item = "item.iron_ore", amount = 2 },
                            new AmountJson { item = "item.coal", amount = 1 }
                        },
                        outputs = new[]
                        {
                            new AmountJson { item = "item.iron_ingot", amount = 1 }
                        },
                        craftSeconds = 2f,
                        requiredCategory = "category.furnace"
                    }
                },

                machines = new[]
                {
                    new MachineJson
                    {
                        id = "machine.furnace",
                        displayName = "Stone Furnace",
                        categories = new[] { "category.furnace" },
                        parallelSlots = 1,
                        queueCapacity = 4,
                        speedMultiplier = 1f,
                        modifierSlots = 2,
                        allowHandCraftedRecipes = false
                    }
                },

                modifiers = new[]
                {
                    new ModifierJson
                    {
                        id = "modifier.speed_1",
                        displayName = "Speed Module I",
                        carrierItem = "item.speed_module",
                        entries = new[]
                        {
                            new StatEntryJson
                            {
                                stat = CraftingJsonParser.ToJsonName(MachineStat.Speed),
                                operation = CraftingJsonParser.ToJsonName(StatOperation.Additive),
                                value = 0.25f
                            }
                        },
                        allowedCategories = new[] { "category.furnace" },
                        maxPerMachine = 2
                    }
                }
            };
        }

        [MenuItem(MenuRoot + "Write Example File...", false, 300)]
        public static void WriteExample()
        {
            string path = EditorUtility.SaveFilePanel(
                "Write example crafting JSON",
                Application.dataPath,
                "crafting_example",
                "json");

            if (string.IsNullOrEmpty(path))
                return;

            string json = CraftingJsonParser.ToJson(Build());

            try
            {
                File.WriteAllText(path, json);
            }
            catch (IOException exception)
            {
                Debug.LogError($"[Crafting] Could not write '{path}': {exception.Message}");
                return;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[Crafting] Example written to {path}.");
        }

        /// <summary>
        /// Parses and structurally checks a file without touching the asset database.
        /// The safe way to see what an import would complain about.
        /// </summary>
        [MenuItem(MenuRoot + "Validate File...", false, 301)]
        public static void ValidateFile()
        {
            string path = EditorUtility.OpenFilePanel("Validate crafting JSON", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path))
                return;

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (IOException exception)
            {
                Debug.LogError($"[Crafting] Could not read '{path}': {exception.Message}");
                return;
            }

            var issues = new CraftingIssues();

            if (!CraftingJsonParser.TryParse(json, out CraftingJsonDocument document, issues))
            {
                LogIssues(path, issues);
                return;
            }

            CraftingJsonParser.ValidateStructure(document, issues);
            LogIssues(path, issues, document);
        }

        private static void LogIssues(string path, CraftingIssues issues, CraftingJsonDocument document = null)
        {
            string header = document != null
                ? $"[Crafting] {Path.GetFileName(path)}: {document.EntryCount} entry(ies). {issues.Summarise()}."
                : $"[Crafting] {Path.GetFileName(path)}: {issues.Summarise()}.";

            var builder = new System.Text.StringBuilder(header);

            for (int i = 0; i < issues.All.Count; i++)
                builder.AppendLine().Append("  ").Append(issues.All[i]);

            string message = builder.ToString();

            if (issues.HasErrors)
                Debug.LogError(message);
            else if (issues.WarningCount > 0)
                Debug.LogWarning(message);
            else
                Debug.Log(message);
        }
    }
}