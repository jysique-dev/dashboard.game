using UnityEditor;
using UnityEngine;

namespace LoopEngine.TagInput.EditorTools
{
    /// <summary>
    /// Entradas de menú del sistema de input por tags.
    /// </summary>
    public static class TagInputMenu
    {
        
        private const string CreateManagerPath = LoopRoutes.InputToolRoute + "/Tag Input/Crear Key Input Manager";
        private const string ToggleSymbolPath = LoopRoutes.InputToolRoute + "/Tag Input/Sistema activo (INPUT_TEST)";
        private const string ValidatePath = LoopRoutes.InputToolRoute + "/Tag Input/Validar escena";

        // ------------------------------------------------------------------

        [MenuItem(CreateManagerPath, false, 10)]
        private static void CreateManager()
        {
            var existing = Object.FindFirstObjectByType<KeyInputManager>();
            if (existing != null)
            {
                Debug.LogWarning(
                    $"[TagInput] Ya existe un KeyInputManager en '{existing.gameObject.name}'.", existing);
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            var go = new GameObject("[KeyInputManager]");
            go.AddComponent<KeyInputManager>();

            Undo.RegisterCreatedObjectUndo(go, "Crear Key Input Manager");
            Selection.activeGameObject = go;

            Debug.Log("[TagInput] KeyInputManager creado en la escena activa.", go);
        }

        // ------------------------------------------------------------------

        [MenuItem(ToggleSymbolPath, false, 20)]
        private static void ToggleSymbol()
        {
            bool next = !TagInputDefines.IsEnabled;
            TagInputDefines.SetEnabled(next);

            Debug.Log(next
                ? "[TagInput] INPUT_TEST añadido. Recompilando…"
                : "[TagInput] INPUT_TEST eliminado. El sistema queda fuera de la compilación.");
        }

        [MenuItem(ToggleSymbolPath, true)]
        private static bool ToggleSymbolValidate()
        {
            Menu.SetChecked(ToggleSymbolPath, TagInputDefines.IsEnabled);
            return true;
        }

        // ------------------------------------------------------------------

        [MenuItem(ValidatePath, false, 40)]
        private static void ValidateScene()
        {
            var managers = Object.FindObjectsByType<KeyInputManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (managers.Length == 0)
            {
                Debug.LogWarning("[TagInput] No hay ningún KeyInputManager en la escena activa.");
                return;
            }

            if (managers.Length > 1)
            {
                Debug.LogError(
                    $"[TagInput] Hay {managers.Length} KeyInputManager en la escena. " +
                    "Solo el primero en despertar quedará activo; el resto se autodesactivan.",
                    managers[0]);
            }

            if (!TagInputDefines.LegacyInputEnabled)
            {
                Debug.LogError(
                    "[TagInput] Active Input Handling no incluye el Input Manager legacy. " +
                    "KeyCode no se puede leer en este estado.");
            }

            if (!TagInputDefines.IsEnabled)
            {
                Debug.LogWarning(
                    "[TagInput] INPUT_TEST no está definido: la API está compilada como no-op.");
            }

            Debug.Log($"[TagInput] Validación terminada. Managers encontrados: {managers.Length}.");
        }
    }
}