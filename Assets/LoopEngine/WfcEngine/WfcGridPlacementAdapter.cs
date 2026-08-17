using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.WfcEngine
{
    /// <summary>
    /// Adaptador a GridEngine. TODO el acoplamiento entre WFC y GridEngine vive en este
    /// archivo: si GridEngine cambia sus firmas, solo se toca aqui.
    ///
    /// Hay tres puntos marcados ADAPTAR que dependen de las firmas reales de
    /// GridPlacementSystem y de los nombres de campo de PlacedObject. Estan aislados a
    /// proposito y el resto del modulo compila y funciona sin ellos, usando
    /// WfcManualPlacementFeed.
    ///
    /// La forma de la integracion no cambia: GridEngine dice "se coloco X en la celda C",
    /// este adaptador traduce X a un WfcBuildingDefinition y emite Placed. El distrito
    /// hace el resto y no sabe que GridEngine existe.
    /// </summary>
    [AddComponentMenu("LoopEngine/WFC/Grid Placement Adapter")]
    public class WfcGridPlacementAdapter : MonoBehaviour, IWfcPlacementFeed
    {
        [Serializable]
        public class PlaceableBinding
        {
            [Tooltip("Nombre del PlaceableDefinition de GridEngine.")]
            public string placeableName;

            public WfcBuildingDefinition building;
        }

        [Tooltip("Que definicion de GridEngine corresponde a que edificio WFC.")]
        [SerializeField] private List<PlaceableBinding> bindings = new List<PlaceableBinding>();

        public event Action<Vector3Int, WfcBuildingDefinition> Placed;
        public event Action<Vector3Int> Removed;

        private void OnEnable()
        {
            // ---------------------------------------------------------------- ADAPTAR 1
            // Suscribirse a los eventos reales de GridPlacementSystem.
            //
            //   placementSystem.OnPlaced += HandlePlaced;
            //   placementSystem.OnRemoved += HandleRemoved;
            //
            // Segun la firma real, HandlePlaced puede recibir un PlacedObject, una celda,
            // o ambos. Ajustar las firmas de abajo en consecuencia.
        }

        private void OnDisable()
        {
            // ---------------------------------------------------------------- ADAPTAR 2
            // Desuscribirse con las mismas firmas.
            //
            //   placementSystem.OnPlaced -= HandlePlaced;
            //   placementSystem.OnRemoved -= HandleRemoved;
        }

        /// <summary>
        /// ADAPTAR 3: traducir el objeto colocado de GridEngine a celda + definicion.
        ///
        /// Lo unico que hace falta saber de PlacedObject es su celda de origen y que
        /// PlaceableDefinition lo genero. Con eso, Resolve devuelve el edificio WFC y el
        /// resto del sistema ya funciona.
        /// </summary>
        public void HandlePlaced(Vector3Int originCell, string placeableName)
        {
            var building = Resolve(placeableName);
            if (building == null) return;

            Placed?.Invoke(originCell, building);
        }

        public void HandleRemoved(Vector3Int originCell) => Removed?.Invoke(originCell);

        public WfcBuildingDefinition Resolve(string placeableName)
        {
            if (string.IsNullOrEmpty(placeableName)) return null;

            foreach (var binding in bindings)
            {
                if (binding == null) continue;
                if (binding.placeableName == placeableName) return binding.building;
            }

            return null;
        }

        /// <summary>Diagnostico: que enlaces estan incompletos.</summary>
        public int CountIncompleteBindings()
        {
            int incomplete = 0;

            foreach (var binding in bindings)
            {
                if (binding == null || binding.building == null ||
                    string.IsNullOrWhiteSpace(binding.placeableName))
                {
                    incomplete++;
                }
            }

            return incomplete;
        }
    }
}