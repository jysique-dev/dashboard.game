namespace LoopEngine.WfcEngine.Core
{
    /// <summary>
    /// Operaciones de bitset sobre ulong[] con offset explicito, para que la matriz de
    /// vecindad y los dominios de celda del solver compartan un solo array grande sin
    /// una allocation por fila.
    ///
    /// Sobre TrailingZeroCount: se usa el metodo binario, no una tabla de De Bruijn.
    /// Una constante de De Bruijn mal transcrita no lanza excepcion, devuelve indices
    /// equivocados, y el error solo aparece en condiciones de borde. Este metodo no
    /// tiene constante que transcribir mal.
    /// </summary>
    public static class WfcBitSet
    {
        public const int BitsPerWord = 64;

        public static int WordsFor(int bitCount) => (bitCount + BitsPerWord - 1) / BitsPerWord;

        public static bool Get(ulong[] bits, int offset, int index)
            => (bits[offset + (index >> 6)] & (1UL << (index & 63))) != 0UL;

        public static void Set(ulong[] bits, int offset, int index)
            => bits[offset + (index >> 6)] |= 1UL << (index & 63);

        public static void Clear(ulong[] bits, int offset, int index)
            => bits[offset + (index >> 6)] &= ~(1UL << (index & 63));

        public static void ClearAll(ulong[] bits, int offset, int words)
        {
            for (int i = 0; i < words; i++) bits[offset + i] = 0UL;
        }

        /// <summary>
        /// Pone a 1 los primeros bitCount bits y a 0 el relleno de la ultima palabra.
        /// Sin la mascara de cola, PopCount cuenta variantes que no existen y el solver
        /// cree tener opciones fantasma.
        /// </summary>
        public static void FillRange(ulong[] bits, int offset, int words, int bitCount)
        {
            for (int i = 0; i < words; i++) bits[offset + i] = ulong.MaxValue;

            int rest = bitCount & 63;
            if (rest != 0 && words > 0)
            {
                bits[offset + words - 1] = (1UL << rest) - 1UL;
            }
        }

        public static void CopyTo(ulong[] source, int sourceOffset, ulong[] dest, int destOffset, int words)
        {
            for (int i = 0; i < words; i++) dest[destOffset + i] = source[sourceOffset + i];
        }

        /// <summary>dest &amp;= source. Devuelve true si dest cambio.</summary>
        public static bool AndInto(ulong[] dest, int destOffset, ulong[] source, int sourceOffset, int words)
        {
            bool changed = false;

            for (int i = 0; i < words; i++)
            {
                ulong before = dest[destOffset + i];
                ulong after = before & source[sourceOffset + i];

                if (after != before)
                {
                    dest[destOffset + i] = after;
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>dest |= source. Devuelve true si dest cambio.</summary>
        public static bool OrInto(ulong[] dest, int destOffset, ulong[] source, int sourceOffset, int words)
        {
            bool changed = false;

            for (int i = 0; i < words; i++)
            {
                ulong before = dest[destOffset + i];
                ulong after = before | source[sourceOffset + i];

                if (after != before)
                {
                    dest[destOffset + i] = after;
                    changed = true;
                }
            }

            return changed;
        }

        public static bool IsEmpty(ulong[] bits, int offset, int words)
        {
            for (int i = 0; i < words; i++)
            {
                if (bits[offset + i] != 0UL) return false;
            }
            return true;
        }

        public static bool Intersects(ulong[] a, int aOffset, ulong[] b, int bOffset, int words)
        {
            for (int i = 0; i < words; i++)
            {
                if ((a[aOffset + i] & b[bOffset + i]) != 0UL) return true;
            }
            return false;
        }

        public static int PopCount(ulong[] bits, int offset, int words)
        {
            int total = 0;
            for (int i = 0; i < words; i++) total += PopCount(bits[offset + i]);
            return total;
        }

        public static int PopCount(ulong value)
        {
            value -= (value >> 1) & 0x5555555555555555UL;
            value = (value & 0x3333333333333333UL) + ((value >> 2) & 0x3333333333333333UL);
            value = (value + (value >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
            return (int)((value * 0x0101010101010101UL) >> 56);
        }

        /// <summary>Numero de ceros a la derecha. 64 si el valor es 0.</summary>
        public static int TrailingZeroCount(ulong value)
        {
            if (value == 0UL) return BitsPerWord;

            int count = 0;

            if ((value & 0xFFFFFFFFUL) == 0UL) { count += 32; value >>= 32; }
            if ((value & 0xFFFFUL) == 0UL) { count += 16; value >>= 16; }
            if ((value & 0xFFUL) == 0UL) { count += 8; value >>= 8; }
            if ((value & 0xFUL) == 0UL) { count += 4; value >>= 4; }
            if ((value & 0x3UL) == 0UL) { count += 2; value >>= 2; }
            if ((value & 0x1UL) == 0UL) { count += 1; }

            return count;
        }

        /// <summary>Primer bit a 1 en el indice dado o despues. -1 si no hay ninguno.</summary>
        public static int NextSetBit(ulong[] bits, int offset, int words, int fromIndex)
        {
            if (fromIndex < 0) fromIndex = 0;

            int word = fromIndex >> 6;
            if (word >= words) return -1;

            ulong current = bits[offset + word] & (ulong.MaxValue << (fromIndex & 63));

            while (true)
            {
                if (current != 0UL) return (word << 6) + TrailingZeroCount(current);

                word++;
                if (word >= words) return -1;

                current = bits[offset + word];
            }
        }

        public static int FirstSetBit(ulong[] bits, int offset, int words)
            => NextSetBit(bits, offset, words, 0);
    }
}