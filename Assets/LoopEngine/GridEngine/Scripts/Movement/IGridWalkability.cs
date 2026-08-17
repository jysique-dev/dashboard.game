using UnityEngine;

namespace LoopEngine.GridEngine.Movement
{
    /// <summary>
    /// Indica si un personaje puede pisar una celda. Desacopla el pathfinding de CÓMO se
    /// decide la caminabilidad: hoy viene de la ocupación (GridWalkability), pero podrías
    /// combinar terreno, agua, zonas bloqueadas, etc. en otra implementación.
    /// </summary>
    public interface IGridWalkability
    {
        bool IsWalkable(Vector2Int cell);
    }
}