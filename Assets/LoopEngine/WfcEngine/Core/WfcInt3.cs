namespace LoopEngine.WfcEngine.Core
{
    /// <summary>
    /// Vector entero 3D. Existe para que el nucleo del solver no dependa de UnityEngine
    /// (Vector3Int). La capa Unity convierte en sus fronteras.
    /// </summary>
    public readonly struct WfcInt3 : System.IEquatable<WfcInt3>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public WfcInt3(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static readonly WfcInt3 Zero = new WfcInt3(0, 0, 0);
        public static readonly WfcInt3 One = new WfcInt3(1, 1, 1);

        public static WfcInt3 operator +(WfcInt3 a, WfcInt3 b) => new WfcInt3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static WfcInt3 operator -(WfcInt3 a, WfcInt3 b) => new WfcInt3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static WfcInt3 operator *(WfcInt3 a, int k) => new WfcInt3(a.X * k, a.Y * k, a.Z * k);

        public static bool operator ==(WfcInt3 a, WfcInt3 b) => a.Equals(b);
        public static bool operator !=(WfcInt3 a, WfcInt3 b) => !a.Equals(b);

        public bool Equals(WfcInt3 other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is WfcInt3 other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X;
                hash = (hash * 397) ^ Y;
                hash = (hash * 397) ^ Z;
                return hash;
            }
        }

        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}