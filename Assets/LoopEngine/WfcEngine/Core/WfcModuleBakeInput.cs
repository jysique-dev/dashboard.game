using System;
using System.Collections.Generic;

namespace LoopEngine.WfcEngine.Core
{
    /// <summary>Datos de autoria de un modulo, ya resueltos y sin Unity de por medio.</summary>
    public readonly struct WfcModuleBakeInput
    {
        public readonly WfcSocketDescriptor[] Faces;
        public readonly WfcRotationMask Rotations;
        public readonly float Weight;

        public WfcModuleBakeInput(WfcSocketDescriptor[] faces, WfcRotationMask rotations, float weight)
        {
            if (faces == null || faces.Length != WfcDirections.Count)
            {
                throw new ArgumentException(
                    $"Un modulo necesita exactamente {WfcDirections.Count} caras.", nameof(faces));
            }

            Faces = faces;
            Rotations = rotations;
            Weight = weight;
        }
    }

    /// <summary>Que paso con cada rotacion pedida. Para que el editor pueda explicarlo.</summary>
    public readonly struct WfcBakeReportEntry
    {
        public readonly int SourceIndex;
        public readonly int RotationSteps;
        public readonly bool Kept;
        public readonly int DuplicateOfSteps;

        public WfcBakeReportEntry(int sourceIndex, int rotationSteps, bool kept, int duplicateOfSteps)
        {
            SourceIndex = sourceIndex;
            RotationSteps = rotationSteps;
            Kept = kept;
            DuplicateOfSteps = duplicateOfSteps;
        }
    }

    /// <summary>
    /// Convierte modulos de autoria en variantes giradas.
    ///
    /// El descarte de duplicados es por modulo, nunca entre modulos distintos: dos modulos
    /// pueden tener sockets identicos y ser geometrias diferentes, y ambos deben sobrevivir.
    /// Dentro de un mismo modulo, en cambio, un cubo solido girado 90 grados es literalmente
    /// la misma variante, y mantener las cuatro solo infla la matriz de vecindad y sesga el
    /// muestreo ponderado hacia ese modulo.
    /// </summary>
    public static class WfcVariantBaker
    {
        public static WfcBakedModules Bake(
            IReadOnlyList<WfcModuleBakeInput> inputs,
            bool dropDuplicateRotations = true,
            List<WfcBakeReportEntry> report = null)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));

            var variants = new List<WfcModuleVariant>();
            var offsets = new int[inputs.Count + 1];

            var perModule = new List<WfcModuleVariant>(WfcRotationMasks.StepCount);

            for (int sourceIndex = 0; sourceIndex < inputs.Count; sourceIndex++)
            {
                offsets[sourceIndex] = variants.Count;

                var input = inputs[sourceIndex];
                perModule.Clear();

                foreach (int steps in WfcRotationMasks.Steps(input.Rotations))
                {
                    var faces = WfcModuleVariant.RotateFaces(input.Faces, steps);
                    var candidate = new WfcModuleVariant(sourceIndex, steps, input.Weight, faces);

                    int duplicateOf = -1;
                    if (dropDuplicateRotations)
                    {
                        for (int i = 0; i < perModule.Count; i++)
                        {
                            if (perModule[i].HasSameFaces(candidate))
                            {
                                duplicateOf = perModule[i].RotationSteps;
                                break;
                            }
                        }
                    }

                    if (duplicateOf < 0)
                    {
                        perModule.Add(candidate);
                        variants.Add(candidate);
                    }

                    report?.Add(new WfcBakeReportEntry(sourceIndex, steps, duplicateOf < 0, duplicateOf));
                }
            }

            offsets[inputs.Count] = variants.Count;

            return new WfcBakedModules(variants.ToArray(), offsets);
        }
    }
}