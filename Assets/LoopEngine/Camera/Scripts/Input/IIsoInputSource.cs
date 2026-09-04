using UnityEngine;

namespace LoopEngine.IsoCamera.InputAbstraction
{
    /// <summary>
    /// Contrato de input de la cámara. Ninguna clase del rig referencia
    /// UnityEngine.Input ni UnityEngine.InputSystem directamente.
    /// Sustituible por un stub en tests automatizados o por un binding propio.
    /// </summary>
    public interface IIsoInputSource
    {
        /// <summary>Eje de movimiento normalizado. x = derecha, y = adelante.</summary>
        Vector2 Move { get; }

        /// <summary>Zoom por rueda. Positivo = acercar. Normalizado a ±1 por muesca.</summary>
        float Zoom { get; }

        /// <summary>-1, 0 o +1 en el frame en que se pulsa la tecla de rotación.</summary>
        int RotateStep { get; }

        /// <summary>Posición del puntero en píxeles de pantalla.</summary>
        Vector2 PointerPosition { get; }

        /// <summary>True mientras se mantiene el botón de arrastre (por defecto, botón central).</summary>
        bool PanHeld { get; }

        /// <summary>Desplazamiento del puntero en píxeles desde el frame anterior.</summary>
        Vector2 PanDelta { get; }
    }

    /// <summary>Fuente vacía. Evita comprobaciones de null en todos los consumidores.</summary>
    public sealed class NullIsoInputSource : IIsoInputSource
    {
        public static readonly NullIsoInputSource Instance = new NullIsoInputSource();
        public Vector2 Move => Vector2.zero;
        public float Zoom => 0f;
        public int RotateStep => 0;
        public Vector2 PointerPosition => Vector2.zero;
        public bool PanHeld => false;
        public Vector2 PanDelta => Vector2.zero;
    }
}