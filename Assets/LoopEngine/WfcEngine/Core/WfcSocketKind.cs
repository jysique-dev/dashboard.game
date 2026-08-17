namespace LoopEngine.WfcEngine.Core
{
    /// <summary>
    /// Naturaleza de un socket. Determina que regla de compatibilidad se aplica
    /// y que modificador es relevante al usarlo en una cara concreta.
    ///
    /// Convencion tomada de la implementacion de marian42 (marian42.de/article/wfc):
    /// los sockets horizontales son simetricos o forman pares flipped / not-flipped;
    /// los verticales son invariantes a la rotacion o llevan un indice de rotacion 0..3.
    /// </summary>
    public enum WfcSocketKind
    {
        /// <summary>Cara horizontal simetrica respecto a su eje vertical. Encaja consigo misma.</summary>
        HorizontalSymmetric = 0,

        /// <summary>Cara horizontal asimetrica. Solo encaja con su version espejada (flipped).</summary>
        HorizontalAsymmetric = 1,

        /// <summary>Cara vertical con simetria rotacional completa. Encaja consigo misma en cualquier giro.</summary>
        VerticalInvariant = 2,

        /// <summary>Cara vertical con orientacion. Solo encaja con el mismo indice de rotacion.</summary>
        VerticalRotational = 3
    }

    public static class WfcSocketKinds
    {
        public static bool IsHorizontal(WfcSocketKind kind)
            => kind == WfcSocketKind.HorizontalSymmetric || kind == WfcSocketKind.HorizontalAsymmetric;

        public static bool IsVertical(WfcSocketKind kind) => !IsHorizontal(kind);

        /// <summary>El modificador "flipped" solo tiene sentido en horizontales asimetricos.</summary>
        public static bool UsesFlipped(WfcSocketKind kind) => kind == WfcSocketKind.HorizontalAsymmetric;

        /// <summary>El indice de rotacion solo tiene sentido en verticales orientados.</summary>
        public static bool UsesRotationIndex(WfcSocketKind kind) => kind == WfcSocketKind.VerticalRotational;

        /// <summary>
        /// Valida que un socket de este tipo pueda ir en esa cara.
        /// Un socket horizontal en la cara Up es un error de autoria.
        /// </summary>
        public static bool FitsDirection(WfcSocketKind kind, WfcDirection direction)
            => IsHorizontal(kind) == WfcDirections.IsHorizontal(direction);

        public static string ShortLabel(WfcSocketKind kind)
        {
            switch (kind)
            {
                case WfcSocketKind.HorizontalSymmetric: return "H-sim";
                case WfcSocketKind.HorizontalAsymmetric: return "H-asim";
                case WfcSocketKind.VerticalInvariant: return "V-inv";
                case WfcSocketKind.VerticalRotational: return "V-rot";
                default: return "?";
            }
        }
    }
}