using LoopEngine.IsoCamera.InputAbstraction;
using System;
using UnityEngine;

namespace LoopEngine.IsoCamera.Core
{
    /// <summary>
    /// Rota el yaw del rig en pasos discretos, con interpolación temporal.
    ///
    /// Mantiene su propio yaw acumulado en vez de leerlo del rig. El rig normaliza
    /// el ángulo a (-180, 180], así que ir de 170° a 200° lo guardaría como -160°
    /// y una interpolación que leyera de vuelta ese valor daría un giro largo hacia atrás.
    /// Escribiendo sin leer, el tween siempre gira por el camino corto que pediste.
    ///
    /// Ejecuta antes que el follow (orden -100): la dead zone se orienta con la base
    /// planar, que depende del yaw de este frame.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(IsometricCameraRig))]
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("Iso Camera/Isometric Camera Rotator")]
    public sealed class IsometricCameraRotator : MonoBehaviour
    {
        [Header("Pasos")]
        [Tooltip("Divisiones de una vuelta completa. 4 = pasos de 90°, 8 = de 45°.")]
        [SerializeField, Range(1, 24)] private int stepsPerTurn = 4;

        [Header("Transición")]
        [SerializeField, Min(0.01f)] private float duration = 0.25f;
        [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Input")]
        [SerializeField] private bool readInput = true;
        [Tooltip("Permite acumular pulsaciones durante un giro en curso.")]
        [SerializeField] private bool queueInput = true;

        private IsometricCameraRig rig;
        private float startYaw;
        private float currentYaw;
        private float targetYaw;
        private float elapsed;
        private bool rotating;

        /// <summary>Se dispara cada vez que se fija un nuevo destino de rotación.</summary>
        public event Action<float> RotationStarted;

        /// <summary>Se dispara al terminar el giro, con el yaw final.</summary>
        public event Action<float> RotationCompleted;

        // ---------------------------------------------------------------- API

        public float StepAngle => 360f / Mathf.Max(1, stepsPerTurn);
        public bool IsRotating => rotating;
        public float TargetYaw => targetYaw;

        /// <summary>Aplica el bloque de rotación de un IsometricCameraSettings.</summary>
        public void Apply(IsometricCameraSettings.RotationSettings s)
        {
            if (s == null) return;
            stepsPerTurn = Mathf.Clamp(s.stepsPerTurn, 1, 24);
            duration = Mathf.Max(0.01f, s.duration);
            if (s.ease != null && s.ease.length > 1) ease = s.ease;
        }

        /// <summary>Gira n pasos. Positivo = sentido horario visto desde arriba.</summary>
        public void RotateSteps(int steps)
        {
            if (steps == 0) return;
            if (rotating && !queueInput) return;
            SetTarget(targetYaw + steps * StepAngle);
        }

        /// <summary>Gira hasta un yaw absoluto, sin obligar a que caiga en un paso.</summary>
        public void RotateTo(float yaw, bool instant = false)
        {
            if (instant)
            {
                currentYaw = targetYaw = yaw;
                rotating = false;
                Apply();
                RotationCompleted?.Invoke(currentYaw);
                return;
            }
            SetTarget(yaw);
        }

        /// <summary>Ajusta el yaw actual al paso más cercano. Útil tras una rotación libre.</summary>
        public void SnapToNearestStep()
        {
            float step = StepAngle;
            SetTarget(Mathf.Round(currentYaw / step) * step);
        }

        // ----------------------------------------------------------- Unity

        private void Awake()
        {
            rig = GetComponent<IsometricCameraRig>();
            currentYaw = targetYaw = rig != null ? rig.Yaw : 45f;
        }

        private void LateUpdate()
        {
            if (rig == null) return;

            if (readInput)
            {
                int step = IsoInput.Source.RotateStep;
                if (step != 0) RotateSteps(step);
            }

            if (!rotating) return;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            currentYaw = Mathf.Lerp(startYaw, targetYaw, ease.Evaluate(t));
            Apply();

            if (t < 1f) return;

            currentYaw = targetYaw;
            rotating = false;
            Apply();
            RotationCompleted?.Invoke(currentYaw);
        }

        // ----------------------------------------------------------- Interno

        private void SetTarget(float yaw)
        {
            // Reiniciar desde el ángulo actual, no desde el anterior destino:
            // así encadenar pulsaciones no produce saltos.
            startYaw = currentYaw;
            targetYaw = yaw;
            elapsed = 0f;
            rotating = true;
            RotationStarted?.Invoke(targetYaw);
        }

        private void Apply() => rig.Yaw = currentYaw;

        private void OnValidate()
        {
            if (duration < 0.01f) duration = 0.01f;
        }
    }
}