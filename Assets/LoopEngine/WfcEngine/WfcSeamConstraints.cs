using System.Collections.Generic;
using UnityEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine
{
    public enum WfcSeamFailurePolicy
    {
        /// <summary>La cara queda libre: la costura no casara, pero el edificio se resuelve.</summary>
        LeaveOpen = 0,

        /// <summary>Se usan los modulos de reserva declarados.</summary>
        UseFallback = 1,

        /// <summary>Se aborta el volumen entero.</summary>
        Fail = 2
    }

    /// <summary>
    /// Costura entre edificios: cada celda del borde se limita a las variantes cuya cara
    /// encaja con lo que el vecino ya resuelto presenta enfrente.
    ///
    /// Es la mitad inter-edificio del sistema. La intra-edificio (sesiones 6 a 8) puede
    /// retroceder y reintentar; esta no, porque el vecino ya esta construido y no se le va
    /// a pedir que cambie. Por eso aqui hay politica de fallo y alli no: cuando ninguna
    /// variante encaja con la fachada del vecino, hay que decidir entre dejar la junta
    /// abierta, tapar con un modulo de reserva, o abortar.
    /// </summary>
    [AddComponentMenu("LoopEngine/WFC/Seam Constraints")]
    [RequireComponent(typeof(WfcRunner))]
    public class WfcSeamConstraints : MonoBehaviour, IWfcConstraintSource
    {
        [Tooltip("Celda de origen de este volumen en coordenadas de ciudad.")]
        [SerializeField] private Vector3Int originCell;

        [SerializeField] private WfcSeamFailurePolicy onNoMatch = WfcSeamFailurePolicy.LeaveOpen;

        [Tooltip("Modulos de reserva para juntas imposibles. Solo se usan con UseFallback.")]
        [SerializeField] private List<WfcModuleDefinition> fallbackModules = new List<WfcModuleDefinition>();

        [Tooltip("Direcciones que se cosen. Lo habitual es solo las horizontales.")]
        [SerializeField] private bool seamHorizontal = true;
        [SerializeField] private bool seamVertical;

        private IWfcFacadeProvider provider;

        public Vector3Int OriginCell { get => originCell; set => originCell = value; }
        public int ConstrainedFaces { get; private set; }
        public int OpenSeams { get; private set; }

        /// <summary>Lo inyecta el distrito antes de preparar. Sin proveedor, no hay costura.</summary>
        public void SetFacadeProvider(IWfcFacadeProvider value) => provider = value;

        public bool ApplyConstraints(WfcRunner runner, out string failure)
        {
            failure = null;
            ConstrainedFaces = 0;
            OpenSeams = 0;

            if (provider == null || runner == null || !runner.IsPrepared) return true;

            var baked = runner.Adjacency.Baked;
            var topology = runner.Solver.Topology;
            var constraints = new WfcConstraintSet(baked.Count);

            var fallbackMask = BuildFallbackMask(baked, constraints);

            for (int cell = 0; cell < topology.CellCount; cell++)
            {
                topology.CoordsOf(cell, out int x, out int y, out int z);
                var worldCell = originCell + new Vector3Int(x, y, z);

                for (int d = 0; d < WfcDirections.Count; d++)
                {
                    var direction = (WfcDirection)d;

                    if (WfcDirections.IsHorizontal(direction) && !seamHorizontal) continue;
                    if (WfcDirections.IsVertical(direction) && !seamVertical) continue;

                    // Solo importan las caras que dan al exterior de este volumen.
                    if (!topology.IsBoundary(cell, direction)) continue;

                    if (!provider.TryGetFacadeSocket(worldCell, direction, out var neighborSocket)) continue;

                    var mask = WfcConstraintSet.BuildSocketBoundaryMask(
                        baked.Core, direction, neighborSocket);

                    if (WfcBitSet.IsEmpty(mask, 0, constraints.WordsPerMask))
                    {
                        if (!HandleNoMatch(direction, worldCell, fallbackMask, constraints, cell, out failure))
                        {
                            return false;
                        }

                        continue;
                    }

                    constraints.AllowOnly(cell, mask);
                    ConstrainedFaces++;
                }
            }

            var result = constraints.Apply(runner.Solver);

            if (!result.Ok)
            {
                failure = "Costura imposible: " + result.Message;
                return false;
            }

            return true;
        }

        private bool HandleNoMatch(
            WfcDirection direction,
            Vector3Int worldCell,
            ulong[] fallbackMask,
            WfcConstraintSet constraints,
            int cell,
            out string failure)
        {
            failure = null;
            OpenSeams++;

            switch (onNoMatch)
            {
                case WfcSeamFailurePolicy.UseFallback:
                    if (fallbackMask != null) constraints.AllowOnly(cell, fallbackMask);
                    return true;

                case WfcSeamFailurePolicy.Fail:
                    failure =
                        $"Ninguna variante encaja con la fachada del vecino en {worldCell} " +
                        $"por {WfcDirections.ShortName(direction)}.";
                    return false;

                default:
                    // LeaveOpen: no se anade restriccion. La junta no casara y el contador
                    // OpenSeams lo dice, en vez de fallar todo el edificio por una cara.
                    return true;
            }
        }

        private ulong[] BuildFallbackMask(WfcBakedSet baked, WfcConstraintSet constraints)
        {
            if (fallbackModules.Count == 0) return null;

            var mask = constraints.NewMask();
            bool any = false;

            for (int variant = 0; variant < baked.Count; variant++)
            {
                var module = baked.GetView(variant).Module;
                if (module == null || !fallbackModules.Contains(module)) continue;

                WfcBitSet.Set(mask, 0, variant);
                any = true;
            }

            return any ? mask : null;
        }
    }
}