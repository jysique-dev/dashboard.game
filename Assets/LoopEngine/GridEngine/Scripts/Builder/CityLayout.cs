using System;
using System.Collections.Generic;
using UnityEngine;
using LoopEngine.GridEngine.Placement;

namespace LoopEngine.GridEngine.Builder
{
    /// <summary>
    /// Un plano de ciudad guardado como DATOS: una lista de colocaciones (pieza + ancla + rotación).
    /// Esta es la clave para construir ciudades "en el editor" sin necesitar una grilla viva:
    /// se autoría la lista en modo edición y en runtime se materializa llamando a TryPlace.
    /// Encaja con la idea de "zonas que se desbloquean": cada zona puede ser un CityLayout.
    /// </summary>
    [CreateAssetMenu(fileName = "CityLayout", menuName = LoopRoutes.CityLayout)]
    public class CityLayout : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public PlaceableDefinition placeable;
            public Vector2Int anchor;
            public GridRotation rotation;
        }

        public List<Entry> entries = new();

        public void Clear() => entries.Clear();

        public void Add(PlaceableDefinition placeable, Vector2Int anchor, GridRotation rotation)
            => entries.Add(new Entry { placeable = placeable, anchor = anchor, rotation = rotation });
    }
}