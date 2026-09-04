using LoopEngine.IsoCamera.Core;
using LoopEngine.IsoCamera.InputAbstraction;
using UnityEngine;

namespace LoopEngine.IsoCamera.Test
{
    /// <summary>
    /// Cubo movible para probar el seguimiento. Implementa ICameraTarget, así que
    /// se puede arrastrar directamente al campo Target del follow.
    ///
    /// Demuestra la corrección de base: pulsar "arriba" mueve el objeto hacia el
    /// fondo de la PANTALLA, no hacia +Z del mundo. Sin esto, con yaw 45° el
    /// personaje se movería en diagonal respecto a lo que ve el jugador.
    /// </summary>
    [AddComponentMenu("Iso Camera/Test/Iso Test Target Mover")]
    public sealed class IsoTestTargetMover : MonoBehaviour, ICameraTarget
    {
        [SerializeField] private IsometricCameraRig rig;
        [SerializeField, Min(0f)] private float speed = 8f;
        [Tooltip("Desactivado, el movimiento va en ejes de mundo. Actívalo y desactívalo para ver la diferencia.")]
        [SerializeField] private bool useCameraBasis = true;
        [Tooltip("Rota el objeto hacia su dirección de avance.")]
        [SerializeField] private bool faceMoveDirection = true;
        [SerializeField, Min(0f)] private float turnSpeed = 720f;

        public Vector3 Position => transform.position;
        public bool IsValid => this != null && isActiveAndEnabled;

        /// <summary>Última dirección de movimiento en mundo. Solo lectura, para el HUD.</summary>
        public Vector3 LastMoveDirection { get; private set; }

        private void Reset() => rig = FindFirstObjectByType<IsometricCameraRig>();
        private void Awake() { if (rig == null) rig = FindFirstObjectByType<IsometricCameraRig>(); }

        private void Update()
        {
            Vector2 input = IsoInput.Source.Move;
            if (input.sqrMagnitude > 1f) input.Normalize();

            Vector3 dir;
            if (useCameraBasis && rig != null)
                dir = rig.PlanarRight * input.x + rig.PlanarForward * input.y;
            else
                dir = new Vector3(input.x, 0f, input.y);

            LastMoveDirection = dir;
            if (dir.sqrMagnitude < 0.0001f) return;

            transform.position += dir * (speed * Time.deltaTime);

            if (faceMoveDirection)
            {
                Quaternion look = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, look, turnSpeed * Time.deltaTime);
            }
        }
    }
}