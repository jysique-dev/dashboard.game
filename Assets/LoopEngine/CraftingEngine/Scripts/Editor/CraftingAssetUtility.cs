using System;
using System.Collections.Generic;
using UnityEditor;

namespace LoopEngine.CraftingEngine.EditorTools
{
    /// <summary>
    /// Finding and reading authored assets. Every other tool in the kit goes through here,
    /// so there is exactly one place that knows how content is discovered.
    /// </summary>
    /// <remarks>
    /// The non-generic overloads exist because a custom inspector only knows the type at
    /// runtime: <c>Editor.target</c> is typed as Object, not as the concrete definition.
    /// </remarks>
    public static class CraftingAssetUtility
    {
        /// <summary>
        /// Every asset of a definition type in the project, sorted by id so listings are stable.
        /// Searches the whole project, not just the content root, because assets get moved.
        /// </summary>
        public static List<CraftingDefinition> FindAll(Type definitionType, List<CraftingDefinition> results = null)
        {
            results ??= new List<CraftingDefinition>();
            results.Clear();

            if (definitionType == null)
                return results;

            string[] guids = AssetDatabase.FindAssets("t:" + definitionType.Name);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath(path, definitionType) as CraftingDefinition;
                if (asset != null)
                    results.Add(asset);
            }

            results.Sort(CompareById);
            return results;
        }

        /// <summary>Typed version of <see cref="FindAll(Type, List{CraftingDefinition})"/>.</summary>
        public static List<T> FindAll<T>(List<T> results = null) where T : UnityEngine.Object
        {
            results ??= new List<T>();
            results.Clear();

            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                    results.Add(asset);
            }

            results.Sort(CompareByIdThenName);
            return results;
        }

        /// <summary>All ids currently in use by a definition type.</summary>
        public static HashSet<string> CollectIds(Type definitionType)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            List<CraftingDefinition> all = FindAll(definitionType);

            for (int i = 0; i < all.Count; i++)
            {
                string id = all[i].RawId;
                if (!string.IsNullOrEmpty(id))
                    ids.Add(id);
            }

            return ids;
        }

        public static HashSet<string> CollectIds<T>() where T : CraftingDefinition
            => CollectIds(typeof(T));

        /// <summary>
        /// Assets sharing an id with <paramref name="definition"/>. Empty when the id is unique.
        /// Duplicate ids are silently dropped at registration, so surfacing them early matters.
        /// </summary>
        public static List<CraftingDefinition> FindDuplicates(
            CraftingDefinition definition,
            List<CraftingDefinition> results = null)
        {
            results ??= new List<CraftingDefinition>();
            results.Clear();

            if (definition == null || string.IsNullOrEmpty(definition.RawId))
                return results;

            List<CraftingDefinition> all = FindAll(definition.GetType());
            for (int i = 0; i < all.Count; i++)
            {
                CraftingDefinition other = all[i];
                if (other == definition)
                    continue;

                if (string.Equals(other.RawId, definition.RawId, StringComparison.Ordinal))
                    results.Add(other);
            }

            return results;
        }

        /// <summary>
        /// Builds an id that no asset of this type is using, appending a numeric suffix.
        /// </summary>
        public static string MakeUniqueId(Type definitionType, string baseId)
        {
            if (string.IsNullOrWhiteSpace(baseId))
                baseId = "new";

            baseId = baseId.Trim();

            HashSet<string> taken = CollectIds(definitionType);
            if (!taken.Contains(baseId))
                return baseId;

            for (int suffix = 2; suffix < 10000; suffix++)
            {
                string candidate = baseId + "_" + suffix;
                if (!taken.Contains(candidate))
                    return candidate;
            }

            // Practically unreachable; better than returning something already in use.
            return baseId + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
        }

        public static string MakeUniqueId<T>(string baseId) where T : CraftingDefinition
            => MakeUniqueId(typeof(T), baseId);

        private static int CompareById(CraftingDefinition a, CraftingDefinition b)
        {
            int byId = string.CompareOrdinal(
                a != null ? a.RawId : string.Empty,
                b != null ? b.RawId : string.Empty);

            if (byId != 0)
                return byId;

            return string.CompareOrdinal(
                a != null ? a.name : string.Empty,
                b != null ? b.name : string.Empty);
        }

        private static int CompareByIdThenName<T>(T a, T b) where T : UnityEngine.Object
        {
            var defA = a as CraftingDefinition;
            var defB = b as CraftingDefinition;

            if (defA != null && defB != null)
                return CompareById(defA, defB);

            return string.CompareOrdinal(
                a != null ? a.name : string.Empty,
                b != null ? b.name : string.Empty);
        }
    }
}