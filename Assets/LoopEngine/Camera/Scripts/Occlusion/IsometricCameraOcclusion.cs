using LoopEngine.IsoCamera.Core;
using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.IsoCamera.Occlusion
{
    /// <summary>
    /// Detecta qué renderers se interponen entre la cámara y el objetivo, y delega
    /// el efecto visual en una IOccluderFadeStrategy.
    ///
    /// El barrido va de la CÁMARA hacia el objetivo y se corta antes de llegar
    /// (Target Padding). Así el collider del propio objetivo nunca se detecta a sí
    /// mismo y no hace falta ponerlo en una capa aparte.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(IsometricCameraRig))]
    [AddComponentMenu("Iso Camera/Isometric Camera Occlusion")]
    public sealed class IsometricCameraOcclusion : MonoBehaviour
    {
        public enum FadeMode
        {
            /// <summary>Apaga el renderer. Siempre funciona; visualmente brusco.</summary>
            Hide,
            /// <summary>Sustituye por instancias de un material de fade. Recomendado.</summary>
            MaterialSwap,
            /// <summary>Convierte el material a transparente en caliente. Frágil.</summary>
            RuntimeTransparency
        }

        [Header("Objetivo")]
        [Tooltip("Si se deja vacío, se usa el objetivo del IsometricCameraFollow del mismo GameObject.")]
        [SerializeField] private Object targetObject;
        [Tooltip("Altura sobre el objetivo a la que se apunta. Evita barrer solo sus pies.")]
        [SerializeField] private float targetHeightOffset = 1f;

        [Header("Detección")]
        [SerializeField] private LayerMask occluderLayers = ~0;
        [Tooltip("Radio del barrido. 0 = rayo fino. Un radio pequeño atenúa también lo " +
                 "que roza el borde del personaje, que suele verse mejor.")]
        [SerializeField, Min(0f)] private float castRadius = 0.35f;
        [Tooltip("Distancia que se recorta antes de llegar al objetivo.")]
        [SerializeField, Min(0f)] private float targetPadding = 0.6f;
        [Tooltip("Segundos entre comprobaciones. 0 = cada frame. Subirlo ahorra CPU.")]
        [SerializeField, Min(0f)] private float checkInterval = 0.05f;
        [SerializeField, Min(1)] private int maxOccluders = 16;

        [Header("Efecto")]
        [SerializeField] private FadeMode mode = FadeMode.MaterialSwap;
        [Tooltip("Requerido por MaterialSwap. Créalo con Surface Type = Transparent " +
                 "o con un Shader Graph con dithering.")]
        [SerializeField] private Material fadeMaterial;
        [SerializeField, Range(0f, 1f)] private float fadeAlpha = 0.28f;

        [Header("Gizmos")]
        [SerializeField] private bool drawGizmos = true;

        private IsometricCameraRig rig;
        private IsometricCameraFollow follow;
        private ICameraTarget target;
        private IOccluderFadeStrategy strategy;

        private RaycastHit[] hitBuffer;
        private readonly HashSet<Renderer> currentOccluders = new HashSet<Renderer>();
        private readonly HashSet<Renderer> previousOccluders = new HashSet<Renderer>();
        private readonly List<Renderer> toRestore = new List<Renderer>();
        private float timer;

        /// <summary>Cantidad de renderers atenuados ahora mismo. Para HUD y debug.</summary>
        public int OccluderCount => currentOccluders.Count;

        /// <summary>Aplica el bloque de oclusión de un IsometricCameraSettings.</summary>
        public void Apply(IsometricCameraSettings.OcclusionSettings s)
        {
            if (s == null) return;
            occluderLayers = s.occluderLayers;
            castRadius = s.castRadius;
            targetPadding = s.targetPadding;
            checkInterval = s.checkInterval;
            targetHeightOffset = s.targetHeightOffset;
            fadeAlpha = s.fadeAlpha;

            bool materialChanged = fadeMaterial != s.fadeMaterial;
            fadeMaterial = s.fadeMaterial;

            // La estrategia se construye a partir del material, así que hay que
            // reconstruirla si cambió, restaurando antes lo que estuviera atenuado.
            if (materialChanged && isActiveAndEnabled) SetStrategy(CreateStrategy());
        }

        /// <summary>Cambia la estrategia en runtime. Restaura la anterior antes de sustituirla.</summary>
        public void SetStrategy(IOccluderFadeStrategy value)
        {
            strategy?.Dispose();
            currentOccluders.Clear();
            previousOccluders.Clear();
            strategy = value;
        }

        // ----------------------------------------------------------- Unity

        private void Awake()
        {
            rig = GetComponent<IsometricCameraRig>();
            follow = GetComponent<IsometricCameraFollow>();
            hitBuffer = new RaycastHit[maxOccluders];
        }

        private void OnEnable()
        {
            strategy ??= CreateStrategy();
            timer = 0f;
        }

        private void OnDisable()
        {
            strategy?.Dispose();
            strategy = null;
            currentOccluders.Clear();
            previousOccluders.Clear();
        }

        private void LateUpdate()
        {
            if (rig == null || strategy == null) return;

            if (checkInterval > 0f)
            {
                timer -= Time.deltaTime;
                if (timer > 0f) return;
                timer = checkInterval;
            }

            if (!TryGetTargetPoint(out Vector3 targetPoint))
            {
                ClearAll();
                return;
            }

            Scan(targetPoint);
            Reconcile();
        }

        // ----------------------------------------------------------- Núcleo

        private void Scan(Vector3 targetPoint)
        {
            currentOccluders.Clear();

            Vector3 origin = rig.Camera.transform.position;
            Vector3 toTarget = targetPoint - origin;
            float distance = toTarget.magnitude - targetPadding;
            if (distance <= 0f) return;

            Vector3 dir = toTarget.normalized;

            int count = castRadius > 0f
                ? Physics.SphereCastNonAlloc(origin, castRadius, dir, hitBuffer, distance,
                                             occluderLayers, QueryTriggerInteraction.Ignore)
                : Physics.RaycastNonAlloc(origin, dir, hitBuffer, distance,
                                          occluderLayers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                var r = hitBuffer[i].collider.GetComponentInChildren<Renderer>();
                if (r != null) currentOccluders.Add(r);
            }
        }

        /// <summary>
        /// Aplica solo a los que entran y restaura solo a los que salen. Comparar
        /// conjuntos evita reasignar materiales cada frame, que anularía el batching.
        /// </summary>
        private void Reconcile()
        {
            foreach (var r in currentOccluders)
                if (!previousOccluders.Contains(r)) strategy.Apply(r, fadeAlpha);

            toRestore.Clear();
            foreach (var r in previousOccluders)
                if (!currentOccluders.Contains(r)) toRestore.Add(r);

            for (int i = 0; i < toRestore.Count; i++) strategy.Restore(toRestore[i]);

            previousOccluders.Clear();
            foreach (var r in currentOccluders) previousOccluders.Add(r);
        }

        private void ClearAll()
        {
            if (previousOccluders.Count == 0) return;
            foreach (var r in previousOccluders) strategy.Restore(r);
            previousOccluders.Clear();
            currentOccluders.Clear();
        }

        private bool TryGetTargetPoint(out Vector3 point)
        {
            point = Vector3.zero;

            if (target == null || !target.IsValid)
            {
                target = CameraTargetResolver.Resolve(targetObject);
                if (target == null && follow != null) target = follow.Target;
            }

            if (target == null || !target.IsValid) return false;
            point = target.Position + Vector3.up * targetHeightOffset;
            return true;
        }

        private IOccluderFadeStrategy CreateStrategy()
        {
            switch (mode)
            {
                case FadeMode.MaterialSwap:
                    if (fadeMaterial != null) return new MaterialSwapFadeStrategy(fadeMaterial);
                    Debug.LogWarning("[IsoCamera] MaterialSwap sin material de fade asignado. " +
                                     "Usando Hide para que la detección siga siendo visible.", this);
                    return new HideFadeStrategy();

                case FadeMode.RuntimeTransparency:
                    return new RuntimeTransparencyFadeStrategy();

                default:
                    return new HideFadeStrategy();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || rig == null || !Application.isPlaying) return;
            if (!TryGetTargetPoint(out Vector3 targetPoint)) return;

            Vector3 origin = rig.Camera.transform.position;
            Gizmos.color = currentOccluders.Count > 0 ? Color.red : Color.green;
            Gizmos.DrawLine(origin, targetPoint);
            if (castRadius > 0f) Gizmos.DrawWireSphere(targetPoint, castRadius);
        }
    }
}