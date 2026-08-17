using UnityEngine;

namespace LoopEngine.GridEngine.Placement
{
    /// <summary>Cuatro orientaciones en pasos de 90°.</summary>
    public enum GridRotation
    {
        Deg0 = 0,
        Deg90 = 1,
        Deg180 = 2,
        Deg270 = 3
    }

    public static class GridRotationExtensions
    {
        public static float ToDegrees(this GridRotation r) => (int)r * 90f;

        public static GridRotation RotatedCW(this GridRotation r) => (GridRotation)(((int)r + 1) & 3);
        public static GridRotation RotatedCCW(this GridRotation r) => (GridRotation)(((int)r + 3) & 3);

        /// <summary>
        /// Rota un offset de celda (en espacio de grilla) en pasos de 90° (sentido antihorario).
        /// Esta rotación en coordenadas de celda es la que define QUÉ celdas ocupa la huella,
        /// de forma exacta e independiente del plano del mundo.
        /// </summary>
        public static Vector2Int Rotate(this GridRotation r, Vector2Int offset) => r switch
        {
            GridRotation.Deg90 => new Vector2Int(-offset.y, offset.x),
            GridRotation.Deg180 => new Vector2Int(-offset.x, -offset.y),
            GridRotation.Deg270 => new Vector2Int(offset.y, -offset.x),
            _ => offset
        };
    }
}