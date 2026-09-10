using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoopEngine.CraftingEngine.EditorTools
{
    /// <summary>
    /// Moves every definition asset into its concept folder and reports folders left empty.
    /// </summary>
    /// <remarks>
    /// Written to clean up after duplicated folders, but useful whenever assets have drifted:
    /// nothing in the toolkit requires assets to live in the content root, so this is a tidy-up,
    /// never a requirement.
    ///
    /// It moves assets and deletes nothing. Empty folders are listed for you to remove, because
    /// an automated delete that guesses wrong costs more than the two seconds it saves.
    /// </remarks>
    public static class CraftingFolderTidy
    {
        private const string MenuPath = LoopRoutes.CraftingToolRoute +"/Content Folder/Tidy Assets Into Concept Folders";

        [MenuItem(MenuPath, false, 210)]
        public static void Tidy()
        {
            CraftingEditorPaths.ClearFolderCache();
            CraftingEditorPaths.EnsureAllFolders();

            var moved = new List<string>();
            var failed = new List<string>();

            Move<ItemDefinition>(moved, failed);
            Move<MachineCategoryDefinition>(moved, failed);
            Move<RecipeDefinition>(moved, failed);
            Move<MachineDefinition>(moved, failed);
            Move<ModifierDefinition>(moved, failed);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            List<string> empties = FindEmptyFoldersUnderRoot();

            var builder = new System.Text.StringBuilder();
            builder.Append("[Crafting] Tidy: ").Append(moved.Count).Append(" asset(s) moved");

            if (failed.Count > 0)
                builder.Append(", ").Append(failed.Count).Append(" could not be moved");

            builder.Append('.');

            for (int i = 0; i < moved.Count; i++)
                builder.AppendLine().Append("  moved ").Append(moved[i]);

            for (int i = 0; i < failed.Count; i++)
                builder.AppendLine().Append("  FAILED ").Append(failed[i]);

            if (empties.Count > 0)
            {
                builder.AppendLine().Append("  Empty folders left behind, safe to delete by hand:");
                for (int i = 0; i < empties.Count; i++)
                    builder.AppendLine().Append("    ").Append(empties[i]);
            }

            if (failed.Count > 0)
                Debug.LogWarning(builder.ToString());
            else
                Debug.Log(builder.ToString());
        }

        private static void Move<T>(List<string> moved, List<string> failed) where T : CraftingDefinition
        {
            string folder = CraftingEditorPaths.EnsureFolder<T>();
            List<T> assets = CraftingAssetUtility.FindAll<T>();

            for (int i = 0; i < assets.Count; i++)
            {
                T asset = assets[i];
                string path = AssetDatabase.GetAssetPath(asset);

                if (string.IsNullOrEmpty(path))
                    continue;

                string currentFolder = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
                if (currentFolder == folder)
                    continue;

                string target = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + asset.name + ".asset");
                string error = AssetDatabase.MoveAsset(path, target);

                if (string.IsNullOrEmpty(error))
                    moved.Add(path + "  ->  " + target);
                else
                    failed.Add(path + " : " + error);
            }
        }

        /// <summary>
        /// Folders under the content root holding no assets and no sub-folders.
        /// Reported only; deleting them is left to you.
        /// </summary>
        private static List<string> FindEmptyFoldersUnderRoot()
        {
            var empties = new List<string>();
            string root = CraftingEditorPaths.Root;

            if (!AssetDatabase.IsValidFolder(root))
                return empties;

            string[] subFolders = AssetDatabase.GetSubFolders(root);
            for (int i = 0; i < subFolders.Length; i++)
            {
                if (IsEmpty(subFolders[i]))
                    empties.Add(subFolders[i]);
            }

            return empties;
        }

        private static bool IsEmpty(string folder)
        {
            if (AssetDatabase.GetSubFolders(folder).Length > 0)
                return false;

            // FindAssets with a folder filter returns the folder's own contents, recursively.
            string[] contents = AssetDatabase.FindAssets(string.Empty, new[] { folder });
            return contents == null || contents.Length == 0;
        }
    }
}