using UnityEngine;

namespace LoopEngine.IsoCamera.Core
{
    /// <summary>
    /// Restringe el punto de foco de la cámara. Implementa esto para límites poligonales,
    /// por sala, por collider, etc., sin tocar el componente de follow.
    /// </summary>
    public interface ICameraBounds
    {
        /// <summary>
        /// Devuelve el foco corregido.
        /// </summary>
        /// <param name="focus">Foco deseado, en mundo.</param>
        /// <param name="viewHalfExtents">
        /// Semiextensiones X/Z del área visible sobre el suelo. Si es Vector3.zero se
        /// limita el punto de foco; si no, se limita el área visible completa.
        /// </param>
        Vector3 Clamp(Vector3 focus, Vector3 viewHalfExtents);
    }

    /// <summary>
    /// Unity no serializa interfaces. Igual que con ICameraTarget, se acepta un
    /// UnityEngine.Object cualquiera y se extrae el ICameraBounds.
    /// </summary>
    public static class CameraBoundsResolver
    {
        public static ICameraBounds Resolve(Object source)
        {
            switch (source)
            {
                case null: return null;
                case ICameraBounds direct: return direct;
                case GameObject go: return go.GetComponent<ICameraBounds>();
                case Component c: return c.GetComponent<ICameraBounds>();
                default: return null;
            }
        }
    }
}