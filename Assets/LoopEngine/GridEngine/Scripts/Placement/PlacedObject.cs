using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.GridEngine.Placement
{
    /// <summary>
    /// Una instancia ya colocada en la grilla. Es el tipo de celda de dominio:
    /// la grilla de colocación es una Grid&lt;PlacedObject&gt; donde cada celda ocupada
    /// referencia al mismo PlacedObject (y las libres valen null).
    /// </summary>
    public class PlacedObject
    {
        public PlaceableDefinition Definition { get; }
        public Vector2Int Anchor { get; }
        public GridRotation Rotation { get; }
        public IReadOnlyList<Vector2Int> Cells { get; }

        /// <summary>GameObject visual instanciado (puede ser null si la definición no tiene prefab).</summary>
        public GameObject Visual { get; set; }

        public PlacedObject(PlaceableDefinition definition, Vector2Int anchor,
                            GridRotation rotation, IReadOnlyList<Vector2Int> cells)
        {
            Definition = definition;
            Anchor = anchor;
            Rotation = rotation;
            Cells = cells;
        }
    }
}