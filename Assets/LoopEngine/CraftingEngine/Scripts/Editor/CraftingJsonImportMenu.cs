using System.IO;
using UnityEditor;
using UnityEngine;

namespace LoopEngine.CraftingEngine.EditorTools
{
    /// <summary>
    /// The import entry point: pick a file, see what it would do, confirm, apply.
    /// </summary>
    /// <remarks>
    /// The preview is a console listing plus a confirmation dialog rather than a window,
    /// because the toolkit is built on custom inspectors only. The listing goes out before the
    /// dialog appears, so the full plan is readable while deciding.
    /// </remarks>
    public static class CraftingJsonImportMenu
    {
        private const string MenuRoot = LoopRoutes.CraftingToolRoute + "/JSON/";

        [MenuItem(MenuRoot + "Import File...", false, 310)]
        public static void ImportFile()
        {
            string path = EditorUtility.OpenFilePanel("Import crafting JSON", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path))
                return;

            Import(path);
        }

        /// <summary>Imports a file by path. Exposed so build scripts can call it directly.</summary>
        public static bool Import(string absolutePath)
        {
            string json;
            try
            {
                json = File.ReadAllText(absolutePath);
            }
            catch (IOException exception)
            {
                Debug.LogError($"[Crafting] Could not read '{absolutePath}': {exception.Message}");
                return false;
            }

            var issues = new CraftingIssues();

            if (!CraftingJsonParser.TryParse(json, out CraftingJsonDocument document, issues))
            {
                Report(absolutePath, issues);
                return false;
            }

            CraftingJsonParser.ValidateStructure(document, issues);

            var session = new CraftingImportSession(document);
            CraftingImportPlan plan = session.BuildPlan(issues);

            // The plan goes to the console first: the dialog is a yes or no, and a yes should
            // not be given blind.
            Debug.Log($"[Crafting] Import plan for {Path.GetFileName(absolutePath)}\n{plan.Describe()}");
            Report(absolutePath, issues);

            if (issues.HasErrors)
            {
                EditorUtility.DisplayDialog(
                    "Crafting import",
                    $"{issues.ErrorCount} error(s) found. Nothing was imported.\n\n"
                    + "See the console for the full list.",
                    "Close");
                return false;
            }

            if (plan.ChangeCount == 0)
            {
                EditorUtility.DisplayDialog("Crafting import", "Nothing to import from this file.", "Close");
                return false;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Crafting import",
                $"{Path.GetFileName(absolutePath)}\n\n{plan.Summarise()}\n\n"
                + (issues.WarningCount > 0 ? $"{issues.WarningCount} warning(s) in the console.\n\n" : string.Empty)
                + "Existing assets are updated in place, keeping their GUID and every reference to them.",
                "Import",
                "Cancel");

            if (!confirmed)
                return false;

            var applyIssues = new CraftingIssues();
            int changed = session.Apply(applyIssues);

            if (applyIssues.Count > 0)
                Report(absolutePath, applyIssues);

            Debug.Log($"[Crafting] Imported {changed} asset(s) from {Path.GetFileName(absolutePath)}.");

            if (session.Touched.Count > 0)
            {
                EditorUtility.FocusProjectWindow();
                Selection.objects = session.Touched.ToArray();
            }

            return true;
        }

        private static void Report(string path, CraftingIssues issues)
        {
            if (issues.IsClean)
                return;

            var builder = new System.Text.StringBuilder(
                $"[Crafting] {Path.GetFileName(path)}: {issues.Summarise()}.");

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