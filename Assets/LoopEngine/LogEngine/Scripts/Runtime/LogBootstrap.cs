using UnityEngine;

namespace LoopEngine.LogEngine
{
    /// <summary>
    /// Monta el router antes de que cargue la primera escena, sin necesidad de
    /// poner ningun GameObject en la escena.
    /// </summary>
    public static class LogBootstrap
    {
        /// <summary>Ruta relativa dentro de una carpeta Resources (sin extension).</summary>
        public const string ResourcesPath = "LoopEngine/LogSettings";

        // Todo el cuerpo se compila solo cuando los logs estan activos, asi que en
        // release ni siquiera se hace el Resources.Load.
#if UNITY_EDITOR || DEVELOPMENT_BUILD || LOOP_LOGS

        private static bool s_initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeOnLoad() => Initialize();

#if UNITY_EDITOR
        // Tambien al recompilar, para que los logs de codigo [ExecuteAlways]
        // y de las herramientas de editor funcionen fuera de play mode.
        [UnityEditor.InitializeOnLoadMethod]
        private static void InitializeInEditor() => Initialize();
#endif

        /// <summary>Idempotente: se puede llamar tantas veces como haga falta.</summary>
        public static void Initialize(bool force = false)
        {
            if (s_initialized && !force) return;
            s_initialized = true;

            var settings = Resources.Load<LogSettings>(ResourcesPath);
            if (settings == null)
            {
                // Sin asset: se monta igual con filtro nulo (todo pasa) para no
                // perder mensajes mientras el asset no exista.
                Log.Configure(null);
                Debug.LogWarning(
                    $"[LogEngine] No se encontro LogSettings en Resources/{ResourcesPath}. " +
                    "Crealo desde Tools > LoopEngine > Logging > Create Log Settings.");
                return;
            }

            Log.Configure(settings);
        }

#else
        /// <summary>En release no hay nada que inicializar.</summary>
        public static void Initialize(bool force = false) { }
#endif
    }
}