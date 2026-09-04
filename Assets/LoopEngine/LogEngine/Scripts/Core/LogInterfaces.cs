namespace LoopEngine.LogEngine.Core
{
    /// <summary>
    /// Destino de los mensajes. Implementaciones posibles: consola de Unity,
    /// archivo en disco, buffer en memoria, overlay en pantalla, servidor remoto.
    /// El router puede tener varios activos a la vez.
    /// </summary>
    public interface ILogSink
    {
        void Write(in LogEntry entry);
    }

    /// <summary>
    /// Decide si un canal deja pasar un nivel. La implementacion concreta del
    /// proyecto es el ScriptableObject <c>LogSettings</c>, pero se puede
    /// sustituir por cualquier otra cosa (tests, configuracion remota, etc.).
    /// </summary>
    public interface ILogChannelFilter
    {
        bool IsEnabled(string channel, LogLevel level);
    }
}