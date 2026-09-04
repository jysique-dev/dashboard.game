using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LoopEngine.IsoCamera.InputAbstraction
{
    /// <summary>
    /// Punto de acceso único al input de la cámara.
    ///
    /// Unity define ENABLE_INPUT_SYSTEM cuando el backend nuevo está activo y
    /// ENABLE_LEGACY_INPUT_MANAGER cuando lo está el viejo. Con "Active Input Handling = Both"
    /// ambos símbolos existen a la vez, por eso el selector es explícito y no un else.
    /// Referencia: Input System - Installation guide (Unity).
    /// </summary>
    public static class IsoInput
    {
        public enum Backend { Auto, Legacy, InputSystem }

        private static IIsoInputSource current;
        private static Backend preferred = Backend.Auto;

        /// <summary>
        /// Factor de calibración de la rueda, común a ambos backends.
        /// Cada backend normaliza a su manera antes de aplicarlo; ajústalo si en tu
        /// plataforma una muesca no produce un valor cercano a 1.
        /// </summary>
        public static float ScrollScale = 1f;

        /// <summary>Fuente activa. Nunca es null.</summary>
        public static IIsoInputSource Source => current ??= Create(preferred);

        /// <summary>Sustituye la fuente por una propia (bindings custom, replays, tests).</summary>
        public static void Override(IIsoInputSource source) => current = source ?? NullIsoInputSource.Instance;

        /// <summary>Fuerza un backend concreto. Útil cuando "Both" está activo.</summary>
        public static void SetBackend(Backend backend)
        {
            preferred = backend;
            current = Create(backend);
        }

        private static IIsoInputSource Create(Backend backend)
        {
#if ENABLE_INPUT_SYSTEM
            if (backend == Backend.Auto || backend == Backend.InputSystem)
                return new InputSystemIsoInputSource();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (backend == Backend.Auto || backend == Backend.Legacy)
                return new LegacyIsoInputSource();
#endif
            Debug.LogWarning("[IsoCamera] Ningún backend de input disponible. Usando fuente nula.");
            return NullIsoInputSource.Instance;
        }
    }

#if ENABLE_LEGACY_INPUT_MANAGER
    /// <summary>Implementación sobre UnityEngine.Input (Input Manager clásico).</summary>
    public sealed class LegacyIsoInputSource : IIsoInputSource
    {
        public Vector2 Move => new Vector2(
            UnityEngine.Input.GetAxisRaw("Horizontal"),
            UnityEngine.Input.GetAxisRaw("Vertical"));

        // mouseScrollDelta ya viene normalizado a ±1 por muesca.
        public float Zoom => UnityEngine.Input.mouseScrollDelta.y * IsoInput.ScrollScale;

        public int RotateStep
        {
            get
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.E)) return 1;
                if (UnityEngine.Input.GetKeyDown(KeyCode.Q)) return -1;
                return 0;
            }
        }

        public Vector2 PointerPosition => UnityEngine.Input.mousePosition;
        public bool PanHeld => UnityEngine.Input.GetMouseButton(2);

        // Los ejes "Mouse X/Y" NO están en píxeles: vienen escalados por la sensibilidad
        // del Input Manager. Por eso la sesión 4 expondrá un factor de calibración.
        public Vector2 PanDelta => new Vector2(
            UnityEngine.Input.GetAxis("Mouse X"),
            UnityEngine.Input.GetAxis("Mouse Y"));
    }
#endif

#if ENABLE_INPUT_SYSTEM
    /// <summary>
    /// Implementación sobre el paquete Input System usando las clases de dispositivo
    /// (Keyboard.current, Mouse.current). No requiere ningún asset .inputactions,
    /// lo que mantiene el paquete de cámara sin dependencias de configuración.
    /// </summary>
    public sealed class InputSystemIsoInputSource : IIsoInputSource
    {
        /// <summary>
        /// El Input System entrega el scroll sin normalizar; en Windows suele ser ±120 por muesca.
        /// Este es el normalizador base del backend; encima se aplica IsoInput.ScrollScale.
        /// </summary>
        public static float ScrollNormalizer = 1f / 120f;

        public Vector2 Move
        {
            get
            {
                Vector2 v = Vector2.zero;
                var k = Keyboard.current;
                if (k != null)
                {
                    if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1f;
                    if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1f;
                    if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1f;
                    if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1f;
                }
                var g = Gamepad.current;
                if (g != null && v.sqrMagnitude < 0.01f) v = g.leftStick.ReadValue();
                return v;
            }
        }

        public float Zoom
        {
            get
            {
                var m = Mouse.current;
                if (m == null) return 0f;
                float raw = m.scroll.ReadValue().y;
                return Mathf.Clamp(raw * ScrollNormalizer * IsoInput.ScrollScale, -3f, 3f);
            }
        }

        public int RotateStep
        {
            get
            {
                var k = Keyboard.current;
                if (k == null) return 0;
                if (k.eKey.wasPressedThisFrame) return 1;
                if (k.qKey.wasPressedThisFrame) return -1;
                return 0;
            }
        }

        public Vector2 PointerPosition => Mouse.current?.position.ReadValue() ?? Vector2.zero;
        public bool PanHeld => Mouse.current?.middleButton.isPressed ?? false;

        // Aquí sí está en píxeles reales, a diferencia del legacy.
        public Vector2 PanDelta => Mouse.current?.delta.ReadValue() ?? Vector2.zero;
    }
#endif
}