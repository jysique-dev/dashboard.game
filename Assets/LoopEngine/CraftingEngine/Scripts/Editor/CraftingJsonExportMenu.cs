using System.IO;
using UnityEditor;
using UnityEngine;

namespace LoopEngine.CraftingEngine.EditorTools
{
    /// <summary>
    /// Export entry points, and the round trip check that proves an export can be read back.
    /// </summary>
    public static class CraftingJsonExportMenu
    {
        private const string MenuRoot = LoopRoutes.CraftingToolRoute + "/JSON/";

        [MenuItem(MenuRoot + "Export All...", false, 320)]
        public static void ExportAll()
        {
            var issues = new CraftingIssues();
            CraftingJsonDocument document = CraftingJsonExporter.ExportAll(issues);
            WriteWithDialog(document, issues, "crafting_content");
        }

        [MenuItem(MenuRoot + "Export Selection...", false, 321)]
        public static void ExportSelection()
        {
            var issues = new CraftingIssues();
            CraftingJsonDocument document = CraftingJsonExporter.ExportSelection(issues);
            WriteWithDialog(document, issues, "crafting_selection");
        }

        /// <summary>Enabled only while at least one definition asset is selected.</summary>
        [MenuItem(MenuRoot + "Export Selection...", true)]
        public static bool ExportSelectionValidate()
            => Selection.GetFiltered(typeof(CraftingDefinition), SelectionMode.Assets).Length > 0;

        [MenuItem(MenuRoot + "Export Database...", false, 322)]
        public static void ExportDatabase()
        {
            var database = Selection.activeObject as CraftingDatabase;
            if (database == null)
                return;

            var issues = new CraftingIssues();
            CraftingJsonDocument document = CraftingJsonExporter.ExportDatabase(database, issues);
            WriteWithDialog(document, issues, database.name);
        }

        [MenuItem(MenuRoot + "Export Database...", true)]
        public static bool ExportDatabaseValidate() => Selection.activeObject is CraftingDatabase;

        /// <summary>
        /// Exports everything in memory, reads it back and compares, without writing a file or
        /// touching an asset. Run it after changing the DTOs or the parser.
        /// </summary>
        [MenuItem(MenuRoot + "Verify Round Trip", false, 340)]
        public static void VerifyRoundTrip()
        {
            var issues = new CraftingIssues();
            CraftingJsonDocument document = CraftingJsonExporter.ExportAll(issues);

            if (document.IsEmpty)
            {
                Debug.Log("[Crafting] Round trip: nothing to check, no definitions found.");
                return;
            }

            bool clean = CraftingRoundTrip.Verify(document, issues);
            Log($"Round trip over {document.EntryCount} entry(ies)", issues);

            if (clean)
                return;

            Debug.LogError(
                "[Crafting] Export is losing data. Importing this file back would not reproduce the "
                + "current content. Fix the DTOs or the parser before shipping content through JSON.");
        }

        // --- Writing ----------------------------------------------------------------------

        private static void WriteWithDialog(CraftingJsonDocument document, CraftingIssues issues, string defaultName)
        {
            if (document.IsEmpty)
            {
                EditorUtility.DisplayDialog("Crafting export", "Nothing to export.", "Close");
                return;
            }

            // Verifying before writing means a file on disk is always one that can be read back.
            var roundTripIssues = new CraftingIssues();
            bool clean = CraftingRoundTrip.Verify(document, roundTripIssues);

            if (!clean)
            {
                Log("Round trip failed, nothing written", roundTripIssues);
                EditorUtility.DisplayDialog(
                    "Crafting export",
                    "This content does not survive a round trip, so the file was not written.\n\n"
                    + "See the console for exactly which field changed.",
                    "Close");
                return;
            }

            string path = EditorUtility.SaveFilePanel(
                "Export crafting JSON",
                Application.dataPath,
                defaultName,
                "json");

            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                File.WriteAllText(path, CraftingJsonParser.ToJson(document));
            }
            catch (IOException exception)
            {
                Debug.LogError($"[Crafting] Could not write '{path}': {exception.Message}");
                return;
            }

            AssetDatabase.Refresh();
            Log($"Exported {document.EntryCount} entry(ies) to {Path.GetFileName(path)}", issues);
        }

        private static void Log(string header, CraftingIssues issues)
        {
            var builder = new System.Text.StringBuilder("[Crafting] " + header + ". " + issues.Summarise() + ".");

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