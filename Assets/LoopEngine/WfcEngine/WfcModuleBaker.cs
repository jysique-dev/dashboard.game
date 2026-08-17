using System.Collections.Generic;
using UnityEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine
{
    /// <summary>
    /// Puente entre la autoria (assets) y el nucleo puro: lee los WfcModuleDefinition de
    /// un set, resuelve sus 6 sockets y delega el giro en WfcVariantBaker.
    ///
    /// Los modulos con errores de validacion se excluyen del horneado en vez de colarse
    /// con sockets invalidos: un socket Invalid no encaja con nada, asi que un modulo roto
    /// dentro de la tabla envenenaria el solver de forma dificil de rastrear.
    /// </summary>
    public static class WfcModuleBaker
    {
        public static WfcBakedSet Bake(
            WfcModuleSet set,
            bool dropDuplicateRotations = true,
            List<WfcValidationIssue> issues = null,
            List<WfcBakeReportEntry> report = null)
        {
            if (set == null)
            {
                issues?.Add(new WfcValidationIssue(WfcIssueSeverity.Error, "No hay set que hornear."));
                return null;
            }

            var inputs = new List<WfcModuleBakeInput>(set.Count);
            var accepted = new List<WfcModuleDefinition>(set.Count);

            var moduleIssues = new List<WfcValidationIssue>();

            foreach (var module in set.Modules)
            {
                if (module == null) continue;

                moduleIssues.Clear();
                WfcModuleValidation.Validate(module, moduleIssues);

                if (WfcModuleValidation.CountBySeverity(moduleIssues, WfcIssueSeverity.Error) > 0)
                {
                    issues?.Add(new WfcValidationIssue(
                        WfcIssueSeverity.Error,
                        $"'{module.name}' excluido del horneado: tiene errores de autoria.",
                        module));

                    if (issues != null) issues.AddRange(moduleIssues);
                    continue;
                }

                if (issues != null) issues.AddRange(moduleIssues);

                inputs.Add(new WfcModuleBakeInput(
                    module.ResolveDescriptors(),
                    module.AllowedRotations,
                    module.Weight));

                accepted.Add(module);
            }

            var core = WfcVariantBaker.Bake(inputs, dropDuplicateRotations, report);

            var views = new WfcBakedVariant[core.Count];
            for (int i = 0; i < core.Count; i++)
            {
                var variant = core[i];
                views[i] = new WfcBakedVariant(accepted[variant.SourceIndex], variant.RotationSteps);
            }

            if (core.Count == 0)
            {
                issues?.Add(new WfcValidationIssue(
                    WfcIssueSeverity.Error, "El horneado no produjo ninguna variante.", set));
            }

            return new WfcBakedSet(set, core, views);
        }

        /// <summary>Cuantas variantes se descartaron por duplicadas, segun el informe.</summary>
        public static int CountDropped(List<WfcBakeReportEntry> report)
        {
            if (report == null) return 0;

            int dropped = 0;
            for (int i = 0; i < report.Count; i++)
            {
                if (!report[i].Kept) dropped++;
            }
            return dropped;
        }
    }
}