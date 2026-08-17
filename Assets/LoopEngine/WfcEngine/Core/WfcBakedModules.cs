using System.Collections.Generic;

namespace LoopEngine.WfcEngine.Core
{
    /// <summary>
    /// Tabla de variantes horneadas. Es la entrada del solver y, en la sesion 4,
    /// el dominio sobre el que se construye la matriz de vecindad.
    ///
    /// Las variantes de un mismo modulo son contiguas, asi que el rango por modulo
    /// se consulta en tiempo constante sin diccionarios.
    /// </summary>
    public sealed class WfcBakedModules
    {
        private readonly WfcModuleVariant[] variants;
        private readonly int[] sourceOffsets;   // longitud sourceCount + 1
        private readonly float[] weights;

        public int Count => variants.Length;
        public int SourceCount => sourceOffsets.Length - 1;

        internal WfcBakedModules(WfcModuleVariant[] variants, int[] sourceOffsets)
        {
            this.variants = variants;
            this.sourceOffsets = sourceOffsets;

            weights = new float[variants.Length];
            for (int i = 0; i < variants.Length; i++) weights[i] = variants[i].Weight;
        }

        public WfcModuleVariant this[int variantIndex] => variants[variantIndex];

        public WfcSocketDescriptor GetSocket(int variantIndex, WfcDirection direction)
            => variants[variantIndex].GetSocket(direction);

        public float GetWeight(int variantIndex) => weights[variantIndex];

        /// <summary>Suma de pesos de todas las variantes. Util para muestreo ponderado.</summary>
        public float TotalWeight()
        {
            float total = 0f;
            for (int i = 0; i < weights.Length; i++) total += weights[i];
            return total;
        }

        /// <summary>Rango contiguo de variantes que salieron del modulo de autoria dado.</summary>
        public void GetVariantRange(int sourceIndex, out int start, out int count)
        {
            start = sourceOffsets[sourceIndex];
            count = sourceOffsets[sourceIndex + 1] - start;
        }

        public IEnumerable<int> VariantsOf(int sourceIndex)
        {
            GetVariantRange(sourceIndex, out int start, out int count);
            for (int i = 0; i < count; i++) yield return start + i;
        }

        public IReadOnlyList<WfcModuleVariant> Variants => variants;
    }
}