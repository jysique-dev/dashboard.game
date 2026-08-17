using LoopEngine.CameraEngine.Inputs;
using LoopEngine.CameraEngine.Settings;
using UnityEngine;


namespace LoopEngine.CameraEngine.CameraController
{
    /// <summary>
    /// Controlador de cámara 2.5D en 3ra persona para citybuilder / RTS.
    /// Maneja paneo (plano XZ), zoom, rotación (yaw por teclado y por arrastre)
    /// e inclinación (pitch fijo, dinámico por zoom, o libre por arrastre).
    ///
    /// Jerarquía esperada:
    ///   CameraRig  (este script; su transform es el pivote/punto de foco)
    ///     └── Main Camera  (hijo, asignado en 'cameraTransform')
    ///
    /// Config en CameraSettings, input en InputReader, límite en CameraBounds.
    /// </summary>
    [DisallowMultipleComponent]
    public class CityCameraController : MonoBehaviour
    {
        [Header("Dependencias (assets ScriptableObject)")]
        [SerializeField] private CameraSettings settings;
        [SerializeField] private InputReader input;
        [Tooltip("Límite del área navegable. Opcional: si es null, no se recorta.")]
        [SerializeField] private CameraBounds bounds;

        [Header("Referencias de escena")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Camera targetCamera;

        public CameraBounds Bounds => bounds;

        // --- Estado (objetivo + valor suavizado) ---
        private Vector3 _focusTarget, _focusVelocity;
        private float _yawTarget, _yawCurrent, _yawVelocity;
        private float _pitchTarget, _pitchCurrent, _pitchVelocity;
        private float _zoomTarget, _zoomCurrent, _zoomVelocity;
        private bool _wasRotating;   // para ignorar el frame en que se presiona el botón

        private void Reset()
        {
            if (cameraTransform == null && transform.childCount > 0)
                cameraTransform = transform.GetChild(0);
            if (targetCamera == null)
                targetCamera = GetComponentInChildren<Camera>();
        }

        private void Awake()
        {
            if (targetCamera == null && cameraTransform != null)
                targetCamera = cameraTransform.GetComponent<Camera>();

            _focusTarget = transform.position;
            if (bounds != null) _focusTarget = bounds.Clamp(_focusTarget);

            _yawTarget = _yawCurrent = transform.eulerAngles.y;

            if (settings != null)
            {
                float mid = (settings.minZoom + settings.maxZoom) * 0.5f;
                _zoomTarget = _zoomCurrent = mid;

                // Pitch inicial coherente con el modo elegido.
                _pitchTarget = Mathf.Clamp(settings.pitchAngle, settings.minPitch, settings.maxPitch);
                _pitchCurrent = ResolveTargetPitch();
            }
        }

        private void Update()
        {
            if (settings == null || input == null) return;

            HandlePan();
            HandleRotation();
            HandleZoom();
        }

        private void LateUpdate()
        {
            if (settings == null) return;

            ApplyPivot();
            ApplyCamera();
        }

        // ---------------------------------------------------------------
        // INPUT -> OBJETIVOS
        // ---------------------------------------------------------------

        private void HandlePan()
        {
            Vector2 raw = input.PanInput;

            if (settings.edgeScrollingEnabled)
                raw += GetEdgeScroll();

            if (raw.sqrMagnitude > 1f) raw.Normalize();

            Vector3 dir = Quaternion.Euler(0f, _yawCurrent, 0f) * new Vector3(raw.x, 0f, raw.y);
            _focusTarget += dir * (settings.panSpeed * Time.deltaTime);

            if (bounds != null)
                _focusTarget = bounds.Clamp(_focusTarget);
        }

        private Vector2 GetEdgeScroll()
        {
            Vector2 p = input.PointerPosition;
            Vector2 r = Vector2.zero;
            float b = settings.edgeScrollBorder;

            if (p.x < 0f || p.y < 0f || p.x > Screen.width || p.y > Screen.height)
                return r;

            if (p.x <= b) r.x = -1f;
            else if (p.x >= Screen.width - b) r.x = 1f;
            if (p.y <= b) r.y = -1f;
            else if (p.y >= Screen.height - b) r.y = 1f;
            return r;
        }

        private void HandleRotation()
        {
            // 1) Yaw discreto por teclado/gamepad (Q/E).
            _yawTarget += input.RotateInput * settings.rotationSpeed * Time.deltaTime;

            // 2) Yaw (y pitch) por arrastre mientras se mantiene el botón.
            bool rotating = input.RotateModifier;
            if (rotating && _wasRotating)   // se ignora el frame del click para evitar saltos
            {
                Vector2 d = input.PointerDelta;
                _yawTarget += d.x * settings.dragYawSensitivity;

                if (settings.pitchMode == CameraPitchMode.FreeDrag)
                {
                    float sign = settings.invertDragPitch ? -1f : 1f;
                    _pitchTarget = Mathf.Clamp(
                        _pitchTarget + d.y * sign * settings.dragPitchSensitivity,
                        settings.minPitch, settings.maxPitch);
                }
            }
            _wasRotating = rotating;

            // El delta se consume siempre para que no quede un valor residual.
            input.ClearPointerDelta();
        }

        private void HandleZoom()
        {
            float z = input.ZoomInput;
            if (Mathf.Abs(z) > 0.01f)
            {
                _zoomTarget = Mathf.Clamp(
                    _zoomTarget - Mathf.Sign(z) * settings.zoomSpeed,
                    settings.minZoom, settings.maxZoom);
                input.ClearZoom();
            }
        }

        // ---------------------------------------------------------------
        // OBJETIVOS -> TRANSFORM (con suavizado)
        // ---------------------------------------------------------------

        private void ApplyPivot()
        {
            transform.position = Vector3.SmoothDamp(
                transform.position, _focusTarget, ref _focusVelocity, settings.panSmoothing);

            _yawCurrent = Mathf.SmoothDampAngle(
                _yawCurrent, _yawTarget, ref _yawVelocity, settings.rotationSmoothing);
            transform.rotation = Quaternion.Euler(0f, _yawCurrent, 0f);
        }

        private void ApplyCamera()
        {
            if (cameraTransform == null) return;

            _zoomCurrent = Mathf.SmoothDamp(
                _zoomCurrent, _zoomTarget, ref _zoomVelocity, settings.zoomSmoothing);

            _pitchCurrent = Mathf.SmoothDampAngle(
                _pitchCurrent, ResolveTargetPitch(), ref _pitchVelocity, settings.rotationSmoothing);

            Quaternion localRot = Quaternion.Euler(_pitchCurrent, 0f, 0f);

            bool ortho = targetCamera != null && targetCamera.orthographic;
            float rigDistance = ortho ? Mathf.Max(settings.maxZoom * 2f, _zoomCurrent + 10f) : _zoomCurrent;

            cameraTransform.localPosition = localRot * (Vector3.back * rigDistance);
            cameraTransform.localRotation = localRot;

            if (ortho) targetCamera.orthographicSize = _zoomCurrent;
        }

        /// <summary>Pitch objetivo según el modo configurado.</summary>
        private float ResolveTargetPitch()
        {
            switch (settings.pitchMode)
            {
                case CameraPitchMode.DynamicByZoom:
                    float t = Mathf.InverseLerp(settings.minZoom, settings.maxZoom, _zoomCurrent);
                    return Mathf.Lerp(settings.minPitch, settings.maxPitch, t);

                case CameraPitchMode.FreeDrag:
                    return Mathf.Clamp(_pitchTarget, settings.minPitch, settings.maxPitch);

                default: // Fixed
                    return settings.pitchAngle;
            }
        }

        private void OnDrawGizmos()
        {
            if (bounds == null) return;
            Vector3 c = new Vector3(bounds.center.x, bounds.drawHeight, bounds.center.y);
            Vector3 size = new Vector3(bounds.size.x, 0f, bounds.size.y);
            Gizmos.color = new Color(0.20f, 0.85f, 1f, 0.25f);
            Gizmos.DrawWireCube(c, size);
        }
    }
}