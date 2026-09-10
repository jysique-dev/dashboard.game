using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LoopEngine.CraftingEngine.EditorTools
{
    /// <summary>What an import would do to one entry.</summary>
    public enum ImportAction
    {
        /// <summary>No asset with this id exists; one will be created.</summary>
        Create = 0,

        /// <summary>An asset with this id exists; its content will be overwritten.</summary>
        Update = 1,

        /// <summary>Something is wrong; the entry will be skipped.</summary>
        Skip = 2
    }

    /// <summary>One planned change.</summary>
    public readonly struct ImportEntry
    {
        public readonly string Section;
        public readonly string Id;
        public readonly ImportAction Action;

        /// <summary>The asset that will be overwritten, for Update. Null otherwise.</summary>
        public readonly Object Existing;

        public ImportEntry(string section, string id, ImportAction action, Object existing = null)
        {
            Section = section;
            Id = id;
            Action = action;
            Existing = existing;
        }

        public override string ToString() => $"{Action} {Section} '{Id}'";
    }

    /// <summary>
    /// Everything an import would do, computed without touching a single asset.
    /// </summary>
    /// <remarks>
    /// Existing content is overwritten in place rather than replaced, so an update keeps the
    /// asset's GUID. Every scene object, prefab and other asset already pointing at it keeps
    /// working. Deleting and recreating would break all of them silently.
    /// </remarks>
    public sealed class CraftingImportPlan
    {
        private readonly List<ImportEntry> _entries = new List<ImportEntry>();

        public IReadOnlyList<ImportEntry> Entries => _entries;

        public int CreateCount { get; private set; }

        public int UpdateCount { get; private set; }

        public int SkipCount { get; private set; }

        public int ChangeCount => CreateCount + UpdateCount;

        public bool IsEmpty => _entries.Count == 0;

        public void Add(in ImportEntry entry)
        {
            _entries.Add(entry);

            switch (entry.Action)
            {
                case ImportAction.Create: CreateCount++; break;
                case ImportAction.Update: UpdateCount++; break;
                default: SkipCount++; break;
            }
        }

        /// <summary>Short summary for a confirmation dialog.</summary>
        public string Summarise()
            => $"{CreateCount} to create, {UpdateCount} to update, {SkipCount} skipped.";

        /// <summary>Full listing, one line per entry, for the console.</summary>
        public string Describe(int maxLines = 60)
        {
            var builder = new StringBuilder(Summarise());

            int lines = System.Math.Min(_entries.Count, maxLines);
            for (int i = 0; i < lines; i++)
                builder.AppendLine().Append("  ").Append(_entries[i]);

            if (_entries.Count > maxLines)
                builder.AppendLine().Append("  ... and ").Append(_entries.Count - maxLines).Append(" more");

            return builder.ToString();
        }

        public void Clear()
        {
            _entries.Clear();
            CreateCount = 0;
            UpdateCount = 0;
            SkipCount = 0;
        }
    }
}