using UnityEngine;
using UnityEngine.InputSystem;

namespace LoopEngine.CameraEngine.Inputs
{
    /// <summary>
    /// Fuente central de input del juego. Envuelve la clase generada
    /// <c>GameControls</c> (creada a partir de GameControls.inputactions con la
    /// opción "Generate C# Class" activada) y expone valores/eventos limpios.
    ///
    /// Patrón reutilizable e interconectable: TODA mecánica futura
    /// (construcción, selección, UI, cámara...) referencia este MISMO asset y
    /// lee de aquí, en lugar de consultar teclado/ratón por su cuenta.
    /// Así el input queda en un solo sitio y es fácil de remapear.
    /// </summary>
    [CreateAssetMenu(fileName = "InputReader", menuName = LoopRoutes.InputReader)]
    public class InputReader : ScriptableObject, GameControls.IGameplayActions
    {
        private GameControls _controls;

        // ---- Valores continuos (se leen por polling cada frame) ----
        public Vector2 PanInput { get; private set; }
        public float ZoomInput { get; private set; }
        public float RotateInput { get; private set; }
        public Vector2 PointerPosition { get; private set; }
        public Vector2 PointerDelta { get; private set; }   // delta del ratón por frame
        public bool RotateModifier { get; private set; }    // botón mantenido para rotar/arrastrar

        private void OnEnable()
        {
            if (_controls == null)
            {
                _controls = new GameControls();
                _controls.Gameplay.SetCallbacks(this);
            }
            _controls.Gameplay.Enable();
        }

        private void OnDisable()
        {
            if (_controls != null)
                _controls.Gameplay.Disable();
        }

        /// <summary>El zoom es un impulso (rueda): quien lo consume lo limpia.</summary>
        public void ClearZoom() => ZoomInput = 0f;

        /// <summary>
        /// El delta del puntero es por-frame; quien lo consume lo limpia para
        /// evitar que un valor residual siga rotando la cámara sin mover el ratón.
        /// </summary>
        public void ClearPointerDelta() => PointerDelta = Vector2.zero;

        // ---- Callbacks generados por el Input System ----
        public void OnPan(InputAction.CallbackContext context)
            => PanInput = context.ReadValue<Vector2>();

        public void OnZoom(InputAction.CallbackContext context)
            => ZoomInput = context.ReadValue<float>();

        public void OnRotateCamera(InputAction.CallbackContext context)
            => RotateInput = context.ReadValue<float>();

        public void OnPointerPosition(InputAction.CallbackContext context)
            => PointerPosition = context.ReadValue<Vector2>();

        public void OnPointerDelta(InputAction.CallbackContext context)
            => PointerDelta = context.ReadValue<Vector2>();

        public void OnRotateModifier(InputAction.CallbackContext context)
            => RotateModifier = context.ReadValueAsButton();
    }
}