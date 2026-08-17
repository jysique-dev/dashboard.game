using System;

namespace LoopEngine.WfcEngine.Core
{
    /// <summary>
    /// Que giros de 90 grados alrededor de Y puede tomar un modulo.
    /// La autoria lo declara aqui; la sesion 3 hornea las variantes correspondientes.
    ///
    /// Un modulo con simetria rotacional completa (una plaza, un bloque solido) puede
    /// declarar solo Rot0 para no generar cuatro variantes identicas que inflan la
    /// matriz de vecindad sin aportar nada.
    /// </summary>
    [Flags]
    public enum WfcRotationMask
    {
        None = 0,
        Rot0 = 1 << 0,
        Rot1 = 1 << 1,
        Rot2 = 1 << 2,
        Rot3 = 1 << 3,
        All = Rot0 | Rot1 | Rot2 | Rot3
    }

    public static class WfcRotationMasks
    {
        public const int StepCount = 4;

        public static WfcRotationMask FromStep(int step)
        {
            int normalized = ((step % StepCount) + StepCount) % StepCount;
            return (WfcRotationMask)(1 << normalized);
        }

        public static bool Has(WfcRotationMask mask, int step)
            => (mask & FromStep(step)) != WfcRotationMask.None;

        public static int Count(WfcRotationMask mask)
        {
            int count = 0;
            for (int step = 0; step < StepCount; step++)
            {
                if (Has(mask, step)) count++;
            }
            return count;
        }

        /// <summary>Pasos activos en orden ascendente. Sin allocations por iteracion.</summary>
        public static System.Collections.Generic.IEnumerable<int> Steps(WfcRotationMask mask)
        {
            for (int step = 0; step < StepCount; step++)
            {
                if (Has(mask, step)) yield return step;
            }
        }

        public static string ShortLabel(WfcRotationMask mask)
        {
            if (mask == WfcRotationMask.None) return "sin rotaciones";
            if (mask == WfcRotationMask.All) return "4 rot";

            var parts = new System.Collections.Generic.List<string>();
            foreach (int step in Steps(mask)) parts.Add(step.ToString());
            return "rot " + string.Join("/", parts);
        }
    }
}