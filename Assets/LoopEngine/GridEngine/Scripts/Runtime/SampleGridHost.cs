using UnityEngine;

namespace LoopEngine.GridEngine
{
    /// <summary>
    /// Host concreto de ejemplo: una grilla de enteros (Grid&lt;int&gt;).
    /// Sirve para probar la visualización de la Sesión 2 y como plantilla de host.
    /// En la Sesión 4 crearemos el host real con el tipo de celda de dominio del juego.
    ///
    /// Reemplaza al GridDemo desechable de la Sesión 1: ya puedes eliminar GridDemo.cs.
    /// </summary>
    [AddComponentMenu(LoopRoutes.GridHost)]
    public class SampleGridHost : GridBehaviour<int>
    {
        // Inicializa todas las celdas en 0. Aquí podrías sembrar, por ejemplo, un mapa de calor.
        protected override int CreateCell(Grid<int> grid, Vector2Int coord) => 0;
    }
}