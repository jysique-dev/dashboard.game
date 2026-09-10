using System.Collections.Generic;
using UnityEditor;

namespace LoopEngine.CraftingEngine.EditorTools
{
    /// <summary>
    /// Rules that apply to every definition, whatever its type: the id must exist, be unique,
    /// and be shaped so it survives being typed into JSON and read back.
    /// </summary>
    /// <remarks>
    /// Concrete inspectors call this first and then add their own rules. The project-wide
    /// validator in session 9 calls exactly the same code, so a warning in the inspector and a
    /// warning in the report can never disagree.
    /// </remarks>
    public static class CraftingIdentityValidator
    {
        /// <summary>Suggested separator between the namespace part and the name part.</summary>
        public const char NamespaceSeparator = '.';

        /// <summary>
        /// Appends identity issues for a definition.
        /// </summary>
        /// <param name="checkDuplicates">
        /// Scans every asset of the same type. Cheap enough for a single inspector, wasteful
        /// inside a loop over the whole project: the batch validator collects ids once instead.
        /// </param>
        public static void Validate(CraftingDefinition definition, CraftingIssues issues, bool checkDuplicates = true)
        {
            if (definition == null || issues == null)
                return;

            string id = definition.RawId;

            if (string.IsNullOrEmpty(id))
            {
                issues.Error("Id is empty. This asset will never be registered and no recipe can reference it.", definition);
                return;
            }

            ValidateFormat(id, issues, definition);

            if (checkDuplicates)
            {
                List<CraftingDefinition> duplicates = CraftingAssetUtility.FindDuplicates(definition);
                for (int i = 0; i < duplicates.Count; i++)
                {
                    issues.Error(
                        $"Id '{id}' is already used by '{duplicates[i].name}'. Only one of them will register; "
                        + "the other is dropped silently. Click to locate it.",
                        duplicates[i]);
                }
            }

            if (string.IsNullOrEmpty(definition.DisplayName) || definition.DisplayName == id)
                issues.Info("No display name set; the raw id is shown in its place.", definition);
        }

        /// <summary>Format rules only. No project scan, so this is safe in a tight loop.</summary>
        public static void ValidateFormat(string id, CraftingIssues issues, UnityEngine.Object context = null)
        {
            if (issues == null || string.IsNullOrEmpty(id))
                return;

            if (id != id.Trim())
            {
                issues.Error("Id has leading or trailing whitespace. Two ids that look identical would not match.", context);
            }

            if (id.IndexOf(' ') >= 0)
            {
                issues.Warning("Id contains spaces. They survive, but they make JSON and console output harder to read.", context);
            }

            if (HasUppercase(id))
            {
                issues.Warning(
                    "Id contains uppercase letters. Id comparison is case sensitive, so 'Item.Coal' and 'item.coal' "
                    + "are two different things. Pick one convention and hold it.",
                    context);
            }

            if (id.IndexOf(NamespaceSeparator) < 0)
            {
                issues.Info(
                    $"Id has no '{NamespaceSeparator}' namespace. Something like 'item.coal' keeps ids readable "
                    + "once there are hundreds of them.",
                    context);
            }
        }

        /// <summary>
        /// Warns when the asset filename has drifted from the id, which makes assets hard to
        /// find in the Project window.
        /// </summary>
        public static void ValidateAssetName(CraftingDefinition definition, CraftingIssues issues)
        {
            if (definition == null || issues == null || string.IsNullOrEmpty(definition.RawId))
                return;

            string expected = SuggestAssetName(definition);
            if (!string.Equals(definition.name, expected, System.StringComparison.Ordinal))
                issues.Info($"Asset file is named '{definition.name}'; '{expected}' would match the id.", definition);
        }

        /// <summary>Filename that matches the id, in the toolkit's convention.</summary>
        public static string SuggestAssetName(CraftingDefinition definition)
        {
            if (definition == null)
                return string.Empty;

            string id = definition.RawId ?? string.Empty;
            int separator = id.LastIndexOf(NamespaceSeparator);
            string tail = separator >= 0 && separator < id.Length - 1 ? id.Substring(separator + 1) : id;

            string prefix = definition.GetType().Name.Replace("Definition", string.Empty);
            return prefix.ToLower() + "." + ToPascalCase(tail).ToLower();
        }

        /// <summary>Renames the asset file to match its id. Returns false when nothing changed.</summary>
        public static bool RenameAssetToMatchId(CraftingDefinition definition)
        {
            if (definition == null)
                return false;

            string path = AssetDatabase.GetAssetPath(definition);
            if (string.IsNullOrEmpty(path))
                return false;

            string desired = SuggestAssetName(definition);
            if (string.IsNullOrEmpty(desired) || desired == definition.name)
                return false;

            string error = AssetDatabase.RenameAsset(path, desired);
            if (!string.IsNullOrEmpty(error))
            {
                UnityEngine.Debug.LogError($"[Crafting] Rename failed: {error}", definition);
                return false;
            }

            AssetDatabase.SaveAssets();
            return true;
        }

        private static bool HasUppercase(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsUpper(value[i]))
                    return true;
            }

            return false;
        }

        private static string ToPascalCase(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var builder = new System.Text.StringBuilder(value.Length);
            bool capitaliseNext = true;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];

               // if (c == '_' || c == '-' || c == ' ')
                if ( c == '-' || c == ' ')
                {
                    capitaliseNext = true;
                    continue;
                }

                builder.Append(capitaliseNext ? char.ToUpperInvariant(c) : c);
                capitaliseNext = false;
            }

            return builder.ToString();
        }
    }
}