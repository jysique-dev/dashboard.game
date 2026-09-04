using System;
using UnityEngine;

namespace LoopEngine.Core
{
    /// <summary>
    /// Marca un campo de referencia NORMAL (no OptionalRef) como obligatorio.
    /// El validador lo reporta si esta vacio y el drawer lo pinta con un aviso.
    ///
    /// NO lo pongas sobre un OptionalRef: Unity da prioridad al drawer del atributo sobre el
    /// del tipo, asi que anularia el dibujo del toggle. Ademas seria redundante — un
    /// OptionalRef activado ya es obligatorio por definicion.
    ///
    /// Uso previsto: dependencias sin las que el componente no tiene sentido alguno
    /// (el GridMover de un controlador de movimiento, por ejemplo).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class RequiredRefAttribute : PropertyAttribute
    {
    }
}