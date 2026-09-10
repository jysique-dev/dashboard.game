using UnityEditor;
using UnityEngine;

namespace LoopEngine.CraftingEngine.EditorTools
{
    /// <summary>
    /// The toolkit's entry points under <c>Tools/LoopEngine/Crafting</c>.
    /// With custom inspectors as the only UI, this menu plays the part a window's toolbar
    /// would: create, locate and set up. Browsing happens in the Project window.
    /// </summary>
    public static class CraftingMenu
    {
        private const string Root = LoopRoutes.CraftingToolRoute + "/";
        private const string CreateRoot = Root + "Create/";
        private const string FolderRoot = Root + "Content Folder/";

        // Priorities group the entries and draw separators between blocks of 11 or more apart.
        private const int CreatePriority = 100;
        private const int FolderPriority = 200;

        // --- Create ------------------------------------------------------------------

        [MenuItem(CreateRoot + "Item", false, CreatePriority)]
        public static void CreateItem()
            => CraftingAssetFactory.Create<ItemDefinition>("item", "Item", "New Item");

        [MenuItem(CreateRoot + "Recipe", false, CreatePriority + 1)]
        public static void CreateRecipe()
            => CraftingAssetFactory.Create<RecipeDefinition>("recipe", "Recipe", "New Recipe");

        [MenuItem(CreateRoot + "Machine Category", false, CreatePriority + 2)]
        public static void CreateCategory()
            => CraftingAssetFactory.Create<MachineCategoryDefinition>("category", "Category", "New Category");

        [MenuItem(CreateRoot + "Machine", false, CreatePriority + 3)]
        public static void CreateMachine()
            => CraftingAssetFactory.Create<MachineDefinition>("machine", "Machine", "New Machine");

        [MenuItem(CreateRoot + "Modifier", false, CreatePriority + 4)]
        public static void CreateModifier()
            => CraftingAssetFactory.Create<ModifierDefinition>("modifier", "Modifier", "New Modifier");

        [MenuItem(CreateRoot + "Database", false, CreatePriority + 20)]
        public static void CreateDatabase() => CraftingAssetFactory.CreateDatabase();

        [MenuItem(Root + "Duplicate Selected", false, CreatePriority + 40)]
        public static void DuplicateSelected()
        {
            var selected = Selection.activeObject as CraftingDefinition;
            if (selected == null)
                return;

            switch (selected)
            {
                case ItemDefinition item: CraftingAssetFactory.Duplicate(item); break;
                case RecipeDefinition recipe: CraftingAssetFactory.Duplicate(recipe); break;
                case MachineDefinition machine: CraftingAssetFactory.Duplicate(machine); break;
                case MachineCategoryDefinition category: CraftingAssetFactory.Duplicate(category); break;
                case ModifierDefinition modifier: CraftingAssetFactory.Duplicate(modifier); break;
                default:
                    Debug.LogWarning($"[Crafting] No duplicate rule for {selected.GetType().Name}.", selected);
                    break;
            }
        }

        /// <summary>Greys the entry out unless a definition asset is selected.</summary>
        [MenuItem(Root + "Duplicate Selected", true)]
        public static bool DuplicateSelectedValidate() => Selection.activeObject is CraftingDefinition;

        // --- Content folder -----------------------------------------------------------

        [MenuItem(FolderRoot + "Create Missing Folders", false, FolderPriority)]
        public static void CreateFolders()
        {
            CraftingEditorPaths.EnsureAllFolders();
            Debug.Log($"[Crafting] Content folders ready under {CraftingEditorPaths.Root}.");
        }

        [MenuItem(FolderRoot + "Set Content Folder...", false, FolderPriority + 1)]
        public static void SetContentFolder()
        {
            string absolute = EditorUtility.OpenFolderPanel(
                "Crafting content folder",
                CraftingEditorPaths.Root,
                string.Empty);

            if (string.IsNullOrEmpty(absolute))
                return;

            string relative = CraftingEditorPaths.ToProjectRelative(absolute);
            if (string.IsNullOrEmpty(relative))
            {
                Debug.LogError("[Crafting] Pick a folder inside this project's Assets folder.");
                return;
            }

            CraftingEditorPaths.Root = relative;
            CraftingEditorPaths.EnsureAllFolders();
            Debug.Log($"[Crafting] Content root set to {relative}.");
        }

        [MenuItem(FolderRoot + "Reset To Default", false, FolderPriority + 2)]
        public static void ResetContentFolder()
        {
            CraftingEditorPaths.ResetRoot();
            Debug.Log($"[Crafting] Content root reset to {CraftingEditorPaths.DefaultRoot}.");
        }

        [MenuItem(FolderRoot + "Show In Project", false, FolderPriority + 3)]
        public static void ShowContentFolder()
        {
            string folder = CraftingEditorPaths.EnsureFolder(CraftingEditorPaths.Root);
            var asset = AssetDatabase.LoadAssetAtPath<Object>(folder);

            if (asset == null)
            {
                Debug.LogWarning($"[Crafting] Folder '{folder}' could not be loaded.");
                return;
            }

            EditorUtility.FocusProjectWindow();
            EditorGUIUtility.PingObject(asset);
        }

        // --- Counts -------------------------------------------------------------------

        [MenuItem(Root + "Log Content Summary", false, FolderPriority + 40)]
        public static void LogSummary()
        {
            Debug.Log(
                "[Crafting] Content summary: "
                + CraftingAssetUtility.FindAll<ItemDefinition>().Count + " item(s), "
                + CraftingAssetUtility.FindAll<RecipeDefinition>().Count + " recipe(s), "
                + CraftingAssetUtility.FindAll<MachineCategoryDefinition>().Count + " category(ies), "
                + CraftingAssetUtility.FindAll<MachineDefinition>().Count + " machine(s), "
                + CraftingAssetUtility.FindAll<ModifierDefinition>().Count + " modifier(s).");
        }
    }
}