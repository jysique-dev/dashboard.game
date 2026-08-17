using UnityEngine;

namespace LoopEngine.GridEngine.Placement
{
    /// <summary>
    /// Host de escena real para la colocación: una Grid&lt;PlacedObject&gt;.
    /// Las celdas arrancan vacías (null). Sustituye al SampleGridHost de pruebas
    /// cuando quieras la grilla de ocupación del juego.
    /// </summary>
    [AddComponentMenu("LoopEngine/Grid/Placement Grid Host")]
    public class PlacementGridHost : GridBehaviour<PlacedObject>
    {
        protected override PlacedObject CreateCell(Grid<PlacedObject> grid, Vector2Int coord) => null;
    }
}