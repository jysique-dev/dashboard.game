using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoopEngine.CraftingEngine.EditorTools
{
    /// <summary>
    /// Where authored content lives. One folder per concept, under a root the project can move.
    /// </summary>
    /// <remarks>
    /// The root is stored in EditorPrefs, keyed by product name, so it is a per-developer
    /// preference rather than something that lands in version control and fights between
    /// machines. Everything else in the toolkit asks this class instead of hardcoding paths.
    /// </remarks>
    public static class CraftingEditorPaths
    {
        /// <summary>Used until someone changes it.</summary>
        public const string DefaultRoot = "Assets/LoopEngine/CraftingEngine/Content";

        private const string RootPrefKeyPrefix = "LoopEngine.Crafting.ContentRoot.";

        private static string RootPrefKey => RootPrefKeyPrefix + PlayerSettings.productName;

        /// <summary>
        /// Root folder for authored content, as a project-relative path with no trailing slash.
        /// Setting it to something outside Assets is ignored.
        /// </summary>
        public static string Root
        {
            get
            {
                string stored = EditorPrefs.GetString(RootPrefKey, DefaultRoot);
                return IsUnderAssets(stored) ? stored : DefaultRoot;
            }
            set
            {
                if (!IsUnderAssets(value))
                {
                    Debug.LogError($"[Crafting] Content root must be inside the Assets folder. Ignored: '{value}'.");
                    return;
                }

                EditorPrefs.SetString(RootPrefKey, value.TrimEnd('/'));
            }
        }

        /// <summary>Resets the root back to <see cref="DefaultRoot"/>.</summary>
        public static void ResetRoot() => EditorPrefs.DeleteKey(RootPrefKey);

        /// <summary>Sub-folder name for a definition type. One folder per concept.</summary>
        public static string GetFolderName(Type definitionType)
        {
            if (definitionType == typeof(ItemDefinition))
                return "Items";

            if (definitionType == typeof(RecipeDefinition))
                return "Recipes";

            if (definitionType == typeof(MachineDefinition))
                return "Machines";

            if (definitionType == typeof(MachineCategoryDefinition))
                return "Categories";

            if (definitionType == typeof(ModifierDefinition))
                return "Modifiers";

            if (definitionType == typeof(CraftingDatabase))
                return string.Empty; // The database sits at the root, next to the folders.

            // A type the toolkit does not know about still gets a home rather than an exception.
            return definitionType != null ? definitionType.Name : "Misc";
        }

        /// <summary>Full project-relative folder for a definition type.</summary>
        public static string GetFolder(Type definitionType)
        {
            string sub = GetFolderName(definitionType);
            return string.IsNullOrEmpty(sub) ? Root : Root + "/" + sub;
        }

        public static string GetFolder<T>() => GetFolder(typeof(T));

        /// <summary>
        /// Returns the folder for a type, creating it and every missing parent first.
        /// </summary>
        public static string EnsureFolder<T>() => EnsureFolder(GetFolder(typeof(T)));

        /// <summary>
        /// Folders this class has already created or confirmed. Needed because
        /// <see cref="AssetDatabase.IsValidFolder"/> cannot see folders created earlier inside
        /// the same <see cref="AssetDatabase.StartAssetEditing"/> batch: it reports them as
        /// missing, and creating them again produces 'Items 1', 'Items 2' and so on.
        /// </summary>
        private static readonly HashSet<string> VerifiedFolders = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Forgets the cached folder list. Call it after anything that may have deleted or
        /// moved folders behind this class's back.
        /// </summary>
        public static void ClearFolderCache() => VerifiedFolders.Clear();

        /// <summary>
        /// Creates a folder chain if it does not exist. Accepts nested paths;
        /// <see cref="AssetDatabase.CreateFolder"/> only makes one level at a time.
        /// </summary>
        /// <returns>The folder path, or <see cref="DefaultRoot"/> if the input was unusable.</returns>
        public static string EnsureFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !IsUnderAssets(assetPath))
                assetPath = DefaultRoot;

            assetPath = assetPath.TrimEnd('/');

            if (VerifiedFolders.Contains(assetPath) || AssetDatabase.IsValidFolder(assetPath))
            {
                VerifiedFolders.Add(assetPath);
                return assetPath;
            }

            string[] parts = assetPath.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                    continue;

                string next = current + "/" + parts[i];

                if (!VerifiedFolders.Contains(next) && !AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                VerifiedFolders.Add(next);
                current = next;
            }

            return current;
        }

        /// <summary>Creates the root and every concept folder in one go.</summary>
        public static void EnsureAllFolders()
        {
            ClearFolderCache();

            EnsureFolder(Root);
            EnsureFolder<ItemDefinition>();
            EnsureFolder<RecipeDefinition>();
            EnsureFolder<MachineDefinition>();
            EnsureFolder<MachineCategoryDefinition>();
            EnsureFolder<ModifierDefinition>();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Converts an absolute path from a folder panel into a project-relative one.
        /// Returns empty when the folder is outside this project.
        /// </summary>
        public static string ToProjectRelative(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                return string.Empty;

            string dataPath = Application.dataPath.Replace('\\', '/');
            string normalised = absolutePath.Replace('\\', '/').TrimEnd('/');

            if (normalised == dataPath)
                return "Assets";

            if (!normalised.StartsWith(dataPath + "/", StringComparison.Ordinal))
                return string.Empty;

            return "Assets" + normalised.Substring(dataPath.Length);
        }

        private static bool IsUnderAssets(string path)
            => !string.IsNullOrEmpty(path)
               && (path == "Assets" || path.StartsWith("Assets/", StringComparison.Ordinal));
    }
}