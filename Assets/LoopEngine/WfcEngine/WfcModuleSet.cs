using System;
using System.Collections.Generic;
using UnityEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine
{
    /// <summary>
    /// Conjunto de modulos que comparten libreria y se resuelven juntos.
    /// Un set = un estilo (ciudad tradicional, ciudad moderna, interior de fabrica...).
    ///
    /// El set no asigna ids a los modulos: cada modulo es un asset y su referencia ya
    /// es estable. Los indices que use el solver se derivan en el horneado y nunca se
    /// serializan.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WfcModuleSet",
        menuName = "LoopEngine/WFC/Module Set",
        order = 2)]
    public class WfcModuleSet : ScriptableObject
    {
        [SerializeField] private WfcSocketLibrary library;
        [SerializeField] private List<WfcModuleDefinition> modules = new List<WfcModuleDefinition>();

        [Tooltip("Tamano de celda en unidades de mundo. Todos los prefabs deben ocupar este cubo.")]
        [SerializeField, Min(0.01f)] private float cellSize = 1f;

        [SerializeField, TextArea(1, 4)] private string notes = string.Empty;

        /// <summary>Cambio en editor. Para refresco en vivo de la galeria y herramientas.</summary>
        public event Action Changed;

        public WfcSocketLibrary Library => library;
        public IReadOnlyList<WfcModuleDefinition> Modules => modules;
        public float CellSize => cellSize;
        public string Notes => notes;

        public int Count => modules.Count;

        public bool Contains(WfcModuleDefinition module) => modules.Contains(module);

        public bool Add(WfcModuleDefinition module)
        {
            if (module == null || modules.Contains(module)) return false;

            modules.Add(module);
            RaiseChanged();
            return true;
        }

        public bool Remove(WfcModuleDefinition module)
        {
            if (!modules.Remove(module)) return false;

            RaiseChanged();
            return true;
        }

        /// <summary>
        /// Cuantas variantes producira el horneado de la sesion 3: la suma de rotaciones
        /// permitidas. Sirve para ver de un vistazo el tamano real del problema.
        /// </summary>
        public int CountBakedVariants()
        {
            int total = 0;
            foreach (var module in modules)
            {
                if (module == null) continue;
                total += WfcRotationMasks.Count(module.AllowedRotations);
            }
            return total;
        }

        public void RaiseChanged() => Changed?.Invoke();

        private void OnValidate()
        {
            if (cellSize < 0.01f) cellSize = 0.01f;
            RaiseChanged();
        }
    }
}