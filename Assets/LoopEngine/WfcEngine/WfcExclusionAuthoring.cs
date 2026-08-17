using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine
{
    /// <summary>
    /// Alta y baja de reglas de exclusion desde herramientas de autoria.
    /// Vive en runtime, junto a WfcExclusionSet, para que la busqueda de una regla
    /// existente use el mismo criterio que su creacion: si "ya existe" y "crear" no
    /// coincidieran, la interfaz acumularia reglas duplicadas sin que se note.
    ///
    /// Las reglas son por modulo, no por variante: excluir afecta a las 4 rotaciones.
    /// Es una limitacion consciente del modelo de la sesion 4 y la interfaz debe decirlo.
    /// </summary>
    public static class WfcExclusionAuthoring
    {
        /// <summary>ModulePair es simetrica: se busca en los dos ordenes.</summary>
        public static WfcExclusionRule FindModulePair(
            WfcExclusionSet set, WfcModuleDefinition a, WfcModuleDefinition b)
        {
            if (set == null || a == null || b == null) return null;

            foreach (var rule in set.Rules)
            {
                if (rule == null || rule.kind != WfcExclusionKind.ModulePair) continue;

                if ((rule.moduleA == a && rule.moduleB == b) ||
                    (rule.moduleA == b && rule.moduleB == a))
                {
                    return rule;
                }
            }

            return null;
        }

        public static WfcExclusionRule FindDirectional(
            WfcExclusionSet set, WfcModuleDefinition a, WfcModuleDefinition b, WfcDirection direction)
        {
            if (set == null || a == null || b == null) return null;

            foreach (var rule in set.Rules)
            {
                if (rule == null || rule.kind != WfcExclusionKind.ModuleDirectional) continue;

                if (rule.moduleA == a && rule.moduleB == b && rule.direction == direction) return rule;
            }

            return null;
        }

        /// <summary>Devuelve el estado resultante: true si queda excluido.</summary>
        public static bool ToggleModulePair(
            WfcExclusionSet set, WfcModuleDefinition a, WfcModuleDefinition b, string reason = null)
        {
            if (set == null || a == null || b == null) return false;

            var existing = FindModulePair(set, a, b);
            if (existing != null)
            {
                set.Remove(existing);
                return false;
            }

            set.Add(new WfcExclusionRule
            {
                kind = WfcExclusionKind.ModulePair,
                moduleA = a,
                moduleB = b,
                reason = reason ?? string.Empty
            });

            return true;
        }

        public static bool ToggleDirectional(
            WfcExclusionSet set,
            WfcModuleDefinition a,
            WfcModuleDefinition b,
            WfcDirection direction,
            string reason = null)
        {
            if (set == null || a == null || b == null) return false;

            var existing = FindDirectional(set, a, b, direction);
            if (existing != null)
            {
                set.Remove(existing);
                return false;
            }

            set.Add(new WfcExclusionRule
            {
                kind = WfcExclusionKind.ModuleDirectional,
                moduleA = a,
                moduleB = b,
                direction = direction,
                reason = reason ?? string.Empty
            });

            return true;
        }
    }
}