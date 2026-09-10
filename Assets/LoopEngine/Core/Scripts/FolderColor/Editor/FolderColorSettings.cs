using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[FilePath("ProjectSettings/FolderColors.asset", FilePathAttribute.Location.ProjectFolder)]
internal sealed class FolderColorSettings : ScriptableSingleton<FolderColorSettings>
{
    [System.Serializable]
    private struct Entry
    {
        public string guid;
        public Color color;
    }

    [SerializeField] private List<Entry> _entries = new List<Entry>();

    // No serializado: se reconstruye tras cada domain reload.
    private Dictionary<string, Color> _cache;

    private Dictionary<string, Color> BuildCache()
    {
        var dict = new Dictionary<string, Color>(_entries.Count);
        foreach (Entry e in _entries)
        {
            if (string.IsNullOrEmpty(e.guid)) continue;
            dict[e.guid] = e.color;   // indexador, no Add: tolera duplicados
        }
        return dict;
    }

    public bool TryGetColor(string guid, out Color color)
    {
        _cache ??= BuildCache();
        return _cache.TryGetValue(guid, out color);
    }

    public void Set(string guid, Color color)
    {
        int i = _entries.FindIndex(e => e.guid == guid);
        if (i >= 0)
        {
            Entry e = _entries[i];
            e.color = color;
            _entries[i] = e;          // struct: hay que reasignar
        }
        else
        {
            _entries.Add(new Entry { guid = guid, color = color });
        }

        _cache = null;
        Save(true);
        EditorApplication.RepaintProjectWindow();
    }

    public void Clear(string guid)
    {
        if (_entries.RemoveAll(e => e.guid == guid) == 0) return;
        _cache = null;
        Save(true);
        EditorApplication.RepaintProjectWindow();
    }
}