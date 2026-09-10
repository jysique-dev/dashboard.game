using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class FolderColorDrawer
{
    private static Texture _folderTex;

    static FolderColorDrawer()
    {
        EditorApplication.projectWindowItemOnGUI -= OnItemGUI;
        EditorApplication.projectWindowItemOnGUI += OnItemGUI;
    }

    private static void OnItemGUI(string guid, Rect rect)
    {
        if (Event.current.type != EventType.Repaint) return;
        if (!FolderColorSettings.instance.TryGetColor(guid, out Color color)) return;

        // Cache: IconContent hace lookup por string, no lo llames por item/frame.
        if (_folderTex == null)
            _folderTex = EditorGUIUtility.IconContent("Folder Icon").image;
        if (_folderTex == null) return;

        bool isList = rect.width > rect.height;

        Rect icon = isList
            ? new Rect(rect.x, rect.y, rect.height, rect.height)
            : new Rect(rect.x, rect.y, rect.width, rect.width);

        Color prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(icon, _folderTex, ScaleMode.ScaleToFit);
        GUI.color = prev;
    }
}