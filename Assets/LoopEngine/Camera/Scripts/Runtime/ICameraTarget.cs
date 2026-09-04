using UnityEngine;

namespace LoopEngine.IsoCamera.Core
{
    /// <summary>
    /// Contrato mínimo que debe cumplir cualquier cosa que la cámara pueda seguir.
    /// La cámara NO conoce al Player, al enemigo ni a ningún tipo concreto: solo esto.
    /// </summary>
    public interface ICameraTarget
    {
        /// <summary>Punto del mundo a seguir.</summary>
        Vector3 Position { get; }

        /// <summary>False si el objetivo fue destruido o desactivado.</summary>
        bool IsValid { get; }
    }

    /// <summary>Adaptador que convierte cualquier Transform en un ICameraTarget.</summary>
    public sealed class TransformCameraTarget : ICameraTarget
    {
        private readonly Transform transform;

        public TransformCameraTarget(Transform transform) => this.transform = transform;

        public Vector3 Position => transform != null ? transform.position : Vector3.zero;
        public bool IsValid => transform != null;
    }

    /// <summary>Objetivo fijo en un punto del mundo. Útil para cinemáticas o cámaras de menú.</summary>
    public sealed class FixedCameraTarget : ICameraTarget
    {
        public Vector3 Position { get; set; }
        public bool IsValid => true;

        public FixedCameraTarget(Vector3 position) => Position = position;
    }

    /// <summary>
    /// Unity no serializa interfaces en el Inspector. Este resolvedor acepta un
    /// UnityEngine.Object cualquiera (Transform, GameObject o Component) y devuelve
    /// el ICameraTarget correspondiente, envolviéndolo en un adaptador si hace falta.
    /// </summary>
    public static class CameraTargetResolver
    {
        public static ICameraTarget Resolve(Object source)
        {
            switch (source)
            {
                case null:
                    return null;

                // Si ya implementa el contrato, se usa directamente.
                case ICameraTarget direct:
                    return direct;

                case GameObject go:
                    var found = go.GetComponent<ICameraTarget>();
                    return found ?? new TransformCameraTarget(go.transform);

                case Component c:
                    var onComponent = c.GetComponent<ICameraTarget>();
                    return onComponent ?? new TransformCameraTarget(c.transform);

                default:
                    Debug.LogWarning($"[IsoCamera] '{source.name}' no se puede usar como objetivo de cámara.", source);
                    return null;
            }
        }
    }
}