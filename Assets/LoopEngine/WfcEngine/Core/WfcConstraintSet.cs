using System.Collections.Generic;

namespace LoopEngine.WfcEngine.Core
{
    public readonly struct WfcConstraintResult
    {
        public readonly bool Ok;
        public readonly string Message;
        public readonly int FailedCell;

        public WfcConstraintResult(bool ok, string message = null, int failedCell = -1)
        {
            Ok = ok;
            Message = message;
            FailedCell = failedCell;
        }

        public static readonly WfcConstraintResult Success = new WfcConstraintResult(true);
    }

    /// <summary>
    /// Restricciones que se aplican al solver antes de empezar a colapsar: que puede tocar
    /// el borde del volumen y que celdas estan fijadas o limitadas.
    ///
    /// Todo se expresa como mascaras de variantes, no como casos especiales dentro del
    /// solver. El solver no sabe que existen los bordes: solo ve celdas cuyo dominio
    /// llego mas recortado. Anadir un tipo de restriccion nuevo no toca el bucle principal.
    ///
    /// Aplicar una restriccion propaga de inmediato, asi que una combinacion imposible se
    /// detecta aqui, con un mensaje que dice que celda fallo, y no como un fallo difuso a
    /// mitad del colapso.
    /// </summary>
    public sealed class WfcConstraintSet
    {
        private readonly int variantCount;
        private readonly int words;

        private readonly ulong[] boundaryMasks;      // 6 filas
        private readonly bool[] boundaryActive;

        private readonly Dictionary<int, ulong[]> cellMasks = new Dictionary<int, ulong[]>();
        private readonly Dictionary<int, int> fixedCells = new Dictionary<int, int>();
        private readonly List<KeyValuePair<int, int>> bans = new List<KeyValuePair<int, int>>();

        public int VariantCount => variantCount;
        public int WordsPerMask => words;
        public int PaintedCellCount => cellMasks.Count + fixedCells.Count;

        public WfcConstraintSet(int variantCount)
        {
            this.variantCount = variantCount;
            words = WfcBitSet.WordsFor(variantCount);

            boundaryMasks = new ulong[WfcDirections.Count * words];
            boundaryActive = new bool[WfcDirections.Count];
        }

        public ulong[] NewMask(bool filled = false)
        {
            var mask = new ulong[words];
            if (filled) WfcBitSet.FillRange(mask, 0, words, variantCount);
            return mask;
        }

        // ------------------------------------------------------------------ bordes

        /// <summary>Variantes admitidas en una celda que toca el borde por esa direccion.</summary>
        public void SetBoundaryMask(WfcDirection direction, ulong[] mask)
        {
            WfcBitSet.CopyTo(mask, 0, boundaryMasks, (int)direction * words, words);
            boundaryActive[(int)direction] = true;
        }

        public void SetBoundaryOpen(WfcDirection direction) => boundaryActive[(int)direction] = false;

        public bool IsBoundaryConstrained(WfcDirection direction) => boundaryActive[(int)direction];

        /// <summary>
        /// Mascara de las variantes cuya cara en esa direccion encaja con un socket
        /// imaginario situado fuera del volumen. Es la forma natural de decir
        /// "abajo hay suelo solido" o "arriba solo puede asomar aire".
        /// </summary>
        public static ulong[] BuildSocketBoundaryMask(
            WfcBakedModules modules, WfcDirection direction, WfcSocketDescriptor outsideSocket)
        {
            int words = WfcBitSet.WordsFor(modules.Count);
            var mask = new ulong[words];

            for (int variant = 0; variant < modules.Count; variant++)
            {
                var face = modules.GetSocket(variant, direction);
                if (WfcSocketCompatibility.AreCompatible(face, outsideSocket))
                {
                    WfcBitSet.Set(mask, 0, variant);
                }
            }

            return mask;
        }

        // ------------------------------------------------------------------ celdas

        public void AllowOnly(int cell, ulong[] mask)
        {
            var copy = NewMask();
            WfcBitSet.CopyTo(mask, 0, copy, 0, words);

            // Dos pinceladas sobre la misma celda se intersectan: la mas restrictiva manda.
            if (cellMasks.TryGetValue(cell, out var existing))
            {
                WfcBitSet.AndInto(copy, 0, existing, 0, words);
            }

            cellMasks[cell] = copy;
        }

        public void Fix(int cell, int variant) => fixedCells[cell] = variant;

        public void Ban(int cell, int variant) => bans.Add(new KeyValuePair<int, int>(cell, variant));

        public void Clear()
        {
            cellMasks.Clear();
            fixedCells.Clear();
            bans.Clear();

            for (int i = 0; i < boundaryActive.Length; i++) boundaryActive[i] = false;
        }

        // ------------------------------------------------------------------ aplicacion

        /// <summary>
        /// Orden: primero lo general (bordes), luego lo especifico (mascaras, prohibiciones,
        /// fijaciones). Al reves, una fijacion valida podria quedar anulada por un borde
        /// aplicado despues y el mensaje de error senalaria al sitio equivocado.
        /// </summary>
        public WfcConstraintResult Apply(WfcSolver solver)
        {
            var topology = solver.Topology;

            for (int d = 0; d < WfcDirections.Count; d++)
            {
                if (!boundaryActive[d]) continue;

                var direction = (WfcDirection)d;
                int offset = d * words;

                for (int cell = 0; cell < topology.CellCount; cell++)
                {
                    if (!topology.IsBoundary(cell, direction)) continue;

                    if (!solver.Restrict(cell, boundaryMasks, offset))
                    {
                        return new WfcConstraintResult(
                            false,
                            $"El borde {WfcDirections.ShortName(direction)} deja la celda {cell} " +
                            "sin ninguna variante posible.",
                            cell);
                    }
                }
            }

            foreach (var pair in cellMasks)
            {
                if (!solver.Restrict(pair.Key, pair.Value, 0))
                {
                    return new WfcConstraintResult(
                        false, $"La celda {pair.Key} queda sin variantes tras su restriccion.", pair.Key);
                }
            }

            foreach (var ban in bans)
            {
                if (!solver.Ban(ban.Key, ban.Value))
                {
                    return new WfcConstraintResult(
                        false, $"La celda {ban.Key} queda sin variantes tras prohibir la {ban.Value}.", ban.Key);
                }
            }

            foreach (var pair in fixedCells)
            {
                if (!solver.Fix(pair.Key, pair.Value))
                {
                    return new WfcConstraintResult(
                        false,
                        $"No se puede fijar la variante {pair.Value} en la celda {pair.Key}: " +
                        "ya estaba descartada por otra restriccion.",
                        pair.Key);
                }
            }

            return WfcConstraintResult.Success;
        }
    }
}