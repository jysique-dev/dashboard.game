using System.Collections.Generic;
using UnityEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine
{
    /// <summary>
    /// Cara Unity de una variante: que prefab instanciar y con que giro.
    /// El solver nunca ve esto; solo la presentacion.
    /// </summary>
    public readonly struct WfcBakedVariant
    {
        public readonly WfcModuleDefinition Module;
        public readonly int RotationSteps;

        public WfcBakedVariant(WfcModuleDefinition module, int rotationSteps)
        {
            Module = module;
            RotationSteps = rotationSteps;
        }

        public GameObject Prefab => Module != null ? Module.Prefab : null;

        public bool IsAir => Prefab == null;

        public Quaternion Rotation => Quaternion.Euler(0f, 90f * RotationSteps, 0f);

        public string DisplayName
            => Module != null ? $"{Module.name} r{RotationSteps}" : $"<null> r{RotationSteps}";
    }

    /// <summary>
    /// Resultado del horneado: la tabla pura para el solver mas la vista Unity paralela.
    /// Ambas comparten indice de variante, y ese indice es el unico identificador que
    /// circula por el solver.
    ///
    /// No es un asset. Se hornea bajo demanda desde un WfcModuleSet; en la sesion 7 se
    /// cachea para runtime.
    /// </summary>
    public sealed class WfcBakedSet
    {
        public WfcBakedModules Core { get; }

        private readonly WfcBakedVariant[] views;

        public WfcModuleSet Source { get; }

        public int Count => Core.Count;

        public float CellSize => Source != null ? Source.CellSize : 1f;

        internal WfcBakedSet(WfcModuleSet source, WfcBakedModules core, WfcBakedVariant[] views)
        {
            Source = source;
            Core = core;
            this.views = views;
        }

        public WfcBakedVariant GetView(int variantIndex) => views[variantIndex];

        public WfcModuleVariant GetVariant(int variantIndex) => Core[variantIndex];

        public WfcSocketDescriptor GetSocket(int variantIndex, WfcDirection direction)
            => Core.GetSocket(variantIndex, direction);

        public IReadOnlyList<WfcBakedVariant> Views => views;

        /// <summary>Color del socket de una cara, para gizmos y herramientas.</summary>
        public Color GetSocketColor(int variantIndex, WfcDirection direction)
        {
            var library = Source != null ? Source.Library : null;
            if (library == null) return Color.magenta;

            var descriptor = GetSocket(variantIndex, direction);
            return descriptor.IsValid ? library.GetColor(descriptor.Id) : Color.magenta;
        }
    }
}