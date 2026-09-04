using LoopEngine.IsoCamera.InputAbstraction;
using UnityEngine;

namespace LoopEngine.IsoCamera.Core
{
    /// <summary>
    /// Desplazamiento manual del foco: por borde de pantalla y por arrastre del puntero.
    ///
    /// El arrastre es exacto, no proporcional: se memoriza el punto del suelo que había
    /// bajo el cursor al empezar y cada frame se desplaza el foco para que ese punto
    /// siga justo ahí. Un enfoque basado en "delta de píxeles × sensibilidad" derivaría
    /// respecto al cursor y obligaría a recalibrar en cada nivel de zoom.
    ///
    /// Ejecuta antes que el follow (orden -100).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(IsometricCameraRig))]
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("Iso Camera/Isometric Camera Pan")]
    public sealed class IsometricCameraPan : MonoBehaviour
    {
        [Header("Paneo por borde")]
        [SerializeField] private bool edgePan = true;
        [Tooltip("Grosor de la banda sensible, en píxeles.")]
        [SerializeField, Min(1f)] private float edgeThickness = 24f;
        [Tooltip("Velocidad en unidades/segundo cuando orthographicSize vale Reference Size.")]
        [SerializeField, Min(0f)] private float edgeSpeed = 14f;
        [Tooltip("Tamaño de referencia. La velocidad escala con el zoom para que el " +
                 "recorrido en pantalla se sienta igual de rápido siempre.")]
        [SerializeField, Min(0.1f)] private float referenceSize = 10f;
        [Tooltip("Ignora el borde si el cursor está fuera de la ventana.")]
        [SerializeField] private bool ignoreOutsideWindow = true;

        [Header("Paneo por arrastre")]
        [SerializeField] private bool dragPan = true;
        [Tooltip("Activado, el mundo se mueve con el cursor. Desactivado, la cámara se mueve con él.")]
        [SerializeField] private bool grabWorld = true;

        [Header("Integración")]
        [Tooltip("Suspende el follow mientras se panea, y lo reanuda al soltar.")]
        [SerializeField] private bool suspendFollowWhilePanning = true;
        [SerializeField] private Object boundsObject;

        private IsometricCameraRig rig;
        private IsometricCameraFollow follow;
        private ICameraBounds bounds;

        private bool dragging;
        private Vector3 grabPoint;

        // ---------------------------------------------------------------- API

        /// <summary>True mientras hay paneo activo por cualquiera de los dos métodos.</summary>
        public bool IsPanning { get; private set; }

        public bool EdgePanEnabled { get => edgePan; set => edgePan = value; }
        public bool DragPanEnabled { get => dragPan; set => dragPan = value; }

        /// <summary>Aplica el bloque de paneo de un IsometricCameraSettings.</summary>
        public void Apply(IsometricCameraSettings.PanSettings s)
        {
            if (s == null) return;
            edgePan = s.edgePan;
            edgeThickness = s.edgeThickness;
            edgeSpeed = s.edgeSpeed;
            referenceSize = s.referenceSize;
            dragPan = s.dragPan;
            grabWorld = s.grabWorld;
            suspendFollowWhilePanning = s.suspendFollowWhilePanning;
        }

        /// <summary>Desplaza el foco en la base planar de la cámara. x = derecha, y = adelante.</summary>
        public void PanBy(Vector2 planarDelta)
        {
            if (rig == null) return;
            Vector3 move = rig.PlanarRight * planarDelta.x + rig.PlanarForward * planarDelta.y;
            rig.FocusPoint = ApplyBounds(rig.FocusPoint + move);
        }

        // ----------------------------------------------------------- Unity

        private void Awake()
        {
            rig = GetComponent<IsometricCameraRig>();
            follow = GetComponent<IsometricCameraFollow>();
            bounds = CameraBoundsResolver.Resolve(boundsObject);
        }

        private void OnDisable() => EndPan();

        private void LateUpdate()
        {
            if (rig == null) return;

            bool wasPanning = IsPanning;
            IsPanning = false;

            if (dragPan) HandleDrag();
            if (edgePan && !dragging) HandleEdge();

            if (wasPanning && !IsPanning) EndPan();
        }

        // ----------------------------------------------------------- Arrastre

        private void HandleDrag()
        {
            var input = IsoInput.Source;
            Vector2 pointer = input.PointerPosition;

            if (!input.PanHeld)
            {
                dragging = false;
                return;
            }

            if (!dragging)
            {
                if (!rig.ScreenPointToGround(pointer, out grabPoint)) return;
                dragging = true;
                BeginPan();
            }

            if (!rig.ScreenPointToGround(pointer, out Vector3 currentPoint)) return;

            // El punto agarrado no se recalcula: tras mover el foco vuelve a caer
            // exactamente bajo el cursor, que es justo el invariante que buscamos.
            Vector3 delta = grabPoint - currentPoint;
            if (!grabWorld) delta = -delta;

            rig.FocusPoint = ApplyBounds(rig.FocusPoint + delta);
            IsPanning = true;
        }

        // ----------------------------------------------------------- Borde

        private void HandleEdge()
        {
            Vector2 p = IsoInput.Source.PointerPosition;

            if (ignoreOutsideWindow &&
                (p.x < 0f || p.y < 0f || p.x > Screen.width || p.y > Screen.height))
                return;

            Vector2 dir = Vector2.zero;
            if (p.x <= edgeThickness) dir.x -= 1f;
            if (p.x >= Screen.width - edgeThickness) dir.x += 1f;
            if (p.y <= edgeThickness) dir.y -= 1f;
            if (p.y >= Screen.height - edgeThickness) dir.y += 1f;

            if (dir.sqrMagnitude < 0.0001f) return;
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            BeginPan();
            float zoomFactor = rig.OrthographicSize / Mathf.Max(referenceSize, 0.001f);
            PanBy(dir * (edgeSpeed * zoomFactor * Time.deltaTime));
            IsPanning = true;
        }

        // ----------------------------------------------------------- Interno

        private void BeginPan()
        {
            if (suspendFollowWhilePanning && follow != null) follow.Suspended = true;
        }

        private void EndPan()
        {
            dragging = false;
            IsPanning = false;
            if (suspendFollowWhilePanning && follow != null) follow.Suspended = false;
        }

        private Vector3 ApplyBounds(Vector3 focus)
        {
            if (bounds == null) return focus;
            return bounds.Clamp(focus, rig.GroundViewAabbExtents);
        }
    }
}