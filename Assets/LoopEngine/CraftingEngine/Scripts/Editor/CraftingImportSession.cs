using System;
using System.Collections.Generic;
using UnityEditor;

namespace LoopEngine.CraftingEngine.EditorTools
{
    /// <summary>
    /// Turns a parsed document into assets. Existing assets are updated in place by id;
    /// missing ones are created in their concept folder.
    /// </summary>
    /// <remarks>
    /// Two passes, and the order is not optional. Items and categories are written first
    /// because recipes, machines and modifiers reference them; the index is then rebuilt so the
    /// second pass can resolve references to assets that did not exist a moment earlier.
    ///
    /// One session object serves both the preview and the apply, using the same lookups, so the
    /// plan shown to the user cannot disagree with what actually happens.
    /// </remarks>
    public sealed class CraftingImportSession
    {
        private readonly CraftingJsonDocument _document;

        private readonly Dictionary<string, ItemDefinition> _items = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, MachineCategoryDefinition> _categories = new Dictionary<string, MachineCategoryDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, RecipeDefinition> _recipes = new Dictionary<string, RecipeDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, MachineDefinition> _machines = new Dictionary<string, MachineDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, ModifierDefinition> _modifiers = new Dictionary<string, ModifierDefinition>(StringComparer.Ordinal);

        public CraftingImportSession(CraftingJsonDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            RebuildIndex();
        }

        /// <summary>Assets touched by the last <see cref="Apply"/>, for selecting afterwards.</summary>
        public List<CraftingDefinition> Touched { get; } = new List<CraftingDefinition>();

        // --- Preview --------------------------------------------------------------------

        /// <summary>
        /// Works out what would happen, without changing anything.
        /// References are checked against the project plus whatever this file would create.
        /// </summary>
        public CraftingImportPlan BuildPlan(CraftingIssues issues)
        {
            var plan = new CraftingImportPlan();

            PlanSection(plan, "item", _document.items, e => e.id, id => _items.ContainsKey(id) ? _items[id] : null);
            PlanSection(plan, "category", _document.categories, e => e.id, id => _categories.ContainsKey(id) ? _categories[id] : null);
            PlanSection(plan, "recipe", _document.recipes, e => e.id, id => _recipes.ContainsKey(id) ? _recipes[id] : null);
            PlanSection(plan, "machine", _document.machines, e => e.id, id => _machines.ContainsKey(id) ? _machines[id] : null);
            PlanSection(plan, "modifier", _document.modifiers, e => e.id, id => _modifiers.ContainsKey(id) ? _modifiers[id] : null);

            CheckReferences(issues);
            return plan;
        }

        private static void PlanSection<T>(
            CraftingImportPlan plan,
            string section,
            T[] entries,
            Func<T, string> getId,
            Func<string, CraftingDefinition> findExisting)
        {
            if (entries == null)
                return;

            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < entries.Length; i++)
            {
                string id = getId(entries[i]);

                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                {
                    plan.Add(new ImportEntry(section, id ?? string.Empty, ImportAction.Skip));
                    continue;
                }

                CraftingDefinition existing = findExisting(id);
                plan.Add(existing != null
                    ? new ImportEntry(section, id, ImportAction.Update, existing)
                    : new ImportEntry(section, id, ImportAction.Create));
            }
        }

        /// <summary>
        /// Reports references that resolve to neither an existing asset nor an entry in the file.
        /// These are errors: the importer cannot invent the missing definition.
        /// </summary>
        private void CheckReferences(CraftingIssues issues)
        {
            if (issues == null)
                return;

            HashSet<string> items = CombineIds(_items.Keys, _document.items, e => e.id);
            HashSet<string> categories = CombineIds(_categories.Keys, _document.categories, e => e.id);

            if (_document.recipes != null)
            {
                for (int i = 0; i < _document.recipes.Length; i++)
                {
                    RecipeJson recipe = _document.recipes[i];
                    string label = $"recipe '{recipe.id}'";

                    CheckAmounts(recipe.inputs, label + " input", items, issues);
                    CheckAmounts(recipe.outputs, label + " output", items, issues);

                    if (!string.IsNullOrEmpty(recipe.requiredCategory) && !categories.Contains(recipe.requiredCategory))
                        issues.Error($"{label} requires category '{recipe.requiredCategory}', which does not exist.");
                }
            }

            if (_document.machines != null)
            {
                for (int i = 0; i < _document.machines.Length; i++)
                {
                    MachineJson machine = _document.machines[i];
                    if (machine.categories == null)
                        continue;

                    for (int c = 0; c < machine.categories.Length; c++)
                    {
                        string category = machine.categories[c];
                        if (!string.IsNullOrWhiteSpace(category) && !categories.Contains(category))
                            issues.Error($"machine '{machine.id}' provides category '{category}', which does not exist.");
                    }
                }
            }

            if (_document.modifiers == null)
                return;

            for (int i = 0; i < _document.modifiers.Length; i++)
            {
                ModifierJson modifier = _document.modifiers[i];
                string label = $"modifier '{modifier.id}'";

                if (!string.IsNullOrEmpty(modifier.carrierItem) && !items.Contains(modifier.carrierItem))
                    issues.Error($"{label} is carried by '{modifier.carrierItem}', which does not exist.");

                CheckAmounts(modifier.bonusOutputs, label + " bonus output", items, issues);

                if (modifier.allowedCategories == null)
                    continue;

                for (int c = 0; c < modifier.allowedCategories.Length; c++)
                {
                    string category = modifier.allowedCategories[c];
                    if (!string.IsNullOrWhiteSpace(category) && !categories.Contains(category))
                        issues.Error($"{label} allows category '{category}', which does not exist.");
                }
            }
        }

        private static void CheckAmounts(AmountJson[] amounts, string label, HashSet<string> items, CraftingIssues issues)
        {
            if (amounts == null)
                return;

            for (int i = 0; i < amounts.Length; i++)
            {
                string item = amounts[i].item;
                if (!string.IsNullOrWhiteSpace(item) && !items.Contains(item))
                    issues.Error($"{label} references item '{item}', which does not exist.");
            }
        }

        private static HashSet<string> CombineIds<T>(
            IEnumerable<string> existing,
            T[] incoming,
            Func<T, string> getId)
        {
            var combined = new HashSet<string>(existing, StringComparer.Ordinal);

            if (incoming == null)
                return combined;

            for (int i = 0; i < incoming.Length; i++)
            {
                string id = getId(incoming[i]);
                if (!string.IsNullOrWhiteSpace(id))
                    combined.Add(id);
            }

            return combined;
        }

        // --- Apply ----------------------------------------------------------------------

        /// <summary>
        /// Writes the document to disk. Call <see cref="BuildPlan"/> first and refuse to get
        /// here if it reported errors: this method assumes references resolve.
        /// </summary>
        /// <returns>Number of assets created or updated.</returns>
        public int Apply(CraftingIssues issues)
        {
            Touched.Clear();

            // Folders must exist before the batch opens. AssetDatabase.IsValidFolder cannot see
            // folders created inside StartAssetEditing, so creating them there yields a fresh
            // 'Items 1', 'Items 2' for every asset.
            CraftingEditorPaths.EnsureAllFolders();

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Import Crafting Content");

            try
            {
                // Batches the import into one refresh instead of one per asset.
                AssetDatabase.StartAssetEditing();

                ImportItems(issues);
                ImportCategories(issues);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // Assets created above are only findable after this, and the second pass needs them.
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RebuildIndex();

            try
            {
                AssetDatabase.StartAssetEditing();

                ImportRecipes(issues);
                ImportMachines(issues);
                ImportModifiers(issues);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Undo.CollapseUndoOperations(undoGroup);
            return Touched.Count;
        }

        private void ImportItems(CraftingIssues issues)
        {
            if (_document.items == null)
                return;

            for (int i = 0; i < _document.items.Length; i++)
            {
                ItemJson json = _document.items[i];
                if (string.IsNullOrWhiteSpace(json.id))
                    continue;

                ItemDefinition asset = Resolve(_items, json.id, json.displayName);
                if (asset == null)
                {
                    issues?.Error($"Could not create item '{json.id}'.");
                    continue;
                }

                Undo.RecordObject(asset, "Import Item");
                asset.EditorSetIdentity(json.id, json.displayName);

                // The icon is not part of the JSON format, so an update must keep the one an
                // artist already assigned rather than clearing it.
                asset.EditorSetContent(json.description, asset.Icon, json.maxStack, json.tags);

                MarkTouched(asset);
            }
        }

        private void ImportCategories(CraftingIssues issues)
        {
            if (_document.categories == null)
                return;

            for (int i = 0; i < _document.categories.Length; i++)
            {
                CategoryJson json = _document.categories[i];
                if (string.IsNullOrWhiteSpace(json.id))
                    continue;

                MachineCategoryDefinition asset = Resolve(_categories, json.id, json.displayName);
                if (asset == null)
                {
                    issues?.Error($"Could not create category '{json.id}'.");
                    continue;
                }

                Undo.RecordObject(asset, "Import Category");
                asset.EditorSetIdentity(json.id, json.displayName);
                asset.EditorSetContent(json.description);

                MarkTouched(asset);
            }
        }

        private void ImportRecipes(CraftingIssues issues)
        {
            if (_document.recipes == null)
                return;

            for (int i = 0; i < _document.recipes.Length; i++)
            {
                RecipeJson json = _document.recipes[i];
                if (string.IsNullOrWhiteSpace(json.id))
                    continue;

                RecipeDefinition asset = Resolve(_recipes, json.id, json.displayName);
                if (asset == null)
                {
                    issues?.Error($"Could not create recipe '{json.id}'.");
                    continue;
                }

                MachineCategoryDefinition category = null;
                if (!string.IsNullOrEmpty(json.requiredCategory))
                    _categories.TryGetValue(json.requiredCategory, out category);

                Undo.RecordObject(asset, "Import Recipe");
                asset.EditorSetIdentity(json.id, json.displayName);
                asset.EditorSetContent(
                    ToAmounts(json.inputs, issues, json.id),
                    ToAmounts(json.outputs, issues, json.id),
                    json.craftSeconds,
                    category);

                MarkTouched(asset);
            }
        }

        private void ImportMachines(CraftingIssues issues)
        {
            if (_document.machines == null)
                return;

            for (int i = 0; i < _document.machines.Length; i++)
            {
                MachineJson json = _document.machines[i];
                if (string.IsNullOrWhiteSpace(json.id))
                    continue;

                MachineDefinition asset = Resolve(_machines, json.id, json.displayName);
                if (asset == null)
                {
                    issues?.Error($"Could not create machine '{json.id}'.");
                    continue;
                }

                Undo.RecordObject(asset, "Import Machine");
                asset.EditorSetIdentity(json.id, json.displayName);
                asset.EditorSetContent(
                    ToCategories(json.categories, issues, json.id),
                    json.parallelSlots,
                    json.queueCapacity,
                    json.speedMultiplier,
                    json.modifierSlots,
                    json.allowHandCraftedRecipes,
                    asset.Icon);

                MarkTouched(asset);
            }
        }

        private void ImportModifiers(CraftingIssues issues)
        {
            if (_document.modifiers == null)
                return;

            for (int i = 0; i < _document.modifiers.Length; i++)
            {
                ModifierJson json = _document.modifiers[i];
                if (string.IsNullOrWhiteSpace(json.id))
                    continue;

                ModifierDefinition asset = Resolve(_modifiers, json.id, json.displayName);
                if (asset == null)
                {
                    issues?.Error($"Could not create modifier '{json.id}'.");
                    continue;
                }

                ItemDefinition carrier = null;
                if (!string.IsNullOrEmpty(json.carrierItem))
                    _items.TryGetValue(json.carrierItem, out carrier);

                Undo.RecordObject(asset, "Import Modifier");
                asset.EditorSetIdentity(json.id, json.displayName);
                asset.EditorSetContent(
                    carrier,
                    ToEntries(json.entries, issues, json.id),
                    ToAmounts(json.bonusOutputs, issues, json.id),
                    ToCategories(json.allowedCategories, issues, json.id),
                    json.maxPerMachine);

                MarkTouched(asset);
            }
        }

        // --- Conversion -------------------------------------------------------------------

        private ItemAmount[] ToAmounts(AmountJson[] source, CraftingIssues issues, string owner)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<ItemAmount>();

            var result = new List<ItemAmount>(source.Length);

            for (int i = 0; i < source.Length; i++)
            {
                AmountJson entry = source[i];

                if (!_items.TryGetValue(entry.item ?? string.Empty, out ItemDefinition item))
                {
                    issues?.Error($"'{owner}': item '{entry.item}' was not found; the entry was dropped.");
                    continue;
                }

                result.Add(new ItemAmount(item, entry.amount));
            }

            return result.ToArray();
        }

        private MachineCategoryDefinition[] ToCategories(string[] source, CraftingIssues issues, string owner)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<MachineCategoryDefinition>();

            var result = new List<MachineCategoryDefinition>(source.Length);

            for (int i = 0; i < source.Length; i++)
            {
                string id = source[i];
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (!_categories.TryGetValue(id, out MachineCategoryDefinition category))
                {
                    issues?.Error($"'{owner}': category '{id}' was not found; the entry was dropped.");
                    continue;
                }

                result.Add(category);
            }

            return result.ToArray();
        }

        private static StatModifierEntry[] ToEntries(StatEntryJson[] source, CraftingIssues issues, string owner)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<StatModifierEntry>();

            var result = new List<StatModifierEntry>(source.Length);

            for (int i = 0; i < source.Length; i++)
            {
                StatEntryJson entry = source[i];

                if (!CraftingJsonParser.TryParseStat(entry.stat, out MachineStat stat))
                {
                    issues?.Error($"'{owner}': unknown stat '{entry.stat}'; the entry was dropped.");
                    continue;
                }

                if (!CraftingJsonParser.TryParseOperation(entry.operation, out StatOperation operation))
                {
                    issues?.Error($"'{owner}': unknown operation '{entry.operation}'; the entry was dropped.");
                    continue;
                }

                result.Add(new StatModifierEntry(stat, operation, entry.value));
            }

            return result.ToArray();
        }

        // --- Index ------------------------------------------------------------------------

        private T Resolve<T>(Dictionary<string, T> index, string id, string displayName) where T : CraftingDefinition
        {
            if (index.TryGetValue(id, out T existing) && existing != null)
                return existing;

            T created = CraftingAssetFactory.CreateWithId<T>(id, displayName);
            if (created != null)
                index[id] = created;

            return created;
        }

        private void MarkTouched(CraftingDefinition asset)
        {
            EditorUtility.SetDirty(asset);
            Touched.Add(asset);
        }

        private void RebuildIndex()
        {
            Index(_items, CraftingAssetUtility.FindAll<ItemDefinition>());
            Index(_categories, CraftingAssetUtility.FindAll<MachineCategoryDefinition>());
            Index(_recipes, CraftingAssetUtility.FindAll<RecipeDefinition>());
            Index(_machines, CraftingAssetUtility.FindAll<MachineDefinition>());
            Index(_modifiers, CraftingAssetUtility.FindAll<ModifierDefinition>());
        }

        private static void Index<T>(Dictionary<string, T> index, List<T> assets) where T : CraftingDefinition
        {
            index.Clear();

            for (int i = 0; i < assets.Count; i++)
            {
                string id = assets[i].RawId;
                if (string.IsNullOrEmpty(id))
                    continue;

                // First one wins, matching what the runtime registry does with duplicate ids.
                if (!index.ContainsKey(id))
                    index[id] = assets[i];
            }
        }
    }
}