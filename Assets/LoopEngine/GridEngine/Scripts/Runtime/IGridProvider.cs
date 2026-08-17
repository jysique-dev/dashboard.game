using System;

namespace LoopEngine.GridEngine
{
    /// <summary>
    /// Implementado por cualquier componente de escena que posea y exponga una grilla.
    /// Los sistemas (visualización, input, colocación...) piden la grilla a través de
    /// esta interfaz en lugar de conocer el host concreto. Es el punto de conexión
    /// entre módulos a partir de la Sesión 2.
    /// </summary>
    public interface IGridProvider
    {
        /// <summary>La grilla como vista de solo lectura. Null hasta que el host la construye (en Awake).</summary>
        IReadOnlyGrid Grid { get; }

        /// <summary>Se dispara cuando la grilla queda construida y lista para usarse.</summary>
        event Action<IReadOnlyGrid> OnGridReady;
    }
}