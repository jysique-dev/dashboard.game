using System;
using UnityEngine;

namespace LoopEngine.WfcEngine
{
    /// <summary>
    /// De donde salen las colocaciones. El distrito solo conoce esta interfaz, asi que la
    /// misma logica sirve para GridEngine, para un editor manual o para un generador de
    /// barrios sin cambiar una linea del WFC.
    ///
    /// Las coordenadas son celdas de ciudad, no posiciones de mundo: el WFC no necesita
    /// saber el tamano de celda del sistema de colocacion, solo su rejilla.
    /// </summary>
    public interface IWfcPlacementFeed
    {
        event Action<Vector3Int, WfcBuildingDefinition> Placed;
        event Action<Vector3Int> Removed;
    }
}