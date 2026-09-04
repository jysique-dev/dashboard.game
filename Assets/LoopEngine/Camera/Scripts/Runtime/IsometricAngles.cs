using UnityEngine;

namespace LoopEngine.IsoCamera.Core
{
    /// <summary>
    /// Presets de ángulo para el rig. "Custom" deja los valores editables a mano.
    /// </summary>
    public enum IsometricAnglePreset
    {
        Custom = 0,
        TrueIsometric = 1,
        Dimetric2To1 = 2,
        Military45 = 3,
        TopDown = 4
    }

    /// <summary>
    /// Ángulos canónicos. "Pitch" = inclinación por debajo de la horizontal
    /// (0 = cámara horizontal, 90 = cenital), que es como Unity interpreta
    /// la rotación en X del transform.
    /// </summary>
    public static class IsometricAngles
    {
        /// <summary>
        /// Isométrico verdadero: los tres ejes del mundo se proyectan separados 120°.
        /// pitch = asin(tan(30°)) = 35.264389...  Relación de baldosa ancho:alto = √3 : 1.
        /// </summary>
        public const float TrueIsometricPitch = 35.26439f;

        /// <summary>
        /// Dimétrico 2:1 (el clásico de pixel art). pitch = asin(0.5) = 30°.
        /// Relación de baldosa ancho:alto = 2 : 1, cómodo para grillas de píxeles.
        /// </summary>
        public const float Dimetric2To1Pitch = 30f;

        /// <summary>Vista "militar"/oblicua a 45°. Relación ≈ 1.41 : 1.</summary>
        public const float Military45Pitch = 45f;

        /// <summary>Cenital. Se limita a 89.9 para no degenerar la base de movimiento.</summary>
        public const float TopDownPitch = 89.9f;

        public const float DefaultYaw = 45f;

        /// <summary>Resuelve un preset a (pitch, yaw). Devuelve false para Custom.</summary>
        public static bool TryResolve(IsometricAnglePreset preset, out float pitch, out float yaw)
        {
            yaw = DefaultYaw;
            switch (preset)
            {
                case IsometricAnglePreset.TrueIsometric: pitch = TrueIsometricPitch; return true;
                case IsometricAnglePreset.Dimetric2To1: pitch = Dimetric2To1Pitch; return true;
                case IsometricAnglePreset.Military45: pitch = Military45Pitch; return true;
                case IsometricAnglePreset.TopDown: pitch = TopDownPitch; yaw = 0f; return true;
                default: pitch = 0f; return false;
            }
        }

        /// <summary>
        /// Relación ancho:alto con la que se ve en pantalla una baldosa cuadrada del suelo.
        /// Con yaw = 45°, alto_proyectado = ancho * sin(pitch), por lo que ratio = 1 / sin(pitch).
        /// Útil para verificar visualmente que el ángulo es el esperado (2.0 → 2:1, 1.732 → iso real).
        /// </summary>
        public static float TileAspectRatio(float pitchDegrees)
        {
            float s = Mathf.Sin(pitchDegrees * Mathf.Deg2Rad);
            return s <= Mathf.Epsilon ? float.PositiveInfinity : 1f / s;
        }
    }
}