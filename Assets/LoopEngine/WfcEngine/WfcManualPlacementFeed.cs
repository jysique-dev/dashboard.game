using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.WfcEngine
{
    /// <summary>
    /// Fuente de colocaciones manual, para el editor. Existe para que el distrito se pueda
    /// probar entero sin depender de GridEngine: si algo falla, se sabe de que lado esta.
    /// </summary>
    [AddComponentMenu("LoopEngine/WFC/Manual Placement Feed")]
    [RequireComponent(typeof(WfcDistrict))]
    public class WfcManualPlacementFeed : MonoBehaviour, IWfcPlacementFeed
    {
        [SerializeField] private List<WfcBuildingDefinition> palette = new List<WfcBuildingDefinition>();
        [SerializeField] private int selected;

        public event Action<Vector3Int, WfcBuildingDefinition> Placed;
        public event Action<Vector3Int> Removed;

        public IReadOnlyList<WfcBuildingDefinition> Palette => palette;

        public int Selected
        {
            get => selected;
            set => selected = palette.Count == 0 ? 0 : Mathf.Clamp(value, 0, palette.Count - 1);
        }

        public WfcBuildingDefinition Current
            => palette.Count == 0 ? null : palette[Mathf.Clamp(selected, 0, palette.Count - 1)];

        public void PlaceAt(Vector3Int cell)
        {
            var definition = Current;

            if (definition == null)
            {
                Debug.LogWarning("[WFC] Paleta vacia o edificio seleccionado nulo.", this);
                return;
            }

            // Un evento sin suscriptores es el fallo mas dificil de ver: no hay excepcion,
            // no hay log, y el click parece no haber ocurrido. Se avisa explicitamente.
            if (Placed == null)
            {
                Debug.LogWarning(
                    "[WFC] Nadie escucha las colocaciones. Comprueba que el WfcDistrict " +
                    "esta en este mismo GameObject y activo.", this);
                return;
            }

            Placed.Invoke(cell, definition);
        }

        public void RemoveAt(Vector3Int cell)
        {
            if (Removed == null)
            {
                Debug.LogWarning("[WFC] Nadie escucha las retiradas.", this);
                return;
            }

            Removed.Invoke(cell);
        }
    }
}