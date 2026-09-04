using LoopEngine.IsoCamera.Occlusion;
using UnityEngine;

namespace LoopEngine.IsoCamera.Core
{
    /// <summary>
    /// Punto de entrada único del sistema. El resto de tu juego habla solo con esta
    /// clase; los componentes internos (rig, follow, zoom, rotador, paneo, oclusión)
    /// son detalles de implementación.
    ///
    /// Ventaja concreta: si mañana cambias cómo se hace el zoom o sustituyes el
    /// rotador, tu código de juego no se entera. Sin fachada, cada sistema que
    /// necesite mover la cámara acaba referenciando cuatro componentes distintos.
    ///
    /// Crea automáticamente los subcomponentes que la configuración pida.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(IsometricCameraRig))]
    [AddComponentMenu("Iso Camera/Isometric Camera Controller")]
    public sealed class IsometricCameraController : MonoBehaviour
    {
        [Header("Configuración")]
        [Tooltip("Si se deja vacío, cada componente usa sus propios valores del Inspector.")]
        [SerializeField] private IsometricCameraSettings settings;
        [SerializeField] private bool applyOnAwake = true;

        [Header("Escena")]
        [SerializeField] private Object initialTarget;
        [SerializeField] private Object boundsObject;

        [Header("Acceso global")]
        [Tooltip("Registra esta instancia en IsometricCameraController.Main. " +
                 "Cómodo para prototipos; desactívalo si prefieres inyección de dependencias.")]
        [SerializeField] private bool registerAsMain = true;

        private IsometricCameraRig rig;
        private IsometricCameraFollow follow;
        private IsometricCameraZoom zoom;
        private IsometricCameraRotator rotator;
        private IsometricCameraPan pan;
        private IsometricCameraFocus focus;
        private IsometricCameraOcclusion occlusion;

        /// <summary>Última instancia registrada. Puede ser null; comprueba antes de usar.</summary>
        public static IsometricCameraController Main { get; private set; }

        // ---------------------------------------------------------------- API

        public IsometricCameraRig Rig => rig;
        public Camera Camera => rig != null ? rig.Camera : null;

        /// <summary>Punto del mundo al que mira la cámara.</summary>
        public Vector3 FocusPoint
        {
            get => rig != null ? rig.FocusPoint : Vector3.zero;
            set { if (rig != null) rig.FocusPoint = value; }
        }

        /// <summary>Zoom normalizado. 0 = lo más cerca, 1 = lo más lejos.</summary>
        public float Zoom01
        {
            get => zoom != null ? zoom.Zoom01 : 0f;
            set { if (zoom != null) zoom.TargetZoom01 = value; }
        }

        public float Yaw => rig != null ? rig.Yaw : 0f;

        /// <summary>Base planar para convertir input de movimiento a dirección de mundo.</summary>
        public Vector3 PlanarForward => rig != null ? rig.PlanarForward : Vector3.forward;
        public Vector3 PlanarRight => rig != null ? rig.PlanarRight : Vector3.right;

        // --- Objetivo ---

        public void SetTarget(Transform target)
        {
            if (follow == null) return;
            follow.SetTarget(target);
            follow.SnapToTarget();
        }

        public void SetTarget(ICameraTarget target)
        {
            if (follow == null) return;
            follow.Target = target;
            follow.SnapToTarget();
        }

        /// <summary>Coloca la cámara sobre su objetivo sin transición. Úsalo tras teletransportes.</summary>
        public void Snap() => follow?.SnapToTarget();

        // --- Movimiento ---

        /// <summary>Gira n pasos discretos. Positivo = horario visto desde arriba.</summary>
        public void RotateSteps(int steps) => rotator?.RotateSteps(steps);

        public void RotateTo(float yaw, bool instant = false) => rotator?.RotateTo(yaw, instant);

        /// <summary>Desplaza el foco en la base planar. x = derecha en pantalla, y = hacia el fondo.</summary>
        public void PanBy(Vector2 planarDelta) => pan?.PanBy(planarDelta);

        public void FocusOn(Vector3 point, float duration = -1f) => focus?.FocusOn(point, duration);
        public void FocusOn(Transform target, float duration = -1f) => focus?.FocusOn(target, duration);
        public void CancelFocus() => focus?.Cancel();

        /// <summary>Congela el seguimiento sin perder el estado del suavizado.</summary>
        public void SuspendFollow(bool suspended) { if (follow != null) follow.Suspended = suspended; }

        // --- Utilidades ---

        /// <summary>Proyecta un punto de pantalla sobre el plano del suelo. Útil para selección con ratón.</summary>
        public bool ScreenPointToGround(Vector2 screenPosition, out Vector3 world) =>
            rig != null ? rig.ScreenPointToGround(screenPosition, out world) : Fail(out world);

        private static bool Fail(out Vector3 world) { world = Vector3.zero; return false; }

        // --- Configuración ---

        /// <summary>Cambia el perfil de cámara en caliente (p. ej. exploración -> combate).</summary>
        public void SetSettings(IsometricCameraSettings value)
        {
            settings = value;
            ApplySettings();
        }

        /// <summary>Reaplica el asset actual a todos los subcomponentes.</summary>
        public void ApplySettings()
        {
            if (settings == null) return;
            EnsureComponents();

            rig.Apply(settings.Angles);

            SetActive(zoom, settings.Zoom.Enabled);
            if (zoom != null) zoom.Apply(settings.Zoom);

            SetActive(rotator, settings.Rotation.Enabled);
            if (rotator != null) rotator.Apply(settings.Rotation);

            SetActive(follow, settings.Follow.Enabled);
            if (follow != null) follow.Apply(settings.Follow);

            SetActive(pan, settings.Pan.Enabled);
            if (pan != null) pan.Apply(settings.Pan);

            SetActive(occlusion, settings.Occlusion.Enabled);
            if (occlusion != null) occlusion.Apply(settings.Occlusion);
        }

        /// <summary>Crea el rig completo por código, sin prefab.</summary>
        public static IsometricCameraController Create(IsometricCameraSettings settings,
                                                       string objectName = "IsoCamera")
        {
            var go = new GameObject(objectName);
            var controller = go.AddComponent<IsometricCameraController>();
            controller.settings = settings;
            controller.EnsureComponents();
            controller.ApplySettings();
            return controller;
        }

        // ----------------------------------------------------------- Unity

        private void Awake()
        {
            EnsureComponents();
            if (applyOnAwake) ApplySettings();

            if (initialTarget != null && follow != null)
                follow.Target = CameraTargetResolver.Resolve(initialTarget);

            if (registerAsMain) Main = this;
        }

        private void Start() => follow?.SnapToTarget();

        private void OnDestroy() { if (Main == this) Main = null; }

        // ----------------------------------------------------------- Interno

        private void EnsureComponents()
        {
            if (rig == null) rig = GetComponent<IsometricCameraRig>();
            if (rig == null) rig = gameObject.AddComponent<IsometricCameraRig>();

            focus = GetOrAdd(focus);
            follow = GetOrAdd(follow);

            bool wantsZoom = settings == null || settings.Zoom.Enabled;
            bool wantsRotation = settings == null || settings.Rotation.Enabled;
            bool wantsPan = settings != null && settings.Pan.Enabled;
            bool wantsOcclusion = settings != null && settings.Occlusion.Enabled;

            if (wantsZoom) zoom = GetOrAdd(zoom); else zoom = GetComponent<IsometricCameraZoom>();
            if (wantsRotation) rotator = GetOrAdd(rotator); else rotator = GetComponent<IsometricCameraRotator>();
            if (wantsPan) pan = GetOrAdd(pan); else pan = GetComponent<IsometricCameraPan>();
            if (wantsOcclusion) occlusion = GetOrAdd(occlusion); else occlusion = GetComponent<IsometricCameraOcclusion>();
        }

        private T GetOrAdd<T>(T cached) where T : Component
        {
            if (cached != null) return cached;
            T found = GetComponent<T>();
            return found != null ? found : gameObject.AddComponent<T>();
        }

        private static void SetActive(Behaviour b, bool active)
        {
            if (b != null) b.enabled = active;
        }
    }
}