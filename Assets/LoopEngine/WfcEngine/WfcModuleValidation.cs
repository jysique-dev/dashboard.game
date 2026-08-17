using System.Collections.Generic;
using UnityEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine
{
    public enum WfcIssueSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public readonly struct WfcValidationIssue
    {
        public readonly WfcIssueSeverity Severity;
        public readonly string Message;
        public readonly Object Context;

        public WfcValidationIssue(WfcIssueSeverity severity, string message, Object context = null)
        {
            Severity = severity;
            Message = message;
            Context = context;
        }

        public override string ToString() => $"[{Severity}] {Message}";
    }

    /// <summary>
    /// Validacion de autoria compartida por inspectores y galeria. Vive en runtime, no en
    /// Editor/, para que exista una sola definicion de "modulo bien formado" y cualquier
    /// herramienta futura (o un test) pueda preguntarla.
    ///
    /// Alcance de la sesion 2: coherencia interna de cada modulo y del set. La deteccion
    /// de modulos sin vecinos posibles necesita la matriz de adyacencia y llega en la sesion 4.
    /// </summary>
    public static class WfcModuleValidation
    {
        public static void Validate(WfcModuleDefinition module, List<WfcValidationIssue> results)
        {
            if (module == null)
            {
                results.Add(new WfcValidationIssue(WfcIssueSeverity.Error, "Modulo nulo."));
                return;
            }

            if (module.Library == null)
            {
                results.Add(new WfcValidationIssue(
                    WfcIssueSeverity.Error, $"'{module.name}': sin libreria de sockets.", module));
                return;
            }

            for (int i = 0; i < WfcDirections.Count; i++)
            {
                var direction = (WfcDirection)i;
                var face = module.GetFace(direction);
                string faceName = WfcDirections.ShortName(direction);

                if (!face.IsAssigned)
                {
                    results.Add(new WfcValidationIssue(
                        WfcIssueSeverity.Error, $"'{module.name}': cara {faceName} sin socket.", module));
                    continue;
                }

                if (face.library != module.Library)
                {
                    results.Add(new WfcValidationIssue(
                        WfcIssueSeverity.Warning,
                        $"'{module.name}': la cara {faceName} apunta a otra libreria.", module));
                }

                if (!face.IsResolvable)
                {
                    results.Add(new WfcValidationIssue(
                        WfcIssueSeverity.Error,
                        $"'{module.name}': cara {faceName} apunta a un socket borrado (#{face.socketId}).", module));
                    continue;
                }

                var descriptor = face.Descriptor;
                if (!WfcSocketKinds.FitsDirection(descriptor.Kind, direction))
                {
                    string expected = WfcDirections.IsHorizontal(direction) ? "horizontal" : "vertical";
                    results.Add(new WfcValidationIssue(
                        WfcIssueSeverity.Error,
                        $"'{module.name}': la cara {faceName} necesita un socket {expected}, " +
                        $"pero tiene {WfcSocketKinds.ShortLabel(descriptor.Kind)}.", module));
                }
            }

            if (module.AllowedRotations == WfcRotationMask.None)
            {
                results.Add(new WfcValidationIssue(
                    WfcIssueSeverity.Warning,
                    $"'{module.name}': sin rotaciones permitidas, no se usara nunca.", module));
            }

            if (module.Weight <= 0f)
            {
                results.Add(new WfcValidationIssue(
                    WfcIssueSeverity.Warning,
                    $"'{module.name}': peso 0, solo aparecera si es la unica opcion.", module));
            }
        }

        public static void Validate(WfcModuleSet set, List<WfcValidationIssue> results)
        {
            if (set == null)
            {
                results.Add(new WfcValidationIssue(WfcIssueSeverity.Error, "Set nulo."));
                return;
            }

            if (set.Library == null)
            {
                results.Add(new WfcValidationIssue(
                    WfcIssueSeverity.Error, "El set no tiene libreria de sockets.", set));
            }

            if (set.Count == 0)
            {
                results.Add(new WfcValidationIssue(WfcIssueSeverity.Info, "El set no tiene modulos.", set));
                return;
            }

            var seen = new HashSet<WfcModuleDefinition>();

            foreach (var module in set.Modules)
            {
                if (module == null)
                {
                    results.Add(new WfcValidationIssue(
                        WfcIssueSeverity.Error, "Hay una entrada vacia en la lista de modulos.", set));
                    continue;
                }

                if (!seen.Add(module))
                {
                    results.Add(new WfcValidationIssue(
                        WfcIssueSeverity.Warning, $"'{module.name}' esta duplicado en el set.", set));
                }

                if (set.Library != null && module.Library != set.Library)
                {
                    results.Add(new WfcValidationIssue(
                        WfcIssueSeverity.Error,
                        $"'{module.name}' usa una libreria distinta a la del set.", module));
                }

                Validate(module, results);
            }
        }

        public static int CountBySeverity(List<WfcValidationIssue> issues, WfcIssueSeverity severity)
        {
            int count = 0;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == severity) count++;
            }
            return count;
        }

        /// <summary>Peor severidad presente, o Info si no hay nada que reportar.</summary>
        public static WfcIssueSeverity WorstSeverity(List<WfcValidationIssue> issues)
        {
            var worst = WfcIssueSeverity.Info;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity > worst) worst = issues[i].Severity;
            }
            return worst;
        }
    }
}