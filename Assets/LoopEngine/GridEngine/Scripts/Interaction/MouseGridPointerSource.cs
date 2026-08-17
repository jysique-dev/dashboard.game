using UnityEngine;
using UnityEngine.InputSystem;

namespace LoopEngine.GridEngine.Interaction
{
    /// <summary>
    /// Implementación por defecto de IGridPointerSource leyendo el mouse del Input System nuevo.
    /// Funciona sin configuración adicional. Requiere el paquete com.unity.inputsystem.
    ///
    /// Para integrarlo con tu InputReader central, crea OTRO componente que implemente
    /// IGridPointerSource y lea de tu asset GameControls (ver nota en el chat), y asígnalo
    /// en el campo 'pointerSource' del GridInteractor. Este queda como opción lista para usar.
    /// </summary>
    [AddComponentMenu(LoopRoutes.GridPointer)]
    public class MouseGridPointerSource : MonoBehaviour, IGridPointerSource
    {
        public bool IsValid => Mouse.current != null;

        public Vector2 ScreenPosition =>
            Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        public bool PrimaryPressedThisFrame =>
            Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        public bool PrimaryReleasedThisFrame =>
            Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;

        public bool PrimaryHeld =>
            Mouse.current != null && Mouse.current.leftButton.isPressed;

        public bool SecondaryPressedThisFrame =>
            Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
    }
}