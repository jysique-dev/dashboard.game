using UnityEngine;

namespace LoopEngine.CameraEngine.Settings
{
    /// <summary>Modo de inclinación (pitch) de la cámara.</summary>
    public enum CameraPitchMode
    {
        Fixed,          // ángulo fijo (pitchAngle)
        DynamicByZoom,  // interpola minPitch..maxPitch según el zoom
        FreeDrag        // el jugador controla el pitch arrastrando (clamp a minPitch..maxPitch)
    }

    /// <summary>
    /// Configuración de la cámara 2.5D estilo citybuilder / RTS, como asset
    /// ScriptableObject reutilizable.
    ///
    /// NOTA (Chat 03): la sección de pitch se rehízo con un enum de modos y se
    /// añadió rotación por arrastre. Los campos viejos 'dynamicPitch',
    /// 'pitchAtMinZoom' y 'pitchAtMaxZoom' se reemplazaron por 'pitchMode',
    /// 'minPitch' y 'maxPitch'.
    /// </summary>
    [CreateAssetMenu(fileName = "CameraSettings", menuName = LoopRoutes.CameraSettings)]
    public class CameraSettings : ScriptableObject
    {
        [Header("Paneo (movimiento en el plano XZ)")]
        public float panSpeed = 25f;
        [Range(0f, 0.5f)] public float panSmoothing = 0.08f;

        [Header("Paneo por borde de pantalla")]
        public bool edgeScrollingEnabled = true;
        public float edgeScrollBorder = 12f;

        [Header("Zoom")]
        [Tooltip("Unidades de zoom por muesca/impulso.")]
        public float zoomSpeed = 3f;
        [Range(0f, 0.5f)] public float zoomSmoothing = 0.12f;
        public float minZoom = 8f;
        public float maxZoom = 45f;

        [Header("Rotación (yaw)")]
        [Tooltip("Velocidad de rotación con Q/E o gamepad (grados/seg).")]
        public float rotationSpeed = 120f;
        [Range(0f, 0.5f)] public float rotationSmoothing = 0.1f;

        [Header("Rotación por arrastre (mantener botón central)")]
        [Tooltip("Grados de yaw por píxel de movimiento horizontal del ratón.")]
        public float dragYawSensitivity = 0.2f;
        [Tooltip("Grados de pitch por píxel de movimiento vertical (solo modo FreeDrag).")]
        public float dragPitchSensitivity = 0.15f;
        public bool invertDragPitch = false;

        [Header("Inclinación (pitch) — el ángulo que da el look 2.5D")]
        public CameraPitchMode pitchMode = CameraPitchMode.Fixed;
        [Tooltip("Ángulo usado en modo Fixed y como valor inicial en FreeDrag.")]
        [Range(10f, 89f)] public float pitchAngle = 50f;
        [Tooltip("Límite inferior de pitch (DynamicByZoom y FreeDrag).")]
        [Range(10f, 89f)] public float minPitch = 30f;
        [Tooltip("Límite superior de pitch (DynamicByZoom y FreeDrag).")]
        [Range(10f, 89f)] public float maxPitch = 65f;

        private void OnValidate()
        {
            if (maxPitch < minPitch) maxPitch = minPitch;
            pitchAngle = Mathf.Clamp(pitchAngle, 10f, 89f);
        }
    }
}