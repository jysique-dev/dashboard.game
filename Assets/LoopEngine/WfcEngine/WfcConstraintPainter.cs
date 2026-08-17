using System;
using System.Collections.Generic;
using UnityEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine
{
    public enum WfcBoundaryMode
    {
        /// <summary>Sin restriccion: la cara exterior admite cualquier variante.</summary>
        Open = 0,

        /// <summary>Fuera hay un socket imaginario; solo entran las caras que encajen con el.</summary>
        RequireSocket = 1,

        /// <summary>Solo estos modulos pueden tocar el borde por esa cara.</summary>
        AllowOnlyModules = 2
    }

    public enum WfcPaintMode
    {
        /// <summary>Solo estos modulos, en cualquiera de sus rotaciones permitidas.</summary>
        AllowOnly = 0,

        /// <summary>Estos modulos quedan prohibidos en la celda.</summary>
        Ban = 1
    }

    [Serializable]
    public class WfcBoundaryRule
    {
        public WfcDirection direction;
        public WfcBoundaryMode mode = WfcBoundaryMode.Open;

        [Tooltip("Socket que se supone al otro lado del borde.")]
        public WfcSocketRef outsideSocket;

        public List<WfcModuleDefinition> modules = new List<WfcModuleDefinition>();
    }

    [Serializable]
    public class WfcPaintedCell
    {
        public Vector3Int cell;
        public WfcPaintMode mode = WfcPaintMode.AllowOnly;
        public List<WfcModuleDefinition> modules = new List<WfcModuleDefinition>();

        [Tooltip("-1 = cualquier rotacion. 0..3 = solo esa.")]
        public int rotationSteps = -1;
    }

    /// <summary>
    /// Restricciones de un volumen concreto: que puede tocar cada borde y que celdas estan
    /// limitadas a mano. Se cuelga junto al WfcRunner y este lo encuentra por la interfaz.
    ///
    /// Las reglas se escriben en terminos de modulos de autoria, no de variantes horneadas:
    /// "abajo solo cimientos" no deberia romperse porque una rotacion se descarte por
    /// duplicada. La traduccion a variantes se hace al aplicar.
    /// </summary>
    [AddComponentMenu("LoopEngine/WFC/Constraint Painter")]
    [RequireComponent(typeof(WfcRunner))]
    public class WfcConstraintPainter : MonoBehaviour, IWfcConstraintSource
    {
        [SerializeField] private List<WfcBoundaryRule> boundaries = new List<WfcBoundaryRule>();
        [SerializeField] private List<WfcPaintedCell> painted = new List<WfcPaintedCell>();

        [Header("Gizmos")]
        [SerializeField] private bool drawPainted = true;

        public IReadOnlyList<WfcBoundaryRule> Boundaries => boundaries;
        public IReadOnlyList<WfcPaintedCell> Painted => painted;

        public WfcBoundaryRule GetBoundary(WfcDirection direction)
        {
            EnsureBoundaries();
            return boundaries[(int)direction];
        }

        public WfcPaintedCell Find(Vector3Int cell)
        {
            for (int i = 0; i < painted.Count; i++)
            {
                if (painted[i] != null && painted[i].cell == cell) return painted[i];
            }
            return null;
        }

        public WfcPaintedCell PaintCell(Vector3Int cell, WfcPaintMode mode, WfcModuleDefinition module, int rotationSteps = -1)
        {
            var entry = Find(cell);

            if (entry == null)
            {
                entry = new WfcPaintedCell { cell = cell, mode = mode, rotationSteps = rotationSteps };
                painted.Add(entry);
            }

            entry.mode = mode;
            entry.rotationSteps = rotationSteps;

            if (module != null && !entry.modules.Contains(module)) entry.modules.Add(module);

            return entry;
        }

        public bool Erase(Vector3Int cell)
        {
            var entry = Find(cell);
            if (entry == null) return false;

            painted.Remove(entry);
            return true;
        }

        public void ClearLayer(int y)
        {
            painted.RemoveAll(entry => entry != null && entry.cell.y == y);
        }

        public void ClearAll() => painted.Clear();

        // ------------------------------------------------------------------ aplicacion

        public bool ApplyConstraints(WfcRunner runner, out string failure)
        {
            failure = null;

            if (runner == null || !runner.IsPrepared) return true;

            var baked = runner.Adjacency.Baked;
            var constraints = new WfcConstraintSet(baked.Count);

            var variantsOfModule = BuildModuleIndex(baked);

            EnsureBoundaries();

            foreach (var rule in boundaries)
            {
                if (rule == null || rule.mode == WfcBoundaryMode.Open) continue;

                var mask = BuildBoundaryMask(rule, baked, constraints, variantsOfModule);
                if (mask == null)
                {
                    failure = $"La regla de borde {WfcDirections.ShortName(rule.direction)} " +
                              "no tiene datos suficientes.";
                    return false;
                }

                constraints.SetBoundaryMask(rule.direction, mask);
            }

            var topology = runner.Solver.Topology;

            foreach (var entry in painted)
            {
                if (entry == null || entry.modules.Count == 0) continue;
                if (!topology.Contains(entry.cell.x, entry.cell.y, entry.cell.z)) continue;

                int cell = topology.Index(entry.cell.x, entry.cell.y, entry.cell.z);
                var mask = BuildModuleMask(entry, constraints, variantsOfModule, baked);

                if (entry.mode == WfcPaintMode.AllowOnly) constraints.AllowOnly(cell, mask);
                else
                {
                    for (int v = 0; v < baked.Count; v++)
                    {
                        if (WfcBitSet.Get(mask, 0, v)) constraints.Ban(cell, v);
                    }
                }
            }

            var result = constraints.Apply(runner.Solver);

            if (!result.Ok)
            {
                failure = result.Message;
                return false;
            }

            return true;
        }

        private static Dictionary<WfcModuleDefinition, List<int>> BuildModuleIndex(WfcBakedSet baked)
        {
            var map = new Dictionary<WfcModuleDefinition, List<int>>();

            for (int variant = 0; variant < baked.Count; variant++)
            {
                var module = baked.GetView(variant).Module;
                if (module == null) continue;

                if (!map.TryGetValue(module, out var list))
                {
                    list = new List<int>();
                    map[module] = list;
                }

                list.Add(variant);
            }

            return map;
        }

        private static ulong[] BuildBoundaryMask(
            WfcBoundaryRule rule,
            WfcBakedSet baked,
            WfcConstraintSet constraints,
            Dictionary<WfcModuleDefinition, List<int>> variantsOfModule)
        {
            if (rule.mode == WfcBoundaryMode.RequireSocket)
            {
                if (!rule.outsideSocket.TryResolve(out var socket)) return null;
                return WfcConstraintSet.BuildSocketBoundaryMask(baked.Core, rule.direction, socket);
            }

            if (rule.modules.Count == 0) return null;

            var mask = constraints.NewMask();

            foreach (var module in rule.modules)
            {
                if (module == null || !variantsOfModule.TryGetValue(module, out var list)) continue;
                foreach (int variant in list) WfcBitSet.Set(mask, 0, variant);
            }

            return mask;
        }

        /// <summary>
        /// Mascara de las variantes de los modulos pintados. Si la celda pide una rotacion
        /// concreta y esa rotacion no existe (se descarto por duplicada en el horneado), la
        /// mascara sale vacia y la restriccion falla con un mensaje claro. Es preferible a
        /// caer en otra rotacion parecida sin avisar.
        /// </summary>
        private static ulong[] BuildModuleMask(
            WfcPaintedCell entry,
            WfcConstraintSet constraints,
            Dictionary<WfcModuleDefinition, List<int>> variantsOfModule,
            WfcBakedSet baked)
        {
            var mask = constraints.NewMask();

            foreach (var module in entry.modules)
            {
                if (module == null || !variantsOfModule.TryGetValue(module, out var list)) continue;

                foreach (int variant in list)
                {
                    if (entry.rotationSteps >= 0 &&
                        baked.GetView(variant).RotationSteps != entry.rotationSteps)
                    {
                        continue;
                    }

                    WfcBitSet.Set(mask, 0, variant);
                }
            }

            return mask;
        }

        private void EnsureBoundaries()
        {
            if (boundaries.Count == WfcDirections.Count) return;

            var rebuilt = new List<WfcBoundaryRule>(WfcDirections.Count);

            for (int i = 0; i < WfcDirections.Count; i++)
            {
                var direction = (WfcDirection)i;
                var existing = boundaries.Find(rule => rule != null && rule.direction == direction);

                rebuilt.Add(existing ?? new WfcBoundaryRule { direction = direction });
            }

            boundaries = rebuilt;
        }

        private void OnValidate() => EnsureBoundaries();

        private void OnDrawGizmos()
        {
            if (!drawPainted || painted.Count == 0) return;

            var runner = GetComponent<WfcRunner>();
            if (runner == null || runner.ModuleSet == null) return;

            float cell = runner.ModuleSet.CellSize;
            var space = runner.IsPrepared ? runner.Space : default;

            foreach (var entry in painted)
            {
                if (entry == null) continue;

                Gizmos.color = entry.mode == WfcPaintMode.AllowOnly
                    ? new Color(0.3f, 0.9f, 0.5f, 0.7f)
                    : new Color(0.95f, 0.35f, 0.3f, 0.7f);

                var position = runner.IsPrepared
                    ? space.CellToWorld(entry.cell.x, entry.cell.y, entry.cell.z)
                    : transform.position + (Vector3)entry.cell * cell;

                Gizmos.DrawWireCube(position, Vector3.one * cell * 0.9f);
            }
        }
    }
}