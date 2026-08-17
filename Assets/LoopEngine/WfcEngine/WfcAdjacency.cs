using System.Collections.Generic;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine
{
    /// <summary>
    /// Resultado completo de preparar un set: variantes horneadas, matriz de vecindad y
    /// diagnostico. Es lo unico que necesita el solver de la sesion 6.
    /// </summary>
    public sealed class WfcAdjacency
    {
        public WfcBakedSet Baked { get; }
        public WfcNeighborMatrix Matrix { get; }
        public WfcExclusionFilter Filter { get; }

        public List<WfcDeadVariant> DeadVariants { get; } = new List<WfcDeadVariant>();
        public List<WfcUnmatchedSocket> UnmatchedSockets { get; } = new List<WfcUnmatchedSocket>();
        public List<WfcValidationIssue> Issues { get; } = new List<WfcValidationIssue>();

        public int VariantCount => Baked != null ? Baked.Count : 0;

        internal WfcAdjacency(WfcBakedSet baked, WfcNeighborMatrix matrix, WfcExclusionFilter filter)
        {
            Baked = baked;
            Matrix = matrix;
            Filter = filter;
        }

        public float Density() => Matrix != null ? Matrix.Density() : 0f;

        public float AverageOptionsPerFace()
            => Matrix != null ? WfcMatrixDiagnostics.AverageOptionsPerFace(Matrix) : 0f;
    }

    /// <summary>
    /// Punto de entrada unico: de un WfcModuleSet a todo lo que el solver necesita.
    /// Editor y runtime llaman aqui, de modo que la matriz que ves en el heatmap es
    /// literalmente la que usara el solver, no una reconstruccion parecida.
    /// </summary>
    public static class WfcAdjacencyBuilder
    {
        public static WfcAdjacency Build(
            WfcModuleSet set,
            WfcExclusionSet exclusions = null,
            bool dropDuplicateRotations = true)
        {
            var issues = new List<WfcValidationIssue>();
            var baked = WfcModuleBaker.Bake(set, dropDuplicateRotations, issues);

            if (baked == null)
            {
                var empty = new WfcAdjacency(null, null, null);
                empty.Issues.AddRange(issues);
                return empty;
            }

            if (exclusions != null && exclusions.ModuleSet != null && exclusions.ModuleSet != set)
            {
                issues.Add(new WfcValidationIssue(
                    WfcIssueSeverity.Warning,
                    "El set de exclusiones apunta a otro set de modulos.",
                    exclusions));
            }

            var filter = WfcExclusionFilter.Build(baked, exclusions, issues);
            var matrix = WfcNeighborMatrix.Build(baked.Core, filter);

            var result = new WfcAdjacency(baked, matrix, filter);
            result.Issues.AddRange(issues);

            WfcMatrixDiagnostics.FindDeadVariants(matrix, result.DeadVariants);
            WfcMatrixDiagnostics.FindUnmatchedSockets(baked.Core, result.UnmatchedSockets);

            foreach (var dead in result.DeadVariants)
            {
                var view = baked.GetView(dead.VariantIndex);
                result.Issues.Add(new WfcValidationIssue(
                    WfcIssueSeverity.Error,
                    $"'{view.DisplayName}' no tiene ningun vecino posible en " +
                    $"{WfcDirections.ShortName(dead.Direction)}: nunca podra colocarse.",
                    view.Module));
            }

            return result;
        }
    }
}