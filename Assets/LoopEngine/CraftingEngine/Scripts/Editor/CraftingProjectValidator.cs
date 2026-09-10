using System;
using System.Collections.Generic;

namespace LoopEngine.CraftingEngine.EditorTools
{
    /// <summary>
    /// One pass over every definition in the project, reporting everything that would go wrong
    /// at registration time or later.
    /// </summary>
    /// <remarks>
    /// Shares its identity rules with the inspectors through
    /// <see cref="CraftingIdentityValidator"/>, so a warning here and a warning in the inspector
    /// cannot disagree. Duplicate ids are found by collecting every id once rather than by
    /// scanning per asset, which is what the inspector does: the same result, but linear instead
    /// of quadratic.
    /// </remarks>
    public static class CraftingProjectValidator
    {
        /// <summary>Cycles reported before the check gives up, to keep the console usable.</summary>
        private const int MaxReportedCycles = 12;

        /// <summary>Runs every check and returns everything found.</summary>
        public static CraftingIssues ValidateProject()
        {
            var issues = new CraftingIssues();

            List<ItemDefinition> items = CraftingAssetUtility.FindAll<ItemDefinition>();
            List<MachineCategoryDefinition> categories = CraftingAssetUtility.FindAll<MachineCategoryDefinition>();
            List<RecipeDefinition> recipes = CraftingAssetUtility.FindAll<RecipeDefinition>();
            List<MachineDefinition> machines = CraftingAssetUtility.FindAll<MachineDefinition>();
            List<ModifierDefinition> modifiers = CraftingAssetUtility.FindAll<ModifierDefinition>();

            CheckIds(items, "item", issues);
            CheckIds(categories, "category", issues);
            CheckIds(recipes, "recipe", issues);
            CheckIds(machines, "machine", issues);
            CheckIds(modifiers, "modifier", issues);

            CheckRecipeReferences(recipes, issues);
            CheckMachineReferences(machines, issues);
            CheckModifierReferences(modifiers, machines, issues);
            CheckReachability(recipes, machines, issues);
            CheckOrphanItems(items, recipes, modifiers, issues);
            CheckDatabases(items, categories, recipes, machines, modifiers, issues);
            CheckCycles(recipes, issues);

            return issues;
        }

        // --- Identity ---------------------------------------------------------------------

        private static void CheckIds<T>(List<T> assets, string section, CraftingIssues issues)
            where T : CraftingDefinition
        {
            var byId = new Dictionary<string, T>(assets.Count, StringComparer.Ordinal);

            for (int i = 0; i < assets.Count; i++)
            {
                T asset = assets[i];
                string id = asset.RawId;

                if (string.IsNullOrEmpty(id))
                {
                    issues.Error($"{section} '{asset.name}' has no id and will never be registered.", asset);
                    continue;
                }

                // Format only: the duplicate scan is handled here in one pass.
                CraftingIdentityValidator.ValidateFormat(id, issues, asset);

                if (byId.TryGetValue(id, out T first))
                {
                    issues.Error(
                        $"{section} id '{id}' is used by '{first.name}' and '{asset.name}'. "
                        + "Only the first registers; the other is dropped silently.",
                        asset);
                    continue;
                }

                byId.Add(id, asset);
            }
        }

        // --- References -------------------------------------------------------------------

        private static void CheckRecipeReferences(List<RecipeDefinition> recipes, CraftingIssues issues)
        {
            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeDefinition recipe = recipes[i];

                CheckAmounts(recipe.AuthoredInputs, recipe, "input", issues);
                CheckAmounts(recipe.AuthoredOutputs, recipe, "output", issues);

                if (recipe.AuthoredOutputs.Length == 0)
                    issues.Error($"recipe '{recipe.RawId}' has no outputs and will not be registered.", recipe);
            }
        }

        private static void CheckAmounts(ItemAmount[] amounts, RecipeDefinition owner, string side, CraftingIssues issues)
        {
            for (int i = 0; i < amounts.Length; i++)
            {
                ItemAmount amount = amounts[i];

                if (amount.Item == null)
                {
                    issues.Error($"recipe '{owner.RawId}' {side} {i + 1} has no item assigned.", owner);
                    continue;
                }

                if (!amount.Item.Id.IsValid)
                    issues.Error($"recipe '{owner.RawId}' {side} {i + 1} points at an item with no id.", amount.Item);
            }
        }

        private static void CheckMachineReferences(List<MachineDefinition> machines, CraftingIssues issues)
        {
            for (int i = 0; i < machines.Count; i++)
            {
                MachineDefinition machine = machines[i];
                MachineCategoryDefinition[] categories = machine.AuthoredCategories;

                for (int c = 0; c < categories.Length; c++)
                {
                    if (categories[c] == null)
                        issues.Warning($"machine '{machine.RawId}' has an empty category slot.", machine);
                }

                if (!machine.IsValid)
                {
                    issues.Error(
                        $"machine '{machine.RawId}' provides no category and does not allow hand crafted "
                        + "recipes, so it can never run anything.",
                        machine);
                }
            }
        }

        private static void CheckModifierReferences(
            List<ModifierDefinition> modifiers,
            List<MachineDefinition> machines,
            CraftingIssues issues)
        {
            for (int i = 0; i < modifiers.Count; i++)
            {
                ModifierDefinition modifier = modifiers[i];

                if (!modifier.IsValid)
                {
                    issues.Error(
                        $"modifier '{modifier.RawId}' changes no stat and adds no valid output, "
                        + "so it will not be registered.",
                        modifier);
                    continue;
                }

                bool accepted = false;
                for (int m = 0; m < machines.Count && !accepted; m++)
                    accepted = machines[m].ModifierSlots > 0 && modifier.AppliesTo(machines[m]);

                if (!accepted)
                {
                    issues.Warning(
                        $"modifier '{modifier.RawId}' cannot be installed anywhere: no machine both "
                        + "accepts it and has upgrade slots.",
                        modifier);
                }
            }
        }

        private static void CheckReachability(
            List<RecipeDefinition> recipes,
            List<MachineDefinition> machines,
            CraftingIssues issues)
        {
            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeDefinition recipe = recipes[i];
                if (recipe.IsHandCrafted)
                    continue;

                bool reachable = false;
                for (int m = 0; m < machines.Count && !reachable; m++)
                    reachable = machines[m].CanRun(recipe);

                if (!reachable)
                {
                    string category = recipe.RequiredCategory != null
                        ? recipe.RequiredCategory.RawId
                        : "<missing>";

                    issues.Warning(
                        $"recipe '{recipe.RawId}' requires category '{category}', which no machine provides. "
                        + "It is dead content.",
                        recipe);
                }
            }
        }

        private static void CheckOrphanItems(
            List<ItemDefinition> items,
            List<RecipeDefinition> recipes,
            List<ModifierDefinition> modifiers,
            CraftingIssues issues)
        {
            var referenced = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < recipes.Count; i++)
            {
                Collect(recipes[i].AuthoredInputs, referenced);
                Collect(recipes[i].AuthoredOutputs, referenced);
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i].CarrierItem != null)
                    referenced.Add(modifiers[i].CarrierItem.RawId);

                ItemStack[] bonus = modifiers[i].BonusOutputs;
                for (int b = 0; b < bonus.Length; b++)
                {
                    if (!bonus[b].IsEmpty)
                        referenced.Add(bonus[b].Item.Value);
                }
            }

            for (int i = 0; i < items.Count; i++)
            {
                ItemDefinition item = items[i];
                if (string.IsNullOrEmpty(item.RawId) || referenced.Contains(item.RawId))
                    continue;

                issues.Info(
                    $"item '{item.RawId}' is not referenced by any recipe or modifier. "
                    + "Expected for a raw resource placed in the world.",
                    item);
            }
        }

        private static void Collect(ItemAmount[] amounts, HashSet<string> into)
        {
            for (int i = 0; i < amounts.Length; i++)
            {
                if (amounts[i].Item != null && !string.IsNullOrEmpty(amounts[i].Item.RawId))
                    into.Add(amounts[i].Item.RawId);
            }
        }

        // --- Databases --------------------------------------------------------------------

        private static void CheckDatabases(
            List<ItemDefinition> items,
            List<MachineCategoryDefinition> categories,
            List<RecipeDefinition> recipes,
            List<MachineDefinition> machines,
            List<ModifierDefinition> modifiers,
            CraftingIssues issues)
        {
            List<CraftingDatabase> databases = CraftingAssetUtility.FindAll<CraftingDatabase>();

            if (databases.Count == 0)
            {
                issues.Info("No CraftingDatabase asset exists. Nothing can be loaded at runtime without one.");
                return;
            }

            for (int i = 0; i < databases.Count; i++)
            {
                CraftingDatabase database = databases[i];

                CheckDatabaseSection(database, database.Items, items, "item", issues);
                CheckDatabaseSection(database, database.Categories, categories, "category", issues);
                CheckDatabaseSection(database, database.Recipes, recipes, "recipe", issues);
                CheckDatabaseSection(database, database.Machines, machines, "machine", issues);
                CheckDatabaseSection(database, database.Modifiers, modifiers, "modifier", issues);
            }
        }

        private static void CheckDatabaseSection<T>(
            CraftingDatabase database,
            T[] listed,
            List<T> inProject,
            string section,
            CraftingIssues issues) where T : CraftingDefinition
        {
            var present = new HashSet<T>();

            for (int i = 0; i < listed.Length; i++)
            {
                if (listed[i] == null)
                {
                    issues.Warning($"database '{database.name}' has an empty {section} slot.", database);
                    continue;
                }

                if (!present.Add(listed[i]))
                    issues.Warning($"database '{database.name}' lists {section} '{listed[i].RawId}' twice.", database);
            }

            int missing = 0;
            for (int i = 0; i < inProject.Count; i++)
            {
                if (!present.Contains(inProject[i]))
                    missing++;
            }

            if (missing > 0)
            {
                issues.Info(
                    $"database '{database.name}' does not list {missing} {section}(s) that exist in the project. "
                    + "Intentional when the database is a subset.",
                    database);
            }
        }

        // --- Cycles -----------------------------------------------------------------------

        /// <summary>
        /// Finds loops in the item graph: item A is consumed by a recipe producing item B,
        /// and following that chain leads back to A.
        /// </summary>
        /// <remarks>
        /// A loop is not a bug by itself; recycling and reprocessing are loops. What matters is
        /// the ratio around it. The product of output over input along the cycle is computed as
        /// a heuristic: at or above 1 it is a possible duplication loop and gets a warning,
        /// below 1 it is lossy and gets an info.
        ///
        /// This ignores the other inputs and outputs of each recipe, so a loop that only closes
        /// because a second ingredient is being burned will still be reported. It flags things
        /// worth a look, it does not prove an exploit.
        /// </remarks>
        private static void CheckCycles(List<RecipeDefinition> recipes, CraftingIssues issues)
        {
            var edges = new Dictionary<string, List<Edge>>(StringComparer.Ordinal);

            for (int r = 0; r < recipes.Count; r++)
            {
                RecipeDefinition recipe = recipes[r];
                ItemAmount[] inputs = recipe.AuthoredInputs;
                ItemAmount[] outputs = recipe.AuthoredOutputs;

                for (int i = 0; i < inputs.Length; i++)
                {
                    if (inputs[i].Item == null || inputs[i].Amount < 1)
                        continue;

                    string from = inputs[i].Item.RawId;

                    for (int o = 0; o < outputs.Length; o++)
                    {
                        if (outputs[o].Item == null || outputs[o].Amount < 1)
                            continue;

                        if (!edges.TryGetValue(from, out List<Edge> list))
                        {
                            list = new List<Edge>(2);
                            edges.Add(from, list);
                        }

                        list.Add(new Edge(
                            outputs[o].Item.RawId,
                            (float)outputs[o].Amount / inputs[i].Amount,
                            recipe));
                    }
                }
            }

            var state = new Dictionary<string, int>(StringComparer.Ordinal); // 0 unvisited, 1 on stack, 2 done
            var path = new List<string>();
            var pathEdges = new List<Edge>();
            int reported = 0;

            foreach (string node in edges.Keys)
            {
                if (state.TryGetValue(node, out int visited) && visited != 0)
                    continue;

                Walk(node, edges, state, path, pathEdges, issues, ref reported);

                if (reported >= MaxReportedCycles)
                {
                    issues.Info($"Cycle detection stopped after {MaxReportedCycles} reports.");
                    return;
                }
            }
        }

        private static void Walk(
            string node,
            Dictionary<string, List<Edge>> edges,
            Dictionary<string, int> state,
            List<string> path,
            List<Edge> pathEdges,
            CraftingIssues issues,
            ref int reported)
        {
            if (reported >= MaxReportedCycles)
                return;

            state[node] = 1;
            path.Add(node);

            if (edges.TryGetValue(node, out List<Edge> outgoing))
            {
                for (int i = 0; i < outgoing.Count; i++)
                {
                    Edge edge = outgoing[i];
                    state.TryGetValue(edge.To, out int nextState);

                    if (nextState == 1)
                    {
                        pathEdges.Add(edge);
                        ReportCycle(path, pathEdges, edge.To, issues);
                        pathEdges.RemoveAt(pathEdges.Count - 1);
                        reported++;

                        if (reported >= MaxReportedCycles)
                            break;

                        continue;
                    }

                    if (nextState == 2)
                        continue;

                    pathEdges.Add(edge);
                    Walk(edge.To, edges, state, path, pathEdges, issues, ref reported);
                    pathEdges.RemoveAt(pathEdges.Count - 1);
                }
            }

            path.RemoveAt(path.Count - 1);
            state[node] = 2;
        }

        private static void ReportCycle(List<string> path, List<Edge> pathEdges, string closesAt, CraftingIssues issues)
        {
            int start = path.IndexOf(closesAt);
            if (start < 0)
                return;

            var builder = new System.Text.StringBuilder();
            float ratio = 1f;

            for (int i = start; i < path.Count; i++)
            {
                builder.Append(path[i]).Append(" -> ");

                if (i < pathEdges.Count)
                    ratio *= pathEdges[i].Ratio;
            }

            builder.Append(closesAt);

            // The closing edge is the last one pushed.
            ratio *= pathEdges[pathEdges.Count - 1].Ratio;

            RecipeDefinition context = pathEdges[pathEdges.Count - 1].Recipe;
            string chain = builder.ToString();

            if (ratio >= 1f)
            {
                issues.Warning(
                    $"Crafting loop with a ratio of {ratio:0.###}: {chain}. "
                    + "At or above 1 this can duplicate value. Other ingredients are ignored by this check.",
                    context);
            }
            else
            {
                issues.Info(
                    $"Crafting loop with a ratio of {ratio:0.###}: {chain}. Lossy, so it cannot duplicate value.",
                    context);
            }
        }

        private readonly struct Edge
        {
            public readonly string To;
            public readonly float Ratio;
            public readonly RecipeDefinition Recipe;

            public Edge(string to, float ratio, RecipeDefinition recipe)
            {
                To = to;
                Ratio = ratio;
                Recipe = recipe;
            }
        }
    }
}