namespace LoopEngine.LogEngine.Core
{
    /// <summary>
    /// Un mensaje ya resuelto, listo para ser escrito por los sinks.
    /// Es readonly struct para no generar basura por cada log.
    /// </summary>
    /// <remarks>
    /// <see cref="Context"/> es <c>object</c> y no <c>UnityEngine.Object</c> a proposito:
    /// esta capa no puede depender de UnityEngine. El sink de Unity hace el cast
    /// (<c>entry.Context as UnityEngine.Object</c>) y, si no lo es, lo ignora.
    /// </remarks>
    public readonly struct LogEntry
    {
        public readonly LogLevel Level;

        /// <summary>Etiqueta / canal del mensaje. Ej: "Grid", "WFC", "Terrain".</summary>
        public readonly string Channel;

        public readonly string Message;

        /// <summary>Objeto asociado (opcional). En Unity permite hacer ping al hacer click en la consola.</summary>
        public readonly object Context;

        public LogEntry(LogLevel level, string channel, string message, object context = null)
        {
            Level = level;
            Channel = string.IsNullOrEmpty(channel) ? "General" : channel;
            Message = message;
            Context = context;
        }
    }
}