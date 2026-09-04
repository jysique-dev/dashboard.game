using LoopEngine.IsoCamera.Core;
using UnityEngine;

namespace LoopEngine.IsoCamera.Core
{
    /// <summary>
    /// Mueve el punto de foco del rig para seguir a un ICameraTarget.
    ///
    /// La dead zone se expresa en unidades de MUNDO sobre la base planar de la cámara
    /// (PlanarRight / PlanarForward), no en píxeles. Así sigue siendo correcta cuando
    /// el yaw rota (sesión 3) y no depende de la resolución de pantalla.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(IsometricCameraRig))]
    [AddComponentMenu("Iso Camera/Isometric Camera Follow")]
    public sealed class IsometricCameraFollow : MonoBehaviour
    {
        public enum UpdatePhase
        {
            /// <summary>Por defecto. El objetivo se mueve en Update.</summary>
            LateUpdate,
            /// <summary>El objetivo es un Rigidbody movido en FixedUpdate.</summary>
            FixedUpdate
        }

        [Header("Objetivo")]
        [Tooltip("Transform, GameObject o Component. Si implementa ICameraTarget se usa directo; " +
                 "si no, se envuelve automáticamente en un TransformCameraTarget.")]
        [SerializeField] private Object targetObject;
        [SerializeField] private Vector3 worldOffset = Vector3.zero;

        [Header("Dead zone (unidades de mundo)")]
        [Tooltip("Ancho (a lo largo de PlanarRight) y profundidad (a lo largo de PlanarForward). " +
                 "Cero = la cámara persigue siempre.")]
        [SerializeField] private Vector2 deadZoneSize = new Vector2(3f, 2f);

        [Header("Suavizado")]
        [Tooltip("Tiempo aproximado para alcanzar el objetivo. 0 = instantáneo.")]
        [SerializeField, Min(0f)] private float smoothTime = 0.18f;
        [Tooltip("Velocidad máxima del foco en unidades/segundo. 0 = sin límite.")]
        [SerializeField, Min(0f)] private float maxSpeed = 0f;

        [Header("Vertical")]
        [Tooltip("Desactivado, el foco mantiene su altura: el personaje puede saltar sin que la cámara rebote.")]
        [SerializeField] private bool followVertical = false;
        [SerializeField, Min(0f)] private float verticalSmoothTime = 0.6f;

        [Header("Límites")]
        [SerializeField] private Object boundsObject;
        [Tooltip("Activado, se confina el área visible completa. Desactivado, solo el punto de foco.")]
        [SerializeField] private bool clampVisibleArea = true;

        [Header("Ejecución")]
        [SerializeField] private UpdatePhase updatePhase = UpdatePhase.LateUpdate;

        [Header("Gizmos")]
        [SerializeField] private bool drawGizmos = true;

        private IsometricCameraRig rig;
        private ICameraTarget target;
        private ICameraBounds bounds;
        private Vector3 velocity;
        private float verticalVelocity;

        // ---------------------------------------------------------------- API

        /// <summary>Objetivo actual. Asignable en runtime sin pasar por el Inspector.</summary>
        public ICameraTarget Target
        {
            get => target;
            set { target = value; targetObject = value as Object; }
        }

        public ICameraBounds Bounds
        {
            get => bounds;
            set { bounds = value; boundsObject = value as Object; }
        }

        public Vector3 WorldOffset { get => worldOffset; set => worldOffset = value; }
        public Vector2 DeadZoneSize { get => deadZoneSize; set => deadZoneSize = value; }
        public float SmoothTime { get => smoothTime; set => smoothTime = Mathf.Max(0f, value); }

        /// <summary>True si el objetivo está dentro de la dead zone (la cámara está quieta).</summary>
        public bool TargetInsideDeadZone { get; private set; }

        /// <summary>
        /// Mientras esté en true, el follow no toca el foco. Lo usan el paneo y el
        /// enfoque para tomar el control temporalmente sin desactivar el componente
        /// (desactivarlo perdería el estado del suavizado y provocaría un tirón al volver).
        /// </summary>
        public bool Suspended { get; set; }

        /// <summary>Aplica el bloque de follow de un IsometricCameraSettings.</summary>
        public void Apply(IsometricCameraSettings.FollowSettings s)
        {
            if (s == null) return;
            worldOffset = s.worldOffset;
            deadZoneSize = s.deadZoneSize;
            smoothTime = s.smoothTime;
            maxSpeed = s.maxSpeed;
            followVertical = s.followVertical;
            verticalSmoothTime = s.verticalSmoothTime;
            clampVisibleArea = s.clampVisibleArea;
        }

        /// <summary>Coloca el foco sobre el objetivo de inmediato. Úsalo tras teletransportes o al cargar escena.</summary>
        public void SnapToTarget()
        {
            if (!EnsureRefs() || target == null || !target.IsValid) return;
            velocity = Vector3.zero;
            verticalVelocity = 0f;

            Vector3 desired = target.Position + worldOffset;
            if (!followVertical) desired.y = rig.FocusPoint.y;
            rig.FocusPoint = ApplyBounds(desired);
        }

        public void SetTarget(Transform t) => Target = t != null ? new TransformCameraTarget(t) : null;

        // ----------------------------------------------------------- Unity

        private void OnEnable() { EnsureRefs(); SnapToTarget(); }
        private void OnValidate() { rig = null; target = null; bounds = null; }

        private void LateUpdate()
        {
            if (updatePhase == UpdatePhase.LateUpdate) Tick(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (updatePhase == UpdatePhase.FixedUpdate) Tick(Time.fixedDeltaTime);
        }

        // ----------------------------------------------------------- Núcleo

        private void Tick(float dt)
        {
            if (Suspended || dt <= 0f || !EnsureRefs()) return;
            if (target == null || !target.IsValid) return;

            Vector3 focus = rig.FocusPoint;
            Vector3 desired = target.Position + worldOffset;

            // 1. Descomponer el error en la base planar de la cámara.
            Vector3 delta = desired - focus;
            Vector3 right = rig.PlanarRight;
            Vector3 forward = rig.PlanarForward;

            float dr = Vector3.Dot(delta, right);
            float df = Vector3.Dot(delta, forward);

            // 2. Recortar la dead zone. Lo que queda es cuánto debe moverse la cámara.
            float cr = SubtractDeadZone(dr, deadZoneSize.x * 0.5f);
            float cf = SubtractDeadZone(df, deadZoneSize.y * 0.5f);
            TargetInsideDeadZone = Mathf.Approximately(cr, 0f) && Mathf.Approximately(cf, 0f);

            Vector3 planarGoal = focus + right * cr + forward * cf;

            // 3. Vertical, con su propio suavizado (normalmente más lento).
            float goalY = followVertical ? desired.y : focus.y;
            planarGoal.y = focus.y;

            // 4. Damping horizontal.
            float speedCap = maxSpeed <= 0f ? Mathf.Infinity : maxSpeed;
            Vector3 next = smoothTime <= 0f
                ? planarGoal
                : Vector3.SmoothDamp(focus, planarGoal, ref velocity, smoothTime, speedCap, dt);

            // 5. Damping vertical, independiente.
            next.y = (!followVertical || verticalSmoothTime <= 0f)
                ? goalY
                : Mathf.SmoothDamp(focus.y, goalY, ref verticalVelocity, verticalSmoothTime, Mathf.Infinity, dt);

            // 6. Confinamiento.
            rig.FocusPoint = ApplyBounds(next);
        }

        /// <summary>
        /// Devuelve 0 si el error cabe dentro de la mitad de la dead zone;
        /// si no, el exceso conservando el signo. Esto es lo que hace que la cámara
        /// "empuje" solo cuando el objetivo toca el borde de la zona.
        /// </summary>
        private static float SubtractDeadZone(float value, float half)
        {
            if (half <= 0f) return value;
            if (value > half) return value - half;
            if (value < -half) return value + half;
            return 0f;
        }

        private Vector3 ApplyBounds(Vector3 focus)
        {
            if (bounds == null) return focus;
            Vector3 ext = clampVisibleArea ? rig.GroundViewAabbExtents : Vector3.zero;
            return bounds.Clamp(focus, ext);
        }

        private bool EnsureRefs()
        {
            if (rig == null) rig = GetComponent<IsometricCameraRig>();
            if (target == null) target = CameraTargetResolver.Resolve(targetObject);
            if (bounds == null) bounds = CameraBoundsResolver.Resolve(boundsObject);
            return rig != null;
        }

        // ----------------------------------------------------------- Gizmos

        private void OnDrawGizmos()
        {
            if (!drawGizmos || !EnsureRefs()) return;

            Vector3 c = rig.FocusPoint;
            Vector3 r = rig.PlanarRight * (deadZoneSize.x * 0.5f);
            Vector3 f = rig.PlanarForward * (deadZoneSize.y * 0.5f);

            Gizmos.color = TargetInsideDeadZone
                ? new Color(0.2f, 1f, 0.3f, 0.9f)
                : new Color(1f, 0.85f, 0.1f, 0.9f);

            Gizmos.DrawLine(c - r - f, c + r - f);
            Gizmos.DrawLine(c + r - f, c + r + f);
            Gizmos.DrawLine(c + r + f, c - r + f);
            Gizmos.DrawLine(c - r + f, c - r - f);

            if (target != null && target.IsValid)
            {
                Vector3 p = target.Position + worldOffset;
                Gizmos.color = new Color(1f, 0.3f, 0.8f, 0.9f);
                Gizmos.DrawWireSphere(p, 0.25f);
                Gizmos.DrawLine(c, p);
            }
        }
    }
}