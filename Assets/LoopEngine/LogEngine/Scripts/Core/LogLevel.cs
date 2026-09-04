namespace LoopEngine.LogEngine.Core
{
    /// <summary>
    /// Severidad de un mensaje. El orden numerico importa: se usa para filtrar por
    /// "nivel minimo" (todo lo que sea menor al minimo se descarta).
    /// </summary>
    public enum LogLevel
    {
        /// <summary>Ruido de desarrollo: valores por frame, pasos internos de un algoritmo.</summary>
        Trace = 0,

        /// <summary>Log "normal": eventos relevantes del flujo (se cargo la ciudad, se coloco X).</summary>
        Info = 1,

        /// <summary>Algo raro pero recuperable: dato faltante, fallback aplicado.</summary>
        Warning = 2,

        /// <summary>Error: invariante rota, referencia nula donde no deberia.</summary>
        Error = 3,

        /// <summary>Solo como umbral: "no dejar pasar nada".</summary>
        None = 4
    }
}