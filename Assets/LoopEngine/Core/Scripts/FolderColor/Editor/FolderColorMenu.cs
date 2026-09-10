using UnityEditor;
using UnityEngine;

internal static class FolderColorMenu
{
    private const string kRoot = "Assets/Create/" + LoopRoutes.ColorRoute + "/Color de carpeta/";

    [MenuItem(kRoot + "Rojo", false, 1100)]
    private static void Red() => Apply(new Color(0.85f, 0.30f, 0.30f));

    [MenuItem(kRoot + "Verde", false, 1101)]
    private static void Green() => Apply(new Color(0.35f, 0.75f, 0.40f));

    [MenuItem(kRoot + "Azul", false, 1102)]
    private static void Blue() => Apply(new Color(0.30f, 0.55f, 0.90f));

    [MenuItem(kRoot + "Amarillo", false, 1103)]
    private static void Yellow() => Apply(new Color(0.90f, 0.75f, 0.25f));

    [MenuItem(kRoot + "Quitar color", false, 1120)]
    private static void Reset()
    {
        foreach (string guid in Selection.assetGUIDs)
            FolderColorSettings.instance.Clear(guid);
    }

    // Una sola función de validación sirve para todos los items del submenú.
    [MenuItem(kRoot + "Rojo", true)]
    [MenuItem(kRoot + "Verde", true)]
    [MenuItem(kRoot + "Azul", true)]
    [MenuItem(kRoot + "Amarillo", true)]
    [MenuItem(kRoot + "Quitar color", true)]
    private static bool ValidateFolderSelected()
    {
        foreach (string guid in Selection.assetGUIDs)
        {
            if (AssetDatabase.IsValidFolder(AssetDatabase.GUIDToAssetPath(guid)))
                return true;
        }
        return false;
    }

    private static void Apply(Color color)
    {
        foreach (string guid in Selection.assetGUIDs)
        {
            if (!AssetDatabase.IsValidFolder(AssetDatabase.GUIDToAssetPath(guid)))
                continue;
            FolderColorSettings.instance.Set(guid, color);
        }
    }
}