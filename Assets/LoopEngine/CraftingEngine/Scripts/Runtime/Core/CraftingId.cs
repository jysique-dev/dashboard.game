using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace LoopEngine.CraftingEngine
{
    /// <summary>
    /// Immutable identifier used by every definition in the crafting system.
    /// Wraps the authoring string and carries a precomputed, platform-stable hash so
    /// dictionary lookups never re-hash the string.
    /// </summary>
    /// <remarks>
    /// This type is intentionally NOT Unity-serializable. Assets serialize the raw
    /// string (see <see cref="CraftingDefinition"/>) and build the id once on load.
    /// Runtime code passes ids around by value.
    /// </remarks>
    public readonly struct CraftingId : IEquatable<CraftingId>
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        /// <summary>An id that never matches a real definition.</summary>
        public static readonly CraftingId None = default;

        private readonly string _value;
        private readonly int _hash;

        /// <summary>The original authoring string. Never null.</summary>
        public string Value => _value ?? string.Empty;

        /// <summary>Precomputed hash. Zero only for <see cref="None"/>.</summary>
        public int Hash => _hash;

        /// <summary>True when this id was built from a non-empty string.</summary>
        public bool IsValid => _hash != 0;

        public CraftingId(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                _value = string.Empty;
                _hash = 0;
                return;
            }

            _value = value;
            _hash = ComputeHash(value);
        }

        /// <summary>
        /// FNV-1a 32-bit over the raw UTF-16 code units. Deterministic across runs,
        /// platforms and runtime versions, unlike <see cref="string.GetHashCode()"/>.
        /// Never returns 0 for a non-empty input, so 0 can mean "no id".
        /// </summary>
        public static int ComputeHash(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0;

            unchecked
            {
                uint hash = FnvOffsetBasis;
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    hash ^= (byte)c;
                    hash *= FnvPrime;
                    hash ^= (byte)(c >> 8);
                    hash *= FnvPrime;
                }

                int result = (int)hash;
                return result == 0 ? 1 : result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(CraftingId other)
        {
            if (_hash != other._hash)
                return false;

            // Hash equality is not identity: confirm with an ordinal compare.
            return string.Equals(_value, other._value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is CraftingId other && Equals(other);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => _hash;

        public override string ToString() => IsValid ? _value : "<none>";

        public static bool operator ==(CraftingId a, CraftingId b) => a.Equals(b);

        public static bool operator !=(CraftingId a, CraftingId b) => !a.Equals(b);
    }

    /// <summary>
    /// Allocation-free comparer for <see cref="CraftingId"/> keyed collections.
    /// Pass this to every Dictionary/HashSet to avoid the generic default comparer path.
    /// </summary>
    public sealed class CraftingIdComparer : IEqualityComparer<CraftingId>
    {
        public static readonly CraftingIdComparer Instance = new CraftingIdComparer();

        private CraftingIdComparer() { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(CraftingId x, CraftingId y) => x.Equals(y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(CraftingId obj) => obj.Hash;
    }
}