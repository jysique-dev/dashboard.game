namespace LoopEngine.WfcEngine.Core
{
    /// <summary>
    /// Generador propio, xorshift64*.
    ///
    /// No se usa System.Random a proposito: su implementacion cambio entre .NET Framework
    /// y .NET Core, asi que "misma semilla, mismo resultado" solo vale dentro de un mismo
    /// runtime. Para un citybuilder donde una ciudad se reconstruye desde una semilla
    /// guardada, eso es inaceptable: el mismo save daria ciudades distintas.
    /// </summary>
    public sealed class WfcRandom
    {
        private ulong state;

        public uint Seed { get; }

        public WfcRandom(uint seed)
        {
            Seed = seed;

            // Una semilla 0 dejaria el xorshift atascado en 0 para siempre.
            state = seed == 0u ? 0x9E3779B97F4A7C15UL : seed;

            // Unas cuantas iteraciones de calentamiento: semillas consecutivas (1, 2, 3)
            // producirian secuencias iniciales sospechosamente parecidas sin esto.
            for (int i = 0; i < 8; i++) NextULong();
        }

        public ulong NextULong()
        {
            ulong x = state;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            state = x;
            return x * 0x2545F4914F6CDD1DUL;
        }

        public uint NextUInt() => (uint)(NextULong() >> 32);

        /// <summary>[0, 1).</summary>
        public double NextDouble() => (NextULong() >> 11) * (1.0 / 9007199254740992.0);

        /// <summary>[0, 1).</summary>
        public float NextFloat() => (float)NextDouble();

        /// <summary>[0, maxExclusive).</summary>
        public int NextInt(int maxExclusive)
            => maxExclusive <= 0 ? 0 : (int)(NextULong() % (ulong)maxExclusive);
    }
}