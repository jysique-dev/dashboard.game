using LoopEngine.CraftingEngine.EditorTools;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoopEngine.CraftingEngine.EditorTools
{
    /// <summary>
    /// Runs the project validator and prints a report grouped by severity.
    /// </summary>
    public static class CraftingValidationMenu
    {
        private const string MenuRoot = LoopRoutes.CraftingToolRoute +"/Validate/";

        /// <summary>Kept so the follow-up commands can act on the last run.</summary>
        private static CraftingIssues _lastRun;

        [MenuItem(MenuRoot + "Validate Project", false, 400)]
        public static void ValidateProject()
        {
            _lastRun = CraftingProjectValidator.ValidateProject();
            Report(_lastRun);
        }

        [MenuItem(MenuRoot + "Select Assets With Errors", false, 401)]
        public static void SelectErrors()
        {
            if (_lastRun == null)
            {
                Debug.Log("[Crafting] Run 'Validate Project' first.");
                return;
            }

            var objects = new List<Object>();
            var seen = new HashSet<Object>();

            for (int i = 0; i < _lastRun.All.Count; i++)
            {
                CraftingIssue issue = _lastRun.All[i];

                if (issue.Severity != CraftingIssueSeverity.Error || issue.Context == null)
                    continue;

                if (seen.Add(issue.Context))
                    objects.Add(issue.Context);
            }

            if (objects.Count == 0)
            {
                Debug.Log("[Crafting] No assets with errors in the last run.");
                return;
            }

            EditorUtility.FocusProjectWindow();
            Selection.objects = objects.ToArray();
            Debug.Log($"[Crafting] Selected {objects.Count} asset(s) with errors.");
        }

        [MenuItem(MenuRoot + "Select Assets With Errors", true)]
        public static bool SelectErrorsValidate() => _lastRun != null && _lastRun.HasErrors;

        // --- Reporting --------------------------------------------------------------------

        /// <summary>
        /// Three separate console entries, one per severity, so Unity's error and warning
        /// filters work on them. A single combined log would be one line in the console and
        /// unusable at fifty issues.
        /// </summary>
        private static void Report(CraftingIssues issues)
        {
            if (issues.IsClean)
            {
                Debug.Log("[Crafting] Project validation passed with no issues.");
                return;
            }

            LogGroup(issues, CraftingIssueSeverity.Error);
            LogGroup(issues, CraftingIssueSeverity.Warning);
            LogGroup(issues, CraftingIssueSeverity.Info);

            Debug.Log($"[Crafting] Validation finished. {issues.Summarise()}.");
        }

        private static void LogGroup(CraftingIssues issues, CraftingIssueSeverity severity)
        {
            var builder = new System.Text.StringBuilder();
            int count = 0;

            for (int i = 0; i < issues.All.Count; i++)
            {
                CraftingIssue issue = issues.All[i];
                if (issue.Severity != severity)
                    continue;

                count++;
                builder.AppendLine().Append("  ").Append(issue.Message);
            }

            if (count == 0)
                return;

            string header = $"[Crafting] {count} {severity.ToString().ToLowerInvariant()}(s):";
            string message = header + builder;

            switch (severity)
            {
                case CraftingIssueSeverity.Error:
                    Debug.LogError(message);
                    break;

                case CraftingIssueSeverity.Warning:
                    Debug.LogWarning(message);
                    break;

                default:
                    Debug.Log(message);
                    break;
            }
        }
    }
}