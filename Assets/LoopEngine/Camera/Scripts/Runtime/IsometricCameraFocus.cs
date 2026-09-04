using System;
using UnityEngine;

namespace LoopEngine.IsoCamera.Core
{
    /// <summary>
    /// Lleva el foco de la cámara a un punto con interpolación, opcionalmente
    /// ajustando el zoom, y devuelve el control al follow al terminar.
    ///
    /// Casos de uso: mostrar un objetivo de misión, una unidad que pide atención,
    /// el resultado de una acción fuera de pantalla, transiciones de cinemática.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(IsometricCameraRig))]
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("Iso Camera/Isometric Camera Focus")]
    public sealed class IsometricCameraFocus : MonoBehaviour
    {
        [Header("Transición")]
        [SerializeField, Min(0.01f)] private float defaultDuration = 0.6f;
        [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Zoom")]
        [Tooltip("Ajusta también el zoom durante el enfoque y lo restaura al volver.")]
        [SerializeField] private bool alsoZoom = false;
        [SerializeField, Range(0f, 1f)] private float focusZoom01 = 0.25f;

        [Header("Integración")]
        [Tooltip("Suspende el follow durante la transición.")]
        [SerializeField] private bool suspendFollow = true;
        [Tooltip("Al terminar, vuelve al foco que tenía antes en vez de quedarse.")]
        [SerializeField] private bool returnAfterHold = false;
        [SerializeField, Min(0f)] private float holdDuration = 1f;

        private IsometricCameraRig rig;
        private IsometricCameraFollow follow;
        private IsometricCameraZoom zoom;

        private enum Phase { Idle, Going, Holding, Returning }

        private Phase phase = Phase.Idle;
        private Vector3 fromPoint, toPoint, originPoint;
        private float fromZoom, toZoom, originZoom;
        private Transform liveTarget;
        private float elapsed, duration, holdLeft;

        /// <summary>Se dispara cuando la secuencia completa termina y se libera el follow.</summary>
        public event Action Completed;

        // ---------------------------------------------------------------- API

        public bool IsFocusing => phase != Phase.Idle;

        public void FocusOn(Vector3 point, float customDuration = -1f)
        {
            liveTarget = null;
            Begin(point, customDuration);
        }

        /// <summary>Enfoca un Transform y lo sigue durante la transición (útil si se mueve).</summary>
        public void FocusOn(Transform target, float customDuration = -1f)
        {
            if (target == null) return;
            liveTarget = target;
            Begin(target.position, customDuration);
        }

        /// <summary>Corta la transición y devuelve el control al follow de inmediato.</summary>
        public void Cancel()
        {
            if (phase == Phase.Idle) return;
            phase = Phase.Idle;
            liveTarget = null;
            Release();
        }

        // ----------------------------------------------------------- Unity

        private void Awake()
        {
            rig = GetComponent<IsometricCameraRig>();
            follow = GetComponent<IsometricCameraFollow>();
            zoom = GetComponent<IsometricCameraZoom>();
        }

        private void OnDisable() => Cancel();

        private void LateUpdate()
        {
            if (phase == Phase.Idle || rig == null) return;

            if (phase == Phase.Holding)
            {
                holdLeft -= Time.deltaTime;
                if (liveTarget != null) rig.FocusPoint = liveTarget.position;
                if (holdLeft > 0f) return;

                // Volver: se invierte el tramo usando el punto de partida original.
                fromPoint = rig.FocusPoint;
                toPoint = originPoint;
                fromZoom = zoom != null ? zoom.TargetZoom01 : 0f;
                toZoom = originZoom;
                elapsed = 0f;
                phase = Phase.Returning;
                return;
            }

            if (liveTarget != null && phase == Phase.Going) toPoint = liveTarget.position;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float e = ease.Evaluate(t);

            rig.FocusPoint = Vector3.LerpUnclamped(fromPoint, toPoint, e);
            if (alsoZoom && zoom != null) zoom.Zoom01 = Mathf.LerpUnclamped(fromZoom, toZoom, e);

            if (t < 1f) return;

            if (phase == Phase.Going && returnAfterHold)
            {
                holdLeft = holdDuration;
                phase = Phase.Holding;
                return;
            }

            phase = Phase.Idle;
            liveTarget = null;
            Release();
        }

        // ----------------------------------------------------------- Interno

        private void Begin(Vector3 point, float customDuration)
        {
            if (rig == null) rig = GetComponent<IsometricCameraRig>();

            originPoint = rig.FocusPoint;
            fromPoint = rig.FocusPoint;
            toPoint = new Vector3(point.x, rig.FocusPoint.y, point.z);

            if (zoom != null)
            {
                originZoom = zoom.TargetZoom01;
                fromZoom = zoom.Zoom01;
                toZoom = alsoZoom ? focusZoom01 : originZoom;
            }

            duration = customDuration > 0f ? customDuration : defaultDuration;
            elapsed = 0f;
            phase = Phase.Going;

            if (suspendFollow && follow != null) follow.Suspended = true;
        }

        private void Release()
        {
            if (suspendFollow && follow != null) follow.Suspended = false;
            Completed?.Invoke();
        }
    }
}