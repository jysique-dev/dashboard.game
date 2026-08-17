using System;
using UnityEngine;

namespace LoopEngine.GridEngine
{
    /// <summary>
    /// Configuración de una grilla como asset reutilizable.
    /// Sigue el mismo patrón que CameraSettings del módulo CameraEngine:
    /// los datos viven en un ScriptableObject y los sistemas los consumen.
    /// </summary>
    [CreateAssetMenu(fileName = "GridSettings", menuName = LoopRoutes.GridSettings)]
    public class GridSettings : ScriptableObject
    {
        [Header("Dimensiones (en celdas)")]
        [Min(1)] public int width = 20;
        [Min(1)] public int height = 20;

        [Header("Escala y ubicación")]
        [Min(0.01f)] public float cellSize = 1f;
        public Vector3 origin = Vector3.zero;

        [Header("Orientación")]
        [Tooltip("XZ = suelo del citybuilder 2.5D. XY = estilo 2D.")]
        public GridPlane plane = GridPlane.XZ;

        /// <summary>
        /// Crea una instancia de grilla genérica a partir de esta configuración.
        /// El tipo de celda (TCell) lo decide quien la construye, así el mismo asset
        /// sirve para una grilla de ocupación, de selección, de movimiento, etc.
        /// </summary>
        public Grid<TCell> CreateGrid<TCell>(Func<Grid<TCell>, Vector2Int, TCell> createCell = null)
            => new Grid<TCell>(width, height, cellSize, origin, plane, createCell);
    }
}