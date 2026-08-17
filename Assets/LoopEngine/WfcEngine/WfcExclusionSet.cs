using System;
using System.Collections.Generic;
using UnityEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine
{
    public enum WfcExclusionKind
    {
        /// <summary>A y B nunca son vecinos, en ninguna direccion.</summary>
        ModulePair = 0,

        /// <summary>B nunca aparece en la direccion dada respecto de A. Direccion de mundo.</summary>
        ModuleDirectional = 1,

        /// <summary>Dos sockets compatibles que se prohibe unir, en cualquier modulo.</summary>
        SocketPair = 2
    }

    [Serializable]
    public class WfcExclusionRule
    {
        public WfcExclusionKind kind = WfcExclusionKind.ModulePair;

        [Tooltip("Modulo origen. Aplica a todas sus variantes giradas.")]
        public WfcModuleDefinition moduleA;

        [Tooltip("Modulo vecino prohibido.")]
        public WfcModuleDefinition moduleB;

        [Tooltip("Solo para ModuleDirectional: direccion de mundo de A hacia B.")]
        public WfcDirection direction = WfcDirection.Up;

        [Tooltip("Solo para SocketPair.")]
        public WfcSocketRef socketA;

        public WfcSocketRef socketB;

        [TextArea(1, 3)] public string reason = string.Empty;

        public string Describe()
        {
            switch (kind)
            {
                case WfcExclusionKind.ModulePair:
                    return $"{Name(moduleA)} nunca junto a {Name(moduleB)}";
                case WfcExclusionKind.ModuleDirectional:
                    return $"{Name(moduleB)} nunca en {WfcDirections.ShortName(direction)} de {Name(moduleA)}";
                case WfcExclusionKind.SocketPair:
                    return $"socket {socketA.Descriptor.Notation()} nunca con {socketB.Descriptor.Notation()}";
                default:
                    return "?";
            }
        }

        private static string Name(WfcModuleDefinition module) => module != null ? module.name : "<vacio>";
    }

    /// <summary>
    /// Reglas que recortan lo que los sockets permitirian. Asset aparte del set de modulos
    /// a proposito: un mismo set puede tener distintos juegos de reglas por zona o por
    /// mundo sin duplicar modulos.
    ///
    /// Una direccional usa direcciones de mundo, no relativas al modulo. Para "nunca encima"
    /// eso es exactamente lo que se quiere; para reglas horizontales hay que tener presente
    /// que la variante girada no rota la regla.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WfcExclusionSet",
        menuName = "LoopEngine/WFC/Exclusion Set",
        order = 3)]
    public class WfcExclusionSet : ScriptableObject
    {
        [SerializeField] private WfcModuleSet moduleSet;
        [SerializeField] private List<WfcExclusionRule> rules = new List<WfcExclusionRule>();

        public event Action Changed;

        public WfcModuleSet ModuleSet => moduleSet;
        public IReadOnlyList<WfcExclusionRule> Rules => rules;
        public int Count => rules.Count;

        public void Add(WfcExclusionRule rule)
        {
            if (rule == null) return;

            rules.Add(rule);
            Changed?.Invoke();
        }

        public bool Remove(WfcExclusionRule rule)
        {
            if (!rules.Remove(rule)) return false;

            Changed?.Invoke();
            return true;
        }

        private void OnValidate() => Changed?.Invoke();
    }
}