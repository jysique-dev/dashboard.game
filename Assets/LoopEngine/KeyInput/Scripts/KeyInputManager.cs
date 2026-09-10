using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.TagInput
{
    /// <summary>
    /// Manager único de input por tags. Traduce identificadores de texto a KeyCode
    /// y expone consultas de teclado del Input Manager legacy.
    ///
    /// EXCLUSIÓN DE COMPILACIÓN
    /// -------------------------
    /// Toda la lógica está encerrada en "#if UNITY_EDITOR &amp;&amp; INPUT_TEST".
    /// - Sin el símbolo INPUT_TEST, los métodos existen pero devuelven false / valores neutros.
    /// - La declaración de la clase y los campos serializados SIEMPRE se compilan, para que
    ///   Unity no marque "Missing (Mono Script)" en las escenas ni pierda los datos guardados.
    ///
    /// Para activarlo: Edit &gt; Project Settings &gt; Player &gt; Other Settings &gt;
    /// Script Compilation &gt; Scripting Define Symbols, añade INPUT_TEST.
    /// </summary>
    [AddComponentMenu("Tag Input/Key Input Manager")]
    [DisallowMultipleComponent]
    public class KeyInputManager : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Configuración (siempre compilada: preserva los datos del inspector)
        // ------------------------------------------------------------------

        [Tooltip("Lista de bindings tag -> tecla.")]
        [SerializeField]
        private List<KeyBinding> bindings = new List<KeyBinding>();

        [Tooltip("Si está activo, \"Jump\" y \"jump\" son tags distintos.")]
        [SerializeField]
        private bool caseSensitiveTags = false;

        [Tooltip("Mantiene el manager al cargar otra escena.")]
        [SerializeField]
        private bool persistAcrossScenes = true;

        [Tooltip("Reporta por consola tags duplicados, vacíos o no registrados.")]
        [SerializeField]
        private bool logWarnings = true;

        // ------------------------------------------------------------------
        // Acceso estático
        // ------------------------------------------------------------------

        /// <summary>
        /// Instancia activa, o null si el sistema está excluido de la compilación
        /// o no hay ningún KeyInputManager en la escena.
        /// </summary>
        public static KeyInputManager Instance { get; private set; }

        /// <summary>
        /// True solo si el sistema está compilado Y hay una instancia lista.
        /// Úsalo para evitar ramas de código de prueba cuando el sistema no existe.
        /// </summary>
        public static bool IsAvailable => Instance != null;

        /// <summary>Atajo estático seguro: false si no hay manager.</summary>
        public static bool Down(string tag) => Instance != null && Instance.GetKeyDown(tag);

        /// <summary>Atajo estático seguro: false si no hay manager.</summary>
        public static bool Held(string tag) => Instance != null && Instance.GetKey(tag);

        /// <summary>Atajo estático seguro: false si no hay manager.</summary>
        public static bool Up(string tag) => Instance != null && Instance.GetKeyUp(tag);

        // ------------------------------------------------------------------
        // Estado interno
        // ------------------------------------------------------------------

#if UNITY_EDITOR && INPUT_TEST
        private Dictionary<string, KeyCode> map;

        /// <summary>Copia estable de los tags válidos, reconstruida junto con el mapa.</summary>
        private string[] cachedTags = System.Array.Empty<string>();
#endif

        // ------------------------------------------------------------------
        // Ciclo de vida
        // ------------------------------------------------------------------

        private void Awake()
        {
#if UNITY_EDITOR && INPUT_TEST
            if (Instance != null && Instance != this)
            {
                Debug.LogError(
                    $"[KeyInputManager] Ya existe una instancia en '{Instance.gameObject.name}'. " +
                    $"Se desactiva el duplicado en '{gameObject.name}'.", this);
                enabled = false;
                return;
            }

            Instance = this;

            if (persistAcrossScenes && transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            RebuildBindings();
            WarnIfLegacyInputDisabled();
#endif
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR && INPUT_TEST
            if (Instance == this)
            {
                Instance = null;
            }
#endif
        }

        // ------------------------------------------------------------------
        // Construcción del mapa
        // ------------------------------------------------------------------

        /// <summary>
        /// Reconstruye el diccionario interno a partir de la lista serializada.
        /// Llámalo si modificas los bindings por código en tiempo de ejecución.
        /// En un build sin INPUT_TEST no hace nada.
        /// </summary>
        public void RebuildBindings()
        {
#if UNITY_EDITOR && INPUT_TEST
            var comparer = caseSensitiveTags
                ? System.StringComparer.Ordinal
                : System.StringComparer.OrdinalIgnoreCase;

            map = new Dictionary<string, KeyCode>(bindings.Count, comparer);

            for (int i = 0; i < bindings.Count; i++)
            {
                KeyBinding binding = bindings[i];

                if (binding == null)
                {
                    Warn($"El binding en el índice {i} es null y se ignora.");
                    continue;
                }

                if (!binding.IsValid)
                {
                    Warn($"El binding en el índice {i} es inválido ({binding}) y se ignora. " +
                         "Necesita un tag con contenido y una tecla distinta de None.");
                    continue;
                }

                string key = binding.Tag.Trim();

                if (map.ContainsKey(key))
                {
                    Warn($"Tag duplicado \"{key}\" en el índice {i}. " +
                         $"Se conserva la primera aparición ({map[key]}) y se ignora {binding.Key}.");
                    continue;
                }

                map.Add(key, binding.Key);
            }

            cachedTags = new string[map.Count];
            map.Keys.CopyTo(cachedTags, 0);
#endif
        }

        // ------------------------------------------------------------------
        // API pública de consulta
        // ------------------------------------------------------------------

        /// <summary>True el frame en el que se presiona la tecla asociada al tag.</summary>
        public bool GetKeyDown(string tag)
        {
#if UNITY_EDITOR && INPUT_TEST
            return TryGetKeyCode(tag, out KeyCode code) && ReadKeyDown(code);
#else
            return false;
#endif
        }

        /// <summary>True mientras la tecla asociada al tag se mantiene presionada.</summary>
        public bool GetKey(string tag)
        {
#if UNITY_EDITOR && INPUT_TEST
            return TryGetKeyCode(tag, out KeyCode code) && ReadKey(code);
#else
            return false;
#endif
        }

        /// <summary>True el frame en el que se suelta la tecla asociada al tag.</summary>
        public bool GetKeyUp(string tag)
        {
#if UNITY_EDITOR && INPUT_TEST
            return TryGetKeyCode(tag, out KeyCode code) && ReadKeyUp(code);
#else
            return false;
#endif
        }

        /// <summary>
        /// Resuelve el KeyCode asociado a un tag. Devuelve false si el tag no está registrado.
        /// </summary>
        public bool TryGetKeyCode(string tag, out KeyCode key)
        {
            key = KeyCode.None;

#if UNITY_EDITOR && INPUT_TEST
            if (string.IsNullOrWhiteSpace(tag))
            {
                Warn("Se consultó un tag nulo o vacío.");
                return false;
            }

            if (map == null)
            {
                RebuildBindings();
            }

            if (map.TryGetValue(tag.Trim(), out key))
            {
                return true;
            }

            Warn($"Tag no registrado: \"{tag}\".");
            key = KeyCode.None;
            return false;
#else
            return false;
#endif
        }

        /// <summary>True si el tag existe en el mapa. No reporta advertencias.</summary>
        public bool IsTagRegistered(string tag)
        {
#if UNITY_EDITOR && INPUT_TEST
            if (string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            if (map == null)
            {
                RebuildBindings();
            }

            return map.ContainsKey(tag.Trim());
#else
            return false;
#endif
        }

        /// <summary>
        /// Tags válidos registrados, en el orden en que se construyó el mapa.
        /// El array devuelto es la copia interna: trátalo como solo lectura.
        /// Devuelve un array vacío cuando el sistema está excluido de la compilación.
        /// </summary>
        public IReadOnlyList<string> GetAllTags()
        {
#if UNITY_EDITOR && INPUT_TEST
            if (map == null)
            {
                RebuildBindings();
            }

            return cachedTags;
#else
            return System.Array.Empty<string>();
#endif
        }

        // ------------------------------------------------------------------
        // Lectura del Input Manager legacy
        // ------------------------------------------------------------------
        // Estas llamadas lanzan InvalidOperationException si Active Input Handling
        // está en "Input System Package (New)". El símbolo ENABLE_LEGACY_INPUT_MANAGER
        // solo está definido cuando el backend antiguo está activo, así que actúa
        // como red de seguridad.

#if UNITY_EDITOR && INPUT_TEST
        private static bool ReadKeyDown(KeyCode code)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(code);
#else
            return false;
#endif
        }

        private static bool ReadKey(KeyCode code)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(code);
#else
            return false;
#endif
        }

        private static bool ReadKeyUp(KeyCode code)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyUp(code);
#else
            return false;
#endif
        }

        private void WarnIfLegacyInputDisabled()
        {
#if !ENABLE_LEGACY_INPUT_MANAGER
            Debug.LogError(
                "[KeyInputManager] El Input Manager legacy está desactivado, así que KeyCode no se puede leer. " +
                "Ve a Edit > Project Settings > Player > Other Settings > Configuration > Active Input Handling " +
                "y elige \"Input Manager (Old)\" o \"Both\". El editor se reiniciará.", this);
#endif
        }

        private void Warn(string message)
        {
            if (logWarnings)
            {
                Debug.LogWarning($"[KeyInputManager] {message}", this);
            }
        }
#endif
    }
}