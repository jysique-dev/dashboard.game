using LoopEngine.IsoCamera.Core;
using UnityEngine;

namespace LoopEngine.IsoCamera.Core
{

    /// <summary>
    /// Límite rectangular alineado a los ejes X/Z, centrado en este GameObject.
    /// La altura (Y) no se restringe.
    /// </summary>
    [AddComponentMenu("Iso Camera/Rect Camera Bounds")]
    public sealed class RectCameraBounds : MonoBehaviour, ICameraBounds
    {
        [Tooltip("Tamaño total del área jugable en X y Z (no la mitad).")]
        [SerializeField] private Vector2 size = new Vector2(40f, 40f);
        [SerializeField] private bool drawGizmos = true;

        public Vector2 Size { get => size; set => size = value; }
        public Vector3 Center => transform.position;

        public Vector3 Clamp(Vector3 focus, Vector3 viewHalfExtents)
        {
            Vector3 c = Center;
            focus.x = ClampAxis(focus.x, c.x, size.x * 0.5f, viewHalfExtents.x);
            focus.z = ClampAxis(focus.z, c.z, size.y * 0.5f, viewHalfExtents.z);
            return focus;
        }

        /// <summary>
        /// Si el área visible es más grande que el límite, el rango válido se invierte.
        /// En ese caso se centra en vez de producir un salto: es el comportamiento
        /// esperado cuando el jugador hace zoom out más allá del tamaño del nivel.
        /// </summary>
        private static float ClampAxis(float value, float center, float halfSize, float viewHalf)
        {
            float min = center - halfSize + viewHalf;
            float max = center + halfSize - viewHalf;
            return min > max ? center : Mathf.Clamp(value, min, max);
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
            Gizmos.DrawWireCube(Center, new Vector3(size.x, 0.05f, size.y));
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            // Área efectiva para el foco, considerando la cámara activa.
            var rig = FindFirstObjectByType<IsometricCameraRig>();
            if (rig == null) return;

            Vector3 ext = rig.GroundViewAabbExtents;
            float w = Mathf.Max(size.x - ext.x * 2f, 0f);
            float d = Mathf.Max(size.y - ext.z * 2f, 0f);
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
            Gizmos.DrawWireCube(Center, new Vector3(w, 0.05f, d));
        }
#endif
    }
}