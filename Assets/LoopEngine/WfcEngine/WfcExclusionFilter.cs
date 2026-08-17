using System.Collections.Generic;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine
{
    /// <summary>
    /// Traduce reglas de autoria (por modulo, por socket) a la pregunta que hace el
    /// nucleo: dadas dos variantes y una direccion, esta prohibida esa vecindad.
    ///
    /// La traduccion se hace una vez al construir el filtro, no en cada consulta: las
    /// reglas por modulo se expanden a todas sus variantes giradas y se guardan en sets
    /// de pares. Construir la matriz hace VariantCount^2 * 3 consultas, asi que una
    /// busqueda lineal por regla aqui seria muy cara.
    /// </summary>
    public sealed class WfcExclusionFilter : IWfcAdjacencyFilter
    {
        private readonly HashSet<long> anyDirection = new HashSet<long>();
        private readonly HashSet<long> directional = new HashSet<long>();
        private readonly HashSet<long> socketPairs = new HashSet<long>();

        public int RuleCount { get; private set; }
        public int SkippedRules { get; private set; }

        public static WfcExclusionFilter Build(
            WfcBakedSet baked, WfcExclusionSet exclusions, List<WfcValidationIssue> issues = null)
        {
            var filter = new WfcExclusionFilter();
            if (baked == null || exclusions == null) return filter;

            // Modulo de autoria -> variantes horneadas que salieron de el.
            var variantsOfModule = new Dictionary<WfcModuleDefinition, List<int>>();

            for (int variant = 0; variant < baked.Count; variant++)
            {
                var module = baked.GetView(variant).Module;
                if (module == null) continue;

                if (!variantsOfModule.TryGetValue(module, out var list))
                {
                    list = new List<int>();
                    variantsOfModule[module] = list;
                }

                list.Add(variant);
            }

            foreach (var rule in exclusions.Rules)
            {
                if (rule == null) continue;

                if (!filter.Apply(rule, variantsOfModule, baked))
                {
                    filter.SkippedRules++;
                    issues?.Add(new WfcValidationIssue(
                        WfcIssueSeverity.Warning,
                        $"Regla ignorada ({rule.Describe()}): referencias incompletas.",
                        exclusions));
                    continue;
                }

                filter.RuleCount++;
            }

            // Solo hace falta si hay reglas por socket; en el caso comun no se paga.
            if (filter.socketPairs.Count > 0) filter.CacheFaces(baked);

            return filter;
        }

        private bool Apply(
            WfcExclusionRule rule,
            Dictionary<WfcModuleDefinition, List<int>> variantsOfModule,
            WfcBakedSet baked)
        {
            switch (rule.kind)
            {
                case WfcExclusionKind.ModulePair:
                case WfcExclusionKind.ModuleDirectional:
                    {
                        if (rule.moduleA == null || rule.moduleB == null) return false;
                        if (!variantsOfModule.TryGetValue(rule.moduleA, out var listA)) return false;
                        if (!variantsOfModule.TryGetValue(rule.moduleB, out var listB)) return false;

                        foreach (int a in listA)
                        {
                            foreach (int b in listB)
                            {
                                if (rule.kind == WfcExclusionKind.ModulePair)
                                {
                                    anyDirection.Add(Key(a, b));
                                    anyDirection.Add(Key(b, a));
                                }
                                else
                                {
                                    directional.Add(Key(a, b, rule.direction));
                                }
                            }
                        }

                        return true;
                    }

                case WfcExclusionKind.SocketPair:
                    {
                        if (!rule.socketA.TryResolve(out var socketA)) return false;
                        if (!rule.socketB.TryResolve(out var socketB)) return false;

                        socketPairs.Add(SocketKey(socketA, socketB));
                        socketPairs.Add(SocketKey(socketB, socketA));
                        return true;
                    }

                default:
                    return false;
            }
        }

        private readonly List<WfcSocketDescriptor[]> variantFaces = new List<WfcSocketDescriptor[]>();

        /// <summary>Cachea las caras de cada variante para resolver reglas de socket sin volver a la tabla.</summary>
        public void CacheFaces(WfcBakedSet baked)
        {
            variantFaces.Clear();

            for (int variant = 0; variant < baked.Count; variant++)
            {
                var faces = new WfcSocketDescriptor[WfcDirections.Count];
                for (int d = 0; d < WfcDirections.Count; d++)
                {
                    faces[d] = baked.GetSocket(variant, (WfcDirection)d);
                }
                variantFaces.Add(faces);
            }
        }

        public bool IsForbidden(int variantA, int variantB, WfcDirection directionFromAToB)
        {
            if (anyDirection.Count > 0 && anyDirection.Contains(Key(variantA, variantB))) return true;

            if (directional.Count > 0 && directional.Contains(Key(variantA, variantB, directionFromAToB)))
            {
                return true;
            }

            if (socketPairs.Count > 0 && variantFaces.Count > variantA && variantFaces.Count > variantB)
            {
                var socketA = variantFaces[variantA][(int)directionFromAToB];
                var socketB = variantFaces[variantB][(int)WfcDirections.Opposite(directionFromAToB)];

                if (socketPairs.Contains(SocketKey(socketA, socketB))) return true;
            }

            return false;
        }

        private static long Key(int a, int b) => ((long)a << 32) | (uint)b;

        private static long Key(int a, int b, WfcDirection direction)
            => (((long)a << 32) | (uint)b) * 6L + (int)direction;

        private static long SocketKey(WfcSocketDescriptor a, WfcSocketDescriptor b)
            => ((long)a.GetHashCode() << 32) | (uint)b.GetHashCode();
    }
}