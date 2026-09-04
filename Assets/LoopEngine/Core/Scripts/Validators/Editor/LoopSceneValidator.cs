using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using LoopEngine.Core;

namespace LoopEngine.Core.EditorTools
{
    /// <summary>
    /// Revisa la escena antes de entrar en play y, si encuentra dependencias mal cableadas,
    /// CANCELA la entrada en play.
    ///
    /// Por que abortar en vez de solo avisar: un warning en consola se pierde entre los demas
    /// y el sintoma real aparece minutos despues, en forma de comportamiento raro (una unidad
    /// que trepa un muro porque una regla estaba sin asignar). Abortar convierte un fallo de
    /// diagnostico dificil en uno imposible de ignorar.
    ///
    /// Se puede desactivar desde el menu si estorba (por ejemplo, para probar una escena a
    /// medio montar). La preferencia se guarda en EditorPrefs, igual que la paleta activa de
    /// ColorEngine.
    /// </summary>
    [InitializeOnLoad]
    public static class LoopSceneValidator
    {
        private const string AbortPrefKey = "LoopEngine.Core.AbortPlayOnValidationErrors";
        private const string MenuValidate = "Tools/LoopEngine/Validar escena";
        private const string MenuToggle = "Tools/LoopEngine/Abortar play si hay errores";

        private static readonly List<ValidationIssue> Buffer = new List<ValidationIssue>();

        static LoopSceneValidator()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static bool AbortEnabled
        {
            get => EditorPrefs.GetBool(AbortPrefKey, true);
            set => EditorPrefs.SetBool(AbortPrefKey, value);
        }

        [MenuItem(MenuToggle)]
        private static void ToggleAbort() => AbortEnabled = !AbortEnabled;

        [MenuItem(MenuToggle, true)]
        private static bool ToggleAbortValidate()
        {
            Menu.SetChecked(MenuToggle, AbortEnabled);
            return true;
        }

        [MenuItem(MenuValidate)]
        private static void ValidateFromMenu()
        {
            int count = Validate(out string report);

            if (count == 0)
            {
                Debug.Log("[LoopEngine] Escena validada: sin problemas de cableado.");
                return;
            }

            Debug.LogWarning($"[LoopEngine] {count} problema(s) de cableado:\n{report}");
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            // ExitingEditMode es el unico momento en que todavia se puede cancelar la entrada.
            if (state != PlayModeStateChange.ExitingEditMode) return;
            if (!AbortEnabled) return;

            int count = Validate(out string report);
            if (count == 0) return;

            Debug.LogError(
                $"[LoopEngine] Play cancelado: {count} problema(s) de cableado.\n{report}\n" +
                "Desactivalo en Tools > LoopEngine si necesitas jugar igualmente.");

            EditorApplication.isPlaying = false;
        }

        /// <summary>
        /// Recorre TODOS los MonoBehaviour de las escenas cargadas, incluidos los de objetos
        /// inactivos: un componente desactivado hoy es el que se activa manana y falla.
        /// </summary>
        private static int Validate(out string report)
        {
            Buffer.Clear();

            MonoBehaviour[] all = Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < all.Length; i++)
            {
                // Un MonoBehaviour con el script roto aparece como null en el array.
                if (all[i] == null) continue;

                SceneValidation.Collect(all[i], Buffer);
            }

            if (Buffer.Count == 0)
            {
                report = string.Empty;
                return 0;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < Buffer.Count; i++)
            {
                ValidationIssue issue = Buffer[i];
                string path = issue.Context is Component component
                    ? GetHierarchyPath(component.transform)
                    : issue.ComponentName;

                builder.AppendLine($"  - {path} -> {issue.ComponentName}.{issue.FieldName}: {issue.Message}");
            }

            report = builder.ToString();
            return Buffer.Count;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            Transform current = transform.parent;

            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }
}