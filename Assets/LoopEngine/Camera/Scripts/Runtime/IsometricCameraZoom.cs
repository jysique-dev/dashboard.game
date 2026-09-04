using LoopEngine.IsoCamera.InputAbstraction;
using UnityEngine;

namespace LoopEngine.IsoCamera.Core
{
    /// <summary>
    /// Zoom isométrico. Modifica orthographicSize, NUNCA la distancia de la cámara:
    /// en proyección ortográfica acercar la cámara no cambia el encuadre, solo rompe
    /// el culling y las sombras.
    ///
    /// El zoom se guarda como un valor normalizado t en [0,1] y el tamaño real sale de
    /// una curva. Así puedes hacer que los primeros pasos de zoom sean finos y los
    /// últimos gruesos sin tocar código.
    ///
    /// Ejecuta antes que el follow (orden -100) para que los límites se calculen
    /// con el tamaño ya actualizado de este frame.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(IsometricCameraRig))]
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("Iso Camera/Isometric Camera Zoom")]
    public sealed class IsometricCameraZoom : MonoBehaviour
    {
        [Header("Rango")]
        [Tooltip("orthographicSize con el zoom al máximo (t = 0).")]
        [SerializeField, Min(0.1f)] private float minSize = 4f;
        [Tooltip("orthographicSize con el zoom al mínimo (t = 1).")]
        [SerializeField, Min(0.1f)] private float maxSize = 20f;
        [Tooltip("Reparte el rango. Lineal por defecto; EaseInOut da pasos finos cerca y gruesos lejos.")]
        [SerializeField] private AnimationCurve distribution = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Input")]
        [SerializeField] private bool readInput = true;
        [Tooltip("Fracción del rango que cubre una muesca de rueda.")]
        [SerializeField, Range(0.01f, 0.5f)] private float stepPerNotch = 0.12f;

        [Header("Suavizado")]
        [SerializeField, Min(0f)] private float smoothTime = 0.12f;

        [Header("Zoom hacia el cursor")]
        [Tooltip("Mantiene fijo el punto del suelo bajo el cursor. Pensado para cámaras libres " +
                 "tipo RTS. Si hay un follow activo con objetivo, ambos pelearán por el foco.")]
        [SerializeField] private bool zoomToCursor = false;

        [Header("Inicio")]
        [SerializeField, Range(0f, 1f)] private float startZoom = 0.5f;

        private IsometricCameraRig rig;
        private float targetT;
        private float currentT;
        private float velocityT;
        private bool warnedFollowConflict;

        // ---------------------------------------------------------------- API

        /// <summary>Zoom normalizado. 0 = lo más cerca, 1 = lo más lejos.</summary>
        public float Zoom01
        {
            get => currentT;
            set { targetT = Mathf.Clamp01(value); currentT = targetT; velocityT = 0f; ApplySize(); }
        }

        /// <summary>Destino del zoom. Lo que persigue el suavizado.</summary>
        public float TargetZoom01
        {
            get => targetT;
            set => targetT = Mathf.Clamp01(value);
        }

        public float CurrentSize => SizeAt(currentT);
        public float MinSize => minSize;
        public float MaxSize => maxSize;

        /// <summary>Aplica el bloque de zoom de un IsometricCameraSettings.</summary>
        public void Apply(IsometricCameraSettings.ZoomSettings s)
        {
            if (s == null) return;
            minSize = s.minSize;
            maxSize = Mathf.Max(s.maxSize, s.minSize);
            if (s.distribution != null && s.distribution.length > 1) distribution = s.distribution;
            stepPerNotch = s.stepPerNotch;
            smoothTime = s.smoothTime;
            zoomToCursor = s.zoomToCursor;
            startZoom = s.startZoom;
            Zoom01 = s.startZoom;
        }

        /// <summary>Desplaza el zoom en muescas. Positivo = alejar.</summary>
        public void ZoomBy(float notches) => targetT = Mathf.Clamp01(targetT + notches * stepPerNotch);

        /// <summary>Fija un orthographicSize concreto, resolviendo el t equivalente.</summary>
        public void SetSize(float size)
        {
            size = Mathf.Clamp(size, Mathf.Min(minSize, maxSize), Mathf.Max(minSize, maxSize));
            Zoom01 = InverseSize(size);
        }

        // ----------------------------------------------------------- Unity

        private void Awake()
        {
            rig = GetComponent<IsometricCameraRig>();
            targetT = currentT = Mathf.Clamp01(startZoom);
            ApplySize();
        }

        private void LateUpdate()
        {
            if (rig == null) return;

            if (readInput)
            {
                // Rueda arriba = acercar = reducir t.
                float scroll = IsoInput.Source.Zoom;
                if (!Mathf.Approximately(scroll, 0f)) ZoomBy(-scroll);
            }

            float previousT = currentT;
            currentT = smoothTime <= 0f
                ? targetT
                : Mathf.SmoothDamp(currentT, targetT, ref velocityT, smoothTime, Mathf.Infinity, Time.deltaTime);

            if (Mathf.Approximately(previousT, currentT)) return;

            if (zoomToCursor) ApplySizeAnchoredToCursor();
            else ApplySize();
        }

        // ----------------------------------------------------------- Interno

        private float SizeAt(float t) =>
            Mathf.Lerp(minSize, maxSize, distribution.Evaluate(Mathf.Clamp01(t)));

        /// <summary>
        /// Inversa aproximada de la curva. Se muestrea en vez de invertir analíticamente
        /// porque una AnimationCurve arbitraria no tiene inversa cerrada.
        /// </summary>
        private float InverseSize(float size)
        {
            const int samples = 64;
            float best = 0f, bestError = float.MaxValue;
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                float error = Mathf.Abs(SizeAt(t) - size);
                if (error < bestError) { bestError = error; best = t; }
            }
            return best;
        }

        private void ApplySize() => rig.OrthographicSize = SizeAt(currentT);

        /// <summary>
        /// Trigo cero: se mide dónde cae el cursor en el suelo antes y después de
        /// cambiar el tamaño, y se desplaza el foco por la diferencia. Funciona con
        /// cualquier pitch y yaw sin casos especiales.
        /// </summary>
        private void ApplySizeAnchoredToCursor()
        {
            WarnIfFollowConflict();

            Vector2 pointer = IsoInput.Source.PointerPosition;
            bool hadBefore = rig.ScreenPointToGround(pointer, out Vector3 before);

            ApplySize();

            if (!hadBefore) return;
            if (!rig.ScreenPointToGround(pointer, out Vector3 after)) return;

            rig.FocusPoint += before - after;
        }

        private void WarnIfFollowConflict()
        {
            if (warnedFollowConflict) return;
            var follow = GetComponent<IsometricCameraFollow>();
            if (follow != null && follow.enabled && follow.Target != null)
            {
                warnedFollowConflict = true;
                Debug.LogWarning("[IsoCamera] 'Zoom To Cursor' está activo a la vez que un follow con " +
                                 "objetivo. El follow recolocará el foco y el anclaje no se notará.", this);
            }
        }

        private void OnValidate()
        {
            if (maxSize < minSize) maxSize = minSize;
            if (Application.isPlaying && rig != null) ApplySize();
        }
    }
}