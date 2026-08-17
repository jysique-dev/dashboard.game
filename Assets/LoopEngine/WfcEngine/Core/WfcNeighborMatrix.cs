using System;

namespace LoopEngine.WfcEngine.Core
{
    /// <summary>
    /// Para cada direccion y cada variante, el conjunto de variantes que pueden ocupar
    /// la celda vecina. Es la estructura sobre la que propaga el solver: intersectar el
    /// dominio de una celda con una fila de esta matriz es un AND de palabras.
    ///
    /// Se construye simetricamente por pares: al permitir (a -> b en d) se permite tambien
    /// (b -> a en opuesta(d)). La reciprocidad queda garantizada por construccion, no por
    /// una comprobacion posterior; una matriz no reciproca produce colapsos que dependen
    /// del orden de propagacion y son practicamente imposibles de depurar.
    /// </summary>
    public sealed class WfcNeighborMatrix
    {
        private readonly ulong[] allowed;   // [direccion][variante] -> bitset de vecinos
        private readonly int words;

        public int VariantCount { get; }
        public int WordsPerRow => words;

        /// <summary>Array crudo. Expuesto para que el solver haga AND sin copiar filas.</summary>
        public ulong[] Data => allowed;

        private WfcNeighborMatrix(int variantCount)
        {
            VariantCount = variantCount;
            words = WfcBitSet.WordsFor(variantCount);
            allowed = new ulong[WfcDirections.Count * variantCount * words];
        }

        /// <summary>Offset de la fila (direccion, variante) dentro de Data.</summary>
        public int RowOffset(WfcDirection direction, int variantIndex)
            => (((int)direction * VariantCount) + variantIndex) * words;

        public bool IsAllowed(WfcDirection direction, int variantA, int variantB)
            => WfcBitSet.Get(allowed, RowOffset(direction, variantA), variantB);

        public int CountAllowed(WfcDirection direction, int variantIndex)
            => WfcBitSet.PopCount(allowed, RowOffset(direction, variantIndex), words);

        /// <summary>Intersecta el dominio dado con los vecinos permitidos. true si cambio.</summary>
        public bool ConstrainInto(
            ulong[] domain, int domainOffset, WfcDirection direction, int variantIndex)
            => WfcBitSet.AndInto(domain, domainOffset, allowed, RowOffset(direction, variantIndex), words);

        public static WfcNeighborMatrix Build(WfcBakedModules modules, IWfcAdjacencyFilter filter = null)
        {
            if (modules == null) throw new ArgumentNullException(nameof(modules));

            int count = modules.Count;
            var matrix = new WfcNeighborMatrix(count);

            // Solo se recorren tres ejes: el cuarto, quinto y sexto salen por simetria.
            var axes = new[] { WfcDirection.Right, WfcDirection.Up, WfcDirection.Forward };

            foreach (var direction in axes)
            {
                var opposite = WfcDirections.Opposite(direction);

                for (int a = 0; a < count; a++)
                {
                    var socketA = modules.GetSocket(a, direction);
                    if (!socketA.IsValid) continue;

                    for (int b = 0; b < count; b++)
                    {
                        var socketB = modules.GetSocket(b, opposite);
                        if (!WfcSocketCompatibility.AreCompatible(socketA, socketB)) continue;

                        if (filter != null)
                        {
                            if (filter.IsForbidden(a, b, direction)) continue;
                            if (filter.IsForbidden(b, a, opposite)) continue;
                        }

                        WfcBitSet.Set(matrix.allowed, matrix.RowOffset(direction, a), b);
                        WfcBitSet.Set(matrix.allowed, matrix.RowOffset(opposite, b), a);
                    }
                }
            }

            return matrix;
        }

        /// <summary>
        /// Comprobacion de la invariante que el constructor deberia garantizar.
        /// Existe para los tests: si alguna vez falla, la construccion se rompio.
        /// </summary>
        public bool IsReciprocal()
        {
            for (int d = 0; d < WfcDirections.Count; d++)
            {
                var direction = (WfcDirection)d;
                var opposite = WfcDirections.Opposite(direction);

                for (int a = 0; a < VariantCount; a++)
                {
                    for (int b = 0; b < VariantCount; b++)
                    {
                        if (IsAllowed(direction, a, b) != IsAllowed(opposite, b, a)) return false;
                    }
                }
            }

            return true;
        }

        /// <summary>Densidad: fraccion de pares permitidos sobre el total posible.</summary>
        public float Density()
        {
            long total = (long)VariantCount * VariantCount * WfcDirections.Count;
            if (total == 0) return 0f;

            long allowedPairs = 0;
            for (int d = 0; d < WfcDirections.Count; d++)
            {
                for (int a = 0; a < VariantCount; a++)
                {
                    allowedPairs += CountAllowed((WfcDirection)d, a);
                }
            }

            return (float)((double)allowedPairs / total);
        }
    }
}