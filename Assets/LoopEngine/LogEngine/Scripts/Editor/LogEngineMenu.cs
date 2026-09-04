using System;
using System.Collections.Generic;
using LoopEngine.LogEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace LoopEngine.LogEngine.EditorTools
{
    /// <summary>
    /// Utilidades minimas de editor para la sesion L1. La ventana completa
    /// (lista de canales con toggles, al estilo de ColorPaletteWindow) queda para L2.
    /// </summary>
    public static class LogEngineMenu
    {
        private const string AssetFolder = LoopRoutes.ResourcesFolder + "/LogEngine";
        private const string AssetPath = AssetFolder + "/LogSettings.asset";

        [MenuItem(LoopRoutes.LoogingToolRoute + "/Create Log Settings", priority = 0)]
        public static void CreateSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<LogSettings>(AssetPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            EnsureFolder(AssetFolder);

            var settings = ScriptableObject.CreateInstance<LogSettings>();
            settings.AddMissingDefaultChannels();

            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            LogBootstrap.Initialize(force: true);

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem(LoopRoutes.LoogingToolRoute + "/Select Log Settings", priority = 1)]
        public static void SelectSettings()
        {
            var settings = Resources.Load<LogSettings>(LogBootstrap.ResourcesPath);
            if (settings == null)
            {
                CreateSettings();
                return;
            }

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        // --- Simbolo LOOP_LOGS -------------------------------------------------
        // Sirve para dejar los logs vivos en una build de release (por ejemplo,
        // una build de QA que no quieres marcar como Development Build).

        [MenuItem(LoopRoutes.LoogingToolRoute + "/Force Logs In Release Build", priority = 20)]
        public static void ToggleForceLogs()
        {
            var target = NamedBuildTarget.FromBuildTargetGroup(
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));

            string raw = PlayerSettings.GetScriptingDefineSymbols(target);
            var symbols = new List<string>(raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));

            if (symbols.Contains(Log.SymbolForce)) symbols.Remove(Log.SymbolForce);
            else symbols.Add(Log.SymbolForce);

            // Ojo: el cambio no surte efecto hasta que el editor recupera el control
            // y recompila los scripts.
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
        }

        [MenuItem(LoopRoutes.LoogingToolRoute + "/Force Logs", validate = true)]
        private static bool ToggleForceLogsValidate()
        {
            var target = NamedBuildTarget.FromBuildTargetGroup(
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));

            string raw = PlayerSettings.GetScriptingDefineSymbols(target);
            Menu.SetChecked("Tools/LoopEngine/Logging/Force Logs In Release Build",
                Array.IndexOf(raw.Split(';'), Log.SymbolForce) >= 0);
            return true;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string[] parts = path.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}