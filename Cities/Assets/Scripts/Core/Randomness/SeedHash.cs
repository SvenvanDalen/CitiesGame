using System;

namespace Cities.Core.Randomness
{
    /// <summary>
    /// Stabiele hashes voor seeds: gelijk op elke runtime en elk platform, anders dan <c>string.GetHashCode</c>.
    /// </summary>
    public static class SeedHash
    {
        private const ulong Golden = 0x9E3779B97F4A7C15UL;
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        /// <summary>SplitMix64-finalizer: elke invoerbit beïnvloedt elke uitvoerbit.</summary>
        public static ulong Mix(ulong value)
        {
            unchecked
            {
                value += Golden;
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
        }

        /// <summary>Combineert twee waarden; de volgorde telt (<c>Combine(a, b) != Combine(b, a)</c>).</summary>
        public static ulong Combine(ulong first, ulong second) => Mix(unchecked((first * Golden) ^ Mix(second)));

        public static ulong Combine(ulong first, ulong second, ulong third) => Combine(Combine(first, second), third);

        /// <summary>FNV-1a over de UTF-16-tekens, daarna gemengd; <c>null</c> en "" geven dezelfde waarde.</summary>
        public static ulong Of(string text)
        {
            var hash = FnvOffset;
            foreach (var character in text ?? string.Empty)
            {
                hash = unchecked((hash ^ character) * FnvPrime);
            }

            return Mix(hash);
        }

        /// <summary>Nieuwe, onvoorspelbare seed voor een nieuwe stad; nooit in simulatiecode gebruiken.</summary>
        public static ulong NewSeed()
        {
            var bytes = Guid.NewGuid().ToByteArray();
            return Mix(BitConverter.ToUInt64(bytes, 0) ^ BitConverter.ToUInt64(bytes, 8));
        }
    }
}
