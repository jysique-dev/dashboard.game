using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.GridEngine
{
    /// <summary>
    /// Vista de solo lectura de una grilla, SIN exponer el tipo de celda (TCell).
    /// Permite que sistemas como la visualización o el input dependan de la geometría
    /// y de los eventos de la grilla sin acoplarse al dato que guarda cada celda.
    /// Grid&lt;TCell&gt; la implementa sin cambios en su cuerpo.
    /// </summary>
    public interface IReadOnlyGrid
    {
        int Width { get; }
        int Height { get; }
        float CellSize { get; }
        Vector3 Origin { get; }
        GridPlane Plane { get; }

        Vector3 CellToWorld(Vector2Int cell);
        Vector3 GetCellCenterWorld(Vector2Int cell);
        bool TryWorldToCell(Vector3 world, out Vector2Int cell);
        Vector2Int WorldToCellClamped(Vector3 world);
        bool IsInside(Vector2Int cell);
        IEnumerable<Vector2Int> AllCoordinates();

        /// <summary>Se dispara cuando cambia el contenido de una celda (solo entrega la coordenada).</summary>
        event Action<Vector2Int> OnCellChanged;
    }
}