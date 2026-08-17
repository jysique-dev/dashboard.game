using UnityEngine;

namespace LoopEngine.GridEngine.Interaction
{
    /// <summary>
    /// Abstracción de la fuente de puntero que necesita GridInteractor.
    /// Desacopla la interacción de la grilla del origen concreto del input: puedes usar
    /// MouseGridPointerSource (Input System directo) o escribir un adaptador sobre tu
    /// InputReader central que implemente esta misma interfaz.
    /// </summary>
    public interface IGridPointerSource
    {
        /// <summary>Posición del puntero en píxeles de pantalla.</summary>
        Vector2 ScreenPosition { get; }

        /// <summary>False si no hay dispositivo/puntero válido este frame.</summary>
        bool IsValid { get; }

        bool PrimaryPressedThisFrame { get; }
        bool PrimaryReleasedThisFrame { get; }
        bool PrimaryHeld { get; }
        bool SecondaryPressedThisFrame { get; }
    }
}