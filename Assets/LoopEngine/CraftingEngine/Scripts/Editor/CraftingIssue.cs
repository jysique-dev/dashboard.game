using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.CraftingEngine.EditorTools
{
    /// <summary>How bad a content problem is.</summary>
    public enum CraftingIssueSeverity
    {
        /// <summary>Worth knowing, harmless.</summary>
        Info = 0,

        /// <summary>The asset works but something is probably wrong.</summary>
        Warning = 1,

        /// <summary>The asset will not register, or will register and misbehave.</summary>
        Error = 2
    }

    /// <summary>One thing wrong with one asset.</summary>
    public readonly struct CraftingIssue
    {
        public readonly CraftingIssueSeverity Severity;
        public readonly string Message;

        /// <summary>Asset to ping when the user clicks the issue. May be null.</summary>
        public readonly Object Context;

        public CraftingIssue(CraftingIssueSeverity severity, string message, Object context = null)
        {
            Severity = severity;
            Message = message ?? string.Empty;
            Context = context;
        }

        public override string ToString() => Severity + ": " + Message;
    }

    /// <summary>
    /// Gathers issues while validating. Inspectors use one per selected asset; the global
    /// validator in session 9 uses one for the whole project.
    /// </summary>
    public sealed class CraftingIssues
    {
        private readonly List<CraftingIssue> _issues = new List<CraftingIssue>();

        public IReadOnlyList<CraftingIssue> All => _issues;

        public int Count => _issues.Count;

        public int ErrorCount { get; private set; }

        public int WarningCount { get; private set; }

        public bool HasErrors => ErrorCount > 0;

        /// <summary>True when nothing at all was reported.</summary>
        public bool IsClean => _issues.Count == 0;

        public void Info(string message, Object context = null)
            => Add(new CraftingIssue(CraftingIssueSeverity.Info, message, context));

        public void Warning(string message, Object context = null)
            => Add(new CraftingIssue(CraftingIssueSeverity.Warning, message, context));

        public void Error(string message, Object context = null)
            => Add(new CraftingIssue(CraftingIssueSeverity.Error, message, context));

        public void Add(in CraftingIssue issue)
        {
            _issues.Add(issue);

            if (issue.Severity == CraftingIssueSeverity.Error)
                ErrorCount++;
            else if (issue.Severity == CraftingIssueSeverity.Warning)
                WarningCount++;
        }

        public void Clear()
        {
            _issues.Clear();
            ErrorCount = 0;
            WarningCount = 0;
        }

        /// <summary>One-line summary for logs and headers.</summary>
        public string Summarise()
        {
            if (IsClean)
                return "No issues";

            return ErrorCount + " error(s), " + WarningCount + " warning(s), " + Count + " total";
        }
    }
}