using UnityEditor;
using UnityEditor.Build;

namespace LoopEngine.TagInput.EditorTools
{
    /// <summary>
    /// Lectura y escritura del símbolo de compilación INPUT_TEST.
    ///
    /// Usa la API basada en NamedBuildTarget. Las variantes ...ForGroup(BuildTargetGroup)
    /// están marcadas como obsoletas en Unity 6; la documentación indica explícitamente
    /// usar PlayerSettings.GetScriptingDefineSymbols / SetScriptingDefineSymbols en su lugar.
    /// </summary>
    public static class TagInputDefines
    {
        public const string Symbol = "INPUT_TEST";

        /// <summary>
        /// Target activo en el editor. Los símbolos son por plataforma de destino,
        /// así que siempre se opera sobre la seleccionada en Build Settings.
        /// </summary>
        private static NamedBuildTarget CurrentTarget =>
            NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);

        /// <summary>True si INPUT_TEST está definido para la plataforma activa.</summary>
        public static bool IsEnabled
        {
            get
            {
                string defines = PlayerSettings.GetScriptingDefineSymbols(CurrentTarget);
                return ContainsSymbol(defines, Symbol);
            }
        }

        /// <summary>
        /// Añade o quita INPUT_TEST. Provoca una recompilación de scripts.
        /// </summary>
        public static void SetEnabled(bool enabled)
        {
            NamedBuildTarget target = CurrentTarget;
            string defines = PlayerSettings.GetScriptingDefineSymbols(target);

            bool present = ContainsSymbol(defines, Symbol);
            if (present == enabled)
            {
                return;
            }

            string result;

            if (enabled)
            {
                result = string.IsNullOrEmpty(defines)
                    ? Symbol
                    : defines.TrimEnd(';') + ";" + Symbol;
            }
            else
            {
                var kept = new System.Collections.Generic.List<string>();
                foreach (string piece in defines.Split(';'))
                {
                    string trimmed = piece.Trim();
                    if (trimmed.Length > 0 && trimmed != Symbol)
                    {
                        kept.Add(trimmed);
                    }
                }

                result = string.Join(";", kept);
            }

            PlayerSettings.SetScriptingDefineSymbols(target, result);
            AssetDatabase.SaveAssets();
        }

        private static bool ContainsSymbol(string defines, string symbol)
        {
            if (string.IsNullOrEmpty(defines))
            {
                return false;
            }

            foreach (string piece in defines.Split(';'))
            {
                if (piece.Trim() == symbol)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True si el backend de input legacy está activo. Sin él, KeyCode no se puede leer:
        /// Input.GetKey lanza InvalidOperationException.
        /// </summary>
        public static bool LegacyInputEnabled
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return true;
#else
                return false;
#endif
            }
        }
    }
}