using LoopEngine.IsoCamera.Core;
using UnityEngine;

namespace LoopEngine.IsoCamera.Core
{
    /// <summary>
    /// Rig de cámara isométrica.
    ///
    /// Jerarquía que construye y mantiene automáticamente:
    ///   [este GameObject]  -> Pivot. Su POSICIÓN es el punto de foco en el mundo.
    ///     └─ Yaw           -> rotación Y (giro alrededor del pivot)
    ///          └─ Pitch    -> rotación X (inclinación)
    ///               └─ Camera -> desplazada -Z * distance, proyección ortográfica
    ///
    /// Nunca se rota ni se mueve la cámara directamente: todo se hace sobre los nodos.
    /// Así, mover el pivot = mover el foco, y rotar Yaw = orbitar, sin acoplar
    /// posición y orientación en un mismo transform.
    ///
    /// No tiene dependencias externas (sin Cinemachine, sin Input System).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Iso Camera/Isometric Camera Rig")]
    public sealed class IsometricCameraRig : MonoBehaviour
    {
        public const string YawNodeName = "Yaw";
        public const string PitchNodeName = "Pitch";
        public const string CameraNodeName = "Camera";

        [Header("Ángulos")]
        [SerializeField] private IsometricAnglePreset preset = IsometricAnglePreset.Dimetric2To1;
        [SerializeField, Range(0.1f, 89.9f)] private float pitch = IsometricAngles.Dimetric2To1Pitch;
        [SerializeField, Range(-180f, 180f)] private float yaw = IsometricAngles.DefaultYaw;

        [Header("Lente")]
        [Tooltip("Media altura del volumen visible, en unidades de mundo. Es el control de zoom.")]
        [SerializeField, Min(0.01f)] private float orthographicSize = 8f;
        [Tooltip("Distancia del pivot a la cámara. No afecta al encuadre en ortográfico, " +
                 "pero sí al culling, a las sombras y a los materiales que dependen de la posición de cámara.")]
        [SerializeField, Min(0.1f)] private float distance = 40f;
        [SerializeField, Min(0.01f)] private float nearClip = 0.3f;
        [Tooltip("Margen que se suma a 'distance' para calcular el far clip.")]
        [SerializeField, Min(1f)] private float farClipPadding = 100f;

        [Header("Gizmos")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField, Min(1f)] private float gizmoGroundSize = 20f;

        private Transform yawNode;
        private Transform pitchNode;
        private Camera cam;
        private bool dirty = true;

        // ---------------------------------------------------------------- API

        /// <summary>Punto del mundo al que mira el rig. Escribirlo mueve el pivot.</summary>
        public Vector3 FocusPoint
        {
            get => transform.position;
            set => transform.position = value;
        }

        public float Pitch
        {
            get => pitch;
            set { pitch = Mathf.Clamp(value, 0.1f, 89.9f); preset = IsometricAnglePreset.Custom; MarkDirty(); }
        }

        public float Yaw
        {
            get => yaw;
            set { yaw = Mathf.DeltaAngle(0f, value); MarkDirty(); }
        }

        /// <summary>Zoom. Media altura visible en unidades de mundo.</summary>
        public float OrthographicSize
        {
            get => orthographicSize;
            set { orthographicSize = Mathf.Max(0.01f, value); MarkDirty(); }
        }

        public float Distance
        {
            get => distance;
            set { distance = Mathf.Max(0.1f, value); MarkDirty(); }
        }

        /// <summary>Cámara controlada por el rig (se crea si no existe).</summary>
        public Camera Camera { get { EnsureHierarchy(); return cam; } }

        /// <summary>Nodo de yaw. Base de referencia para convertir input a movimiento de mundo.</summary>
        public Transform YawTransform { get { EnsureHierarchy(); return yawNode; } }

        /// <summary>Adelante de la cámara proyectado sobre el plano XZ (normalizado).</summary>
        public Vector3 PlanarForward
        {
            get
            {
                float r = yaw * Mathf.Deg2Rad;
                return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
            }
        }

        /// <summary>Derecha de la cámara proyectada sobre el plano XZ (normalizado).</summary>
        public Vector3 PlanarRight
        {
            get
            {
                float r = yaw * Mathf.Deg2Rad;
                return new Vector3(Mathf.Cos(r), 0f, -Mathf.Sin(r));
            }
        }

        /// <summary>Relación ancho:alto con la que se proyecta una baldosa cuadrada. Solo lectura, para verificar.</summary>
        public float TileAspectRatio => IsometricAngles.TileAspectRatio(pitch);

        /// <summary>
        /// Semiextensiones de la huella visible sobre el plano del suelo, expresadas en la
        /// base planar de la cámara: x = a lo largo de PlanarRight, y = a lo largo de PlanarForward.
        /// El ancho es orthographicSize * aspect; la profundidad se estira por 1/sin(pitch)
        /// porque la cámara mira el suelo en oblicuo.
        /// </summary>
        public Vector2 GroundViewHalfExtents
        {
            get
            {
                EnsureHierarchy();
                float halfH = orthographicSize;
                float aspect = (cam != null && cam.aspect > 0.01f) ? cam.aspect : 16f / 9f;
                float depth = halfH / Mathf.Max(Mathf.Sin(pitch * Mathf.Deg2Rad), 0.0001f);
                return new Vector2(halfH * aspect, depth);
            }
        }

        /// <summary>
        /// AABB en mundo (semiextensiones X/Z) que envuelve la huella visible ya rotada por el yaw.
        /// Para un rectángulo rotado, la semiextensión en un eje es la suma de las proyecciones
        /// absolutas de sus dos semiejes sobre ese eje. Y siempre es 0.
        /// </summary>
        public Vector3 GroundViewAabbExtents
        {
            get
            {
                Vector2 h = GroundViewHalfExtents;
                Vector3 r = PlanarRight * h.x;
                Vector3 f = PlanarForward * h.y;
                return new Vector3(Mathf.Abs(r.x) + Mathf.Abs(f.x), 0f, Mathf.Abs(r.z) + Mathf.Abs(f.z));
            }
        }

        public void ApplyPreset(IsometricAnglePreset value)
        {
            preset = value;
            if (IsometricAngles.TryResolve(preset, out float p, out float y)) { pitch = p; yaw = y; }
            MarkDirty();
        }

        /// <summary>Fuerza reconstrucción de jerarquía y reaplicación de parámetros.</summary>
        [ContextMenu("Rebuild Rig")]
        public void Rebuild()
        {
            yawNode = pitchNode = null;
            cam = null;
            dirty = true;
            EnsureHierarchy();
            Apply();
        }

        /// <summary>Aplica el bloque de ángulos de un IsometricCameraSettings.</summary>
        public void Apply(IsometricCameraSettings.AngleSettings s)
        {
            if (s == null) return;
            preset = s.preset;
            pitch = Mathf.Clamp(s.pitch, 0.1f, 89.9f);
            yaw = s.yaw;
            distance = Mathf.Max(0.1f, s.distance);
            MarkDirty();
        }

        public void MarkDirty() => dirty = true;

        /// <summary>
        /// Proyecta un punto de pantalla sobre un plano horizontal del mundo.
        /// Funciona igual con cámara ortográfica: ScreenPointToRay devuelve rayos
        /// paralelos en vez de divergentes, y la intersección con el plano es la misma operación.
        /// </summary>
        /// <param name="screenPosition">Píxeles de pantalla.</param>
        /// <param name="world">Punto resultante en mundo.</param>
        /// <param name="groundY">Altura del plano. Por defecto, la del pivot.</param>
        /// <returns>False si el rayo es paralelo al plano (pitch = 0).</returns>
        public bool ScreenPointToGround(Vector2 screenPosition, out Vector3 world, float groundY = float.NaN)
        {
            EnsureHierarchy();
            world = Vector3.zero;
            if (cam == null) return false;

            float y = float.IsNaN(groundY) ? transform.position.y : groundY;
            var plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
            Ray ray = cam.ScreenPointToRay(screenPosition);

            if (!plane.Raycast(ray, out float enter)) return false;
            world = ray.GetPoint(enter);
            return true;
        }

        // ----------------------------------------------------------- Unity

        private void OnEnable() { dirty = true; }

        // OnValidate no debe crear GameObjects: solo marca y difiere el trabajo a Update.
        private void OnValidate()
        {
            if (IsometricAngles.TryResolve(preset, out float p, out float y)) { pitch = p; yaw = y; }
            dirty = true;
        }

        private void Update()
        {
            EnsureHierarchy();
            if (dirty) Apply();
        }

        // ----------------------------------------------------------- Interno

        private void EnsureHierarchy()
        {
            if (yawNode == null) yawNode = GetOrCreateChild(transform, YawNodeName);
            if (pitchNode == null) pitchNode = GetOrCreateChild(yawNode, PitchNodeName);

            if (cam == null)
            {
                cam = pitchNode.GetComponentInChildren<Camera>(true);
                if (cam == null)
                {
                    var go = new GameObject(CameraNodeName, typeof(Camera));
                    go.transform.SetParent(pitchNode, false);
                    cam = go.GetComponent<Camera>();
                    if (Camera.main == null) go.tag = "MainCamera";
                }
            }
        }

        private static Transform GetOrCreateChild(Transform parent, string childName)
        {
            Transform t = parent.Find(childName);
            if (t == null)
            {
                t = new GameObject(childName).transform;
                t.SetParent(parent, false);
            }
            t.localScale = Vector3.one;
            return t;
        }

        private void Apply()
        {
            dirty = false;
            if (yawNode == null || pitchNode == null || cam == null) return;

            yawNode.localPosition = Vector3.zero;
            yawNode.localRotation = Quaternion.Euler(0f, yaw, 0f);

            pitchNode.localPosition = Vector3.zero;
            pitchNode.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            Transform camT = cam.transform;
            camT.localPosition = new Vector3(0f, 0f, -distance);
            camT.localRotation = Quaternion.identity;

            cam.orthographic = true;
            cam.orthographicSize = orthographicSize;
            cam.nearClipPlane = nearClip;
            cam.farClipPlane = distance + farClipPadding;
        }

        // ----------------------------------------------------------- Gizmos

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            // Pivot
            Vector3 c = transform.position;
            Gizmos.color = Color.yellow;
            float k = orthographicSize * 0.08f;
            Gizmos.DrawLine(c + Vector3.left * k, c + Vector3.right * k);
            Gizmos.DrawLine(c + Vector3.forward * k, c + Vector3.back * k);
            Gizmos.DrawLine(c + Vector3.down * k, c + Vector3.up * k);

            // Base planar (rojo = derecha, azul = adelante)
            Gizmos.color = Color.red; Gizmos.DrawRay(c, PlanarRight * orthographicSize * 0.5f);
            Gizmos.color = Color.blue; Gizmos.DrawRay(c, PlanarForward * orthographicSize * 0.5f);

            // Área visible aproximada proyectada sobre el suelo (plano Y = pivot.y)
            if (cam != null)
            {
                Vector2 h = GroundViewHalfExtents;
                Vector3 r = PlanarRight * h.x;
                Vector3 f = PlanarForward * h.y;
                Gizmos.color = new Color(0f, 1f, 1f, 0.9f);
                Gizmos.DrawLine(c - r - f, c + r - f);
                Gizmos.DrawLine(c + r - f, c + r + f);
                Gizmos.DrawLine(c + r + f, c - r + f);
                Gizmos.DrawLine(c - r + f, c - r - f);
            }

            // Línea pivot -> cámara
            if (cam != null)
            {
                Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
                Gizmos.DrawLine(c, cam.transform.position);
            }

            // Grilla de suelo de referencia
            Gizmos.color = new Color(1f, 1f, 1f, 0.12f);
            int n = Mathf.RoundToInt(gizmoGroundSize);
            for (int i = -n; i <= n; i++)
            {
                Gizmos.DrawLine(new Vector3(i, c.y, -n), new Vector3(i, c.y, n));
                Gizmos.DrawLine(new Vector3(-n, c.y, i), new Vector3(n, c.y, i));
            }
        }
    }
}