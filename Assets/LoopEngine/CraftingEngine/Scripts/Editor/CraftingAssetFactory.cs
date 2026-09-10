using UnityEditor;
using UnityEngine;

namespace LoopEngine.CraftingEngine.EditorTools
{
    /// <summary>
    /// Creates definition assets in the right folder, with a unique id already filled in and
    /// the result selected and pinged in the Project window.
    /// </summary>
    /// <remarks>
    /// An asset created by hand through Create > … starts with an empty id, and an empty id
    /// never registers. Going through this factory means a new asset is valid from the moment
    /// it exists.
    /// </remarks>
    public static class CraftingAssetFactory
    {
        /// <summary>
        /// Creates and saves a definition asset.
        /// </summary>
        /// <param name="idPrefix">Namespace part of the generated id, such as "item".</param>
        /// <param name="fileNamePrefix">Leading part of the asset filename, such as "Item".</param>
        /// <param name="displayName">Initial display name.</param>
        public static T Create<T>(string idPrefix, string fileNamePrefix, string displayName)
            where T : CraftingDefinition
        {
            string folder = CraftingEditorPaths.EnsureFolder<T>();

            var asset = ScriptableObject.CreateInstance<T>();
            if (asset == null)
            {
                Debug.LogError($"[Crafting] Could not instantiate {typeof(T).Name}.");
                return null;
            }

            string id = CraftingAssetUtility.MakeUniqueId<T>(idPrefix + ".new");
            asset.EditorSetIdentity(id, displayName);
            asset.name = fileNamePrefix + "_New";

            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + asset.name + ".asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            // Registered after creation so Ctrl+Z removes the asset rather than leaving an orphan.
            Undo.RegisterCreatedObjectUndo(asset, "Create " + typeof(T).Name);

            Select(asset);
            Debug.Log($"[Crafting] Created {typeof(T).Name} '{id}' at {path}.", asset);
            return asset;
        }

        /// <summary>
        /// Creates a definition with an exact id, without uniquifying it.
        /// Used by the JSON importer, where the id comes from the file and must be preserved:
        /// silently renaming it to 'item.coal_2' would make the import a no-op on the next run.
        /// The caller is responsible for having checked that the id is free.
        /// </summary>
        public static T CreateWithId<T>(string id, string displayName) where T : CraftingDefinition
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            string folder = CraftingEditorPaths.EnsureFolder<T>();

            var asset = ScriptableObject.CreateInstance<T>();
            if (asset == null)
                return null;

            asset.EditorSetIdentity(id, displayName);
            asset.name = CraftingIdentityValidator.SuggestAssetName(asset);

            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + asset.name + ".asset");
            AssetDatabase.CreateAsset(asset, path);
            Undo.RegisterCreatedObjectUndo(asset, "Import " + typeof(T).Name);
            return asset;
        }

        /// <summary>
        /// Creates a copy of an existing definition with a fresh unique id, next to the original.
        /// </summary>
        public static T Duplicate<T>(T source) where T : CraftingDefinition
        {
            if (source == null)
                return null;

            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(sourcePath))
            {
                Debug.LogError("[Crafting] Can only duplicate an asset that exists on disk.", source);
                return null;
            }

            string path = AssetDatabase.GenerateUniqueAssetPath(sourcePath);
            if (!AssetDatabase.CopyAsset(sourcePath, path))
            {
                Debug.LogError($"[Crafting] Failed to copy '{sourcePath}'.", source);
                return null;
            }

            AssetDatabase.ImportAsset(path);
            var copy = AssetDatabase.LoadAssetAtPath<T>(path);
            if (copy == null)
                return null;

            // The copy carries the original's id, which would be a silent duplicate.
            string id = CraftingAssetUtility.MakeUniqueId<T>(source.RawId);
            copy.EditorSetIdentity(id, source.DisplayName + " Copy");

            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssets();

            Undo.RegisterCreatedObjectUndo(copy, "Duplicate " + typeof(T).Name);
            Select(copy);
            Debug.Log($"[Crafting] Duplicated as '{id}' at {path}.", copy);
            return copy;
        }

        /// <summary>Creates the database asset at the content root.</summary>
        public static CraftingDatabase CreateDatabase()
        {
            string folder = CraftingEditorPaths.EnsureFolder(CraftingEditorPaths.Root);

            var asset = ScriptableObject.CreateInstance<CraftingDatabase>();
            asset.name = "CraftingDatabase";

            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + asset.name + ".asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            Undo.RegisterCreatedObjectUndo(asset, "Create Crafting Database");
            Select(asset);
            Debug.Log($"[Crafting] Created database at {path}.", asset);
            return asset;
        }

        private static void Select(Object asset)
        {
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}