namespace LoopEngine.WfcEngine.Core
{
    /// <summary>
    /// Las 6 caras de una celda cubica. El orden es estable y se usa como indice
    /// en arrays de vecinos y en la matriz de adyacencia (sesion 4).
    /// Convencion de ejes: Unity, Y arriba, Z hacia adelante.
    /// </summary>
    public enum WfcDirection
    {
        Right = 0,   // +X
        Left = 1,    // -X
        Up = 2,      // +Y
        Down = 3,    // -Y
        Forward = 4, // +Z
        Back = 5     // -Z
    }

    public static class WfcDirections
    {
        public const int Count = 6;

        public static readonly WfcDirection[] All =
        {
            WfcDirection.Right,
            WfcDirection.Left,
            WfcDirection.Up,
            WfcDirection.Down,
            WfcDirection.Forward,
            WfcDirection.Back
        };

        /// <summary>
        /// Anillo horizontal en sentido de giro positivo (+90 grados de yaw en Unity).
        /// Un paso de rotacion manda Forward -> Right -> Back -> Left -> Forward.
        /// </summary>
        public static readonly WfcDirection[] HorizontalRing =
        {
            WfcDirection.Forward,
            WfcDirection.Right,
            WfcDirection.Back,
            WfcDirection.Left
        };

        public static readonly WfcDirection[] Horizontal = HorizontalRing;

        public static readonly WfcDirection[] Vertical =
        {
            WfcDirection.Up,
            WfcDirection.Down
        };

        public static bool IsHorizontal(WfcDirection d) => d != WfcDirection.Up && d != WfcDirection.Down;

        public static bool IsVertical(WfcDirection d) => d == WfcDirection.Up || d == WfcDirection.Down;

        public static WfcDirection Opposite(WfcDirection d)
        {
            switch (d)
            {
                case WfcDirection.Right: return WfcDirection.Left;
                case WfcDirection.Left: return WfcDirection.Right;
                case WfcDirection.Up: return WfcDirection.Down;
                case WfcDirection.Down: return WfcDirection.Up;
                case WfcDirection.Forward: return WfcDirection.Back;
                case WfcDirection.Back: return WfcDirection.Forward;
                default: return d;
            }
        }

        public static WfcInt3 Offset(WfcDirection d)
        {
            switch (d)
            {
                case WfcDirection.Right: return new WfcInt3(1, 0, 0);
                case WfcDirection.Left: return new WfcInt3(-1, 0, 0);
                case WfcDirection.Up: return new WfcInt3(0, 1, 0);
                case WfcDirection.Down: return new WfcInt3(0, -1, 0);
                case WfcDirection.Forward: return new WfcInt3(0, 0, 1);
                case WfcDirection.Back: return new WfcInt3(0, 0, -1);
                default: return WfcInt3.Zero;
            }
        }

        /// <summary>
        /// Indice de la direccion dentro del anillo horizontal, o -1 si es vertical.
        /// </summary>
        public static int HorizontalIndex(WfcDirection d)
        {
            for (int i = 0; i < HorizontalRing.Length; i++)
            {
                if (HorizontalRing[i] == d) return i;
            }
            return -1;
        }

        /// <summary>
        /// Rota una direccion alrededor de Y en pasos de 90 grados.
        /// Las verticales no se ven afectadas. Acepta pasos negativos.
        /// </summary>
        public static WfcDirection RotateAroundY(WfcDirection d, int steps)
        {
            if (IsVertical(d)) return d;

            int index = HorizontalIndex(d);
            if (index < 0) return d;

            int rotated = ((index + steps) % 4 + 4) % 4;
            return HorizontalRing[rotated];
        }

        public static string ShortName(WfcDirection d)
        {
            switch (d)
            {
                case WfcDirection.Right: return "+X";
                case WfcDirection.Left: return "-X";
                case WfcDirection.Up: return "+Y";
                case WfcDirection.Down: return "-Y";
                case WfcDirection.Forward: return "+Z";
                case WfcDirection.Back: return "-Z";
                default: return "?";
            }
        }
    }
}