using UnityEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine
{
    /// <summary>
    /// Que es un edificio para el WFC: con que set se resuelve y que volumen ocupa.
    /// Es lo que un sistema de colocacion (GridEngine, un editor, un generador de barrios)
    /// necesita conocer, y no incluye nada de como se coloca ni quien lo coloca.
    /// </summary>
    [CreateAssetMenu(fileName = "WfcBuilding", menuName = "LoopEngine/WFC/Building Definition", order = 4)]
    public class WfcBuildingDefinition : ScriptableObject
    {
        [SerializeField] private WfcModuleSet moduleSet;
        [SerializeField] private WfcExclusionSet exclusions;

        [Tooltip("Volumen en celdas. X y Z son la huella en la ciudad; Y es la altura.")]
        [SerializeField] private Vector3Int size = new Vector3Int(3, 4, 3);

        [SerializeField] private WfcHeuristic heuristic = WfcHeuristic.ShannonEntropy;
        [SerializeField, Min(0)] private int maxBacktracks = 10000;

        [SerializeField, TextArea(1, 3)] private string notes = string.Empty;

        public WfcModuleSet ModuleSet => moduleSet;
        public WfcExclusionSet Exclusions => exclusions;
        public Vector3Int Size => size;
        public WfcHeuristic Heuristic => heuristic;
        public int MaxBacktracks => maxBacktracks;
        public string Notes => notes;

        public Vector2Int Footprint => new Vector2Int(size.x, size.z);

        public float CellSize => moduleSet != null ? moduleSet.CellSize : 1f;

        private void OnValidate()
        {
            size.x = Mathf.Max(1, size.x);
            size.y = Mathf.Max(1, size.y);
            size.z = Mathf.Max(1, size.z);
        }
    }
}