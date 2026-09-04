using LoopEngine.LogEngine.Core;
using System.Diagnostics;
using UnityEngine;
using LogLevel = LoopEngine.LogEngine.Core.LogLevel;
using UnityDebug = UnityEngine.Debug;
using UnityObject = UnityEngine.Object;

namespace LoopEngine.LogEngine
{
    /// <summary>
    /// Punto de entrada del modulo. Uso:
    /// <code>
    /// Log.Info(LogChannels.Grid, $"Celda {cell} ocupada por {def.name}", this);
    /// Log.Warning(LogChannels.Wfc, "Backtracking en profundidad 12");
    /// Log.Error(LogChannels.Terrain, "TerrainRaycaster sin malla generada", this);
    /// </code>
    /// </summary>
    /// <remarks>
    /// <para><b>Por que [Conditional] y no un if:</b> cuando el simbolo no esta definido,
    /// el compilador omite la llamada entera, incluida la evaluacion de los argumentos.
    /// Un <c>$"..."</c> caro en un Update nunca se ejecuta ni asigna memoria en release.
    /// Un <c>if (habilitado)</c> en cambio si evalua los argumentos antes de entrar.</para>
    ///
    /// <para><b>Cuidado:</b> por lo mismo, no metas efectos secundarios en los argumentos
    /// (<c>Log.Info(canal, $"{contador++}")</c> deja de incrementar en release).</para>
    ///
    /// <para><b>Simbolos:</b> las llamadas sobreviven si esta definido UNITY_EDITOR,
    /// DEVELOPMENT_BUILD o LOOP_LOGS. Varios [Conditional] sobre el mismo metodo se
    /// combinan con OR.</para>
    /// </remarks>
    public static class Log
    {
        /// <summary>Definido siempre dentro del editor.</summary>
        public const string SymbolEditor = "UNITY_EDITOR";

        /// <summary>Definido por Unity cuando la build se marca como Development Build.</summary>
        public const string SymbolDevelopmentBuild = "DEVELOPMENT_BUILD";

        /// <summary>Simbolo propio, para forzar logs en una build de release (QA).</summary>
        public const string SymbolForce = "LOOP_LOGS";

        private static readonly LogRouter s_router = new LogRouter();

        /// <summary>Router compartido: aqui se enchufan sinks y filtro.</summary>
        public static LogRouter Router => s_router;

        // ---------------------------------------------------------------------
        // API de desarrollo: DESAPARECE de la build de release.
        // ---------------------------------------------------------------------

        [Conditional(SymbolEditor), Conditional(SymbolDevelopmentBuild), Conditional(SymbolForce)]
        [HideInCallstack]
        public static void Trace(string channel, string message, UnityObject context = null)
            => Dispatch(LogLevel.Trace, channel, message, context);

        [Conditional(SymbolEditor), Conditional(SymbolDevelopmentBuild), Conditional(SymbolForce)]
        [HideInCallstack]
        public static void Info(string channel, string message, UnityObject context = null)
            => Dispatch(LogLevel.Info, channel, message, context);

        [Conditional(SymbolEditor), Conditional(SymbolDevelopmentBuild), Conditional(SymbolForce)]
        [HideInCallstack]
        public static void Warning(string channel, string message, UnityObject context = null)
            => Dispatch(LogLevel.Warning, channel, message, context);

        [Conditional(SymbolEditor), Conditional(SymbolDevelopmentBuild), Conditional(SymbolForce)]
        [HideInCallstack]
        public static void Error(string channel, string message, UnityObject context = null)
            => Dispatch(LogLevel.Error, channel, message, context);

        // ---------------------------------------------------------------------
        // API de produccion: SOBREVIVE en release. Usar con cuentagotas.
        // ---------------------------------------------------------------------

        /// <summary>
        /// Error que si quieres ver en el log del jugador (fallo de guardado, asset
        /// critico ausente). No lleva [Conditional].
        /// </summary>
        [HideInCallstack]
        public static void Critical(string channel, string message, UnityObject context = null)
        {
            if (s_router.SinkCount > 0)
            {
                s_router.Dispatch(new LogEntry(LogLevel.Error, channel, message, context));
                return;
            }

            // En release no hay sinks montados: se va directo a la consola de Unity.
            UnityDebug.LogError($"[{channel}] {message}", context);
        }

        /// <summary>Excepcion atrapada. Tampoco se elimina en release.</summary>
        [HideInCallstack]
        public static void Exception(System.Exception exception, UnityObject context = null)
        {
            if (exception == null) return;
            UnityDebug.LogException(exception, context);
        }

        // ---------------------------------------------------------------------
        // Utilidades
        // ---------------------------------------------------------------------

        /// <summary>
        /// Para envolver construcciones caras de mensaje. No es [Conditional]
        /// (devuelve bool), pero en release el router no tiene filtro ni sinks
        /// y la comprobacion es trivial.
        /// </summary>
        public static bool IsEnabled(string channel, LogLevel level) => s_router.IsEnabled(channel, level);

        /// <summary>Reconfigura el router. Idempotente: limpia antes de montar.</summary>
        public static void Configure(LogSettings settings)
        {
            s_router.ClearSinks();
            s_router.Filter = settings;
            s_router.MinimumLevel = settings != null ? settings.GlobalMinimumLevel : LogLevel.Trace;
            s_router.AddSink(new UnityConsoleSink(settings));
        }

        [HideInCallstack]
        private static void Dispatch(LogLevel level, string channel, string message, UnityObject context)
        {
            s_router.Dispatch(new LogEntry(level, channel, message, context));
        }
    }
}