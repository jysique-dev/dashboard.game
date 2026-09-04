using System.Collections.Generic;

namespace LoopEngine.LogEngine.Core
{
    /// <summary>
    /// Nucleo del modulo: recibe entradas, aplica el filtro y las reparte a los sinks.
    /// C# puro, sin UnityEngine: se puede testear con NUnit usando un sink falso.
    /// </summary>
    public sealed class LogRouter
    {
        private readonly List<ILogSink> _sinks = new List<ILogSink>(4);

        /// <summary>Filtro por canal. Si es null, todos los canales pasan.</summary>
        public ILogChannelFilter Filter { get; set; }

        /// <summary>Corte global de severidad, aplicado antes que el filtro por canal.</summary>
        public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;

        public int SinkCount => _sinks.Count;

        public void AddSink(ILogSink sink)
        {
            if (sink == null || _sinks.Contains(sink)) return;
            _sinks.Add(sink);
        }

        public void RemoveSink(ILogSink sink)
        {
            if (sink == null) return;
            _sinks.Remove(sink);
        }

        public void ClearSinks() => _sinks.Clear();

        /// <summary>
        /// Consulta previa para casos donde construir el mensaje es caro
        /// (recorrer una lista, serializar, etc.). Uso:
        /// <c>if (Log.IsEnabled(canal, nivel)) Log.Info(canal, Construir());</c>
        /// </summary>
        public bool IsEnabled(string channel, LogLevel level)
        {
            if (level < MinimumLevel) return false;
            var filter = Filter;
            return filter == null || filter.IsEnabled(channel, level);
        }

        public void Dispatch(in LogEntry entry)
        {
            if (!IsEnabled(entry.Channel, entry.Level)) return;

            // Recorrido por indice: evita el enumerador y tolera que un sink
            // se quite a si mismo durante el recorrido (se comprueba el rango).
            for (int i = 0; i < _sinks.Count; i++)
            {
                var sink = _sinks[i];
                if (sink == null) continue;
                sink.Write(entry);
            }
        }
    }
}