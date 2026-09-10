using UnityEditor;
using UnityEngine;

namespace LoopEngine.TagInput.EditorTools
{
    /// <summary>
    /// Revisa el estado del sistema justo antes de entrar en Play Mode.
    ///
    /// Solo reporta cuando hay algo que decir y solo si existe un KeyInputManager
    /// en la escena. Un proyecto que no usa el sistema nunca ve un solo mensaje.
    /// </summary>
    [InitializeOnLoad]
    public static class TagInputPlayModeValidator
    {
        static TagInputPlayModeValidator()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // ExitingEditMode es el instante previo a entrar en Play: los objetos
            // de la escena todavía existen y aún no se ha ejecutado ningún Awake.
            if (state != PlayModeStateChange.ExitingEditMode)
            {
                return;
            }

            var managers = Object.FindObjectsByType<KeyInputManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (managers.Length == 0)
            {
                return;
            }

            if (!TagInputDefines.IsEnabled)
            {
                Debug.LogWarning(
                    "[TagInput] Hay un KeyInputManager en la escena pero INPUT_TEST no está definido. " +
                    "Toda consulta por tag devolverá false. " +
                    "Actívalo desde Tools > Tag Input > Sistema activo (INPUT_TEST).",
                    managers[0]);
                return;
            }

            if (!TagInputDefines.LegacyInputEnabled)
            {
                Debug.LogError(
                    "[TagInput] Active Input Handling no incluye el Input Manager legacy, " +
                    "así que KeyCode no se puede leer. " +
                    "Project Settings > Player > Other Settings > Active Input Handling: " +
                    "\"Input Manager (Old)\" o \"Both\".",
                    managers[0]);
                return;
            }

            if (managers.Length > 1)
            {
                Debug.LogError(
                    $"[TagInput] Hay {managers.Length} KeyInputManager en la escena. " +
                    "Los duplicados se autodesactivan al despertar, pero cuál sobrevive " +
                    "depende del orden de inicialización y no es determinista.",
                    managers[0]);
            }
        }
    }
}