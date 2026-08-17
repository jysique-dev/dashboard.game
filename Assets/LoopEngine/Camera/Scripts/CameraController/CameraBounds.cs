using UnityEngine;

namespace LoopEngine.CameraEngine
{
    /// <summary>
    /// Límite rectangular (en el plano XZ) para la cámara, guardado como asset
    /// reutilizable. Se edita visualmente en la escena mediante gizmos con
    /// handles arrastrables (ver Editor/CameraBoundsEditors.cs).
    ///
    /// El rectángulo se define por 'center' y 'size' en coordenadas de mundo.
    /// 'padding' es un margen interno que se aplica SOLO al recortar, para que
    /// el punto de foco no llegue exactamente al borde.
    /// </summary>
    [CreateAssetMenu(fileName = "CameraBounds", menuName = LoopRoutes.CameraBounds)]
    public class CameraBounds : ScriptableObject
    {
        [Tooltip("Centro del rectángulo en mundo. X = X del mundo, Y = Z del mundo.")]
        public Vector2 center = Vector2.zero;

        [Tooltip("Ancho (X) y profundidad (Z) del área permitida, en unidades.")]
        public Vector2 size = new Vector2(200f, 200f);

        [Tooltip("Altura (Y) a la que se dibuja el gizmo. No afecta al recorte.")]
        public float drawHeight = 0f;

        [Min(0f)]
        [Tooltip("Margen interno aplicado al recortar el foco. Mantiene la cámara algo dentro del borde.")]
        public float padding = 0f;

        // --- Bordes derivados (solo lectura) ---
        public float MinX => center.x - size.x * 0.5f;
        public float MaxX => center.x + size.x * 0.5f;
        public float MinZ => center.y - size.y * 0.5f;
        public float MaxZ => center.y + size.y * 0.5f;

        /// <summary>
        /// Recorta una posición de mundo dentro del rectángulo (con padding).
        /// Conserva la coordenada Y de entrada.
        /// </summary>
        public Vector3 Clamp(Vector3 worldPosition)
        {
            float minX = MinX + padding;
            float maxX = MaxX - padding;
            float minZ = MinZ + padding;
            float maxZ = MaxZ - padding;

            // Si el padding "cierra" el área, colapsar al centro para no invertir.
            if (minX > maxX) minX = maxX = center.x;
            if (minZ > maxZ) minZ = maxZ = center.y;

            worldPosition.x = Mathf.Clamp(worldPosition.x, minX, maxX);
            worldPosition.z = Mathf.Clamp(worldPosition.z, minZ, maxZ);
            return worldPosition;
        }

        /// <summary>True si la posición ya está dentro del área permitida.</summary>
        public bool Contains(Vector3 worldPosition)
        {
            return worldPosition.x >= MinX && worldPosition.x <= MaxX
                && worldPosition.z >= MinZ && worldPosition.z <= MaxZ;
        }

        private void OnValidate()
        {
            size.x = Mathf.Max(0f, size.x);
            size.y = Mathf.Max(0f, size.y);
        }
    }
}