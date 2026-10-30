using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Cities.Core.Randomness
{
    /// <summary>
    /// PCG32-generator (XSH-RR, O'Neill 2014): 64 bit toestand, 2^63 onafhankelijke stromen, periode 2^64. Een struct
    /// zonder verwijzingen, dus bruikbaar in Burst-jobs. Gehele getallen zijn op elk platform identiek; zwevendekomma-
    /// uitkomsten zijn identiek binnen dezelfde build en hetzelfde platform.
    /// </summary>
    /// <remarks>
    /// Simulatiecode gebruikt nooit <c>UnityEngine.Random</c> of <c>System.Random</c>: die delen een globale toestand
    /// of verschillen per runtime. Haal een generator uit <see cref="RandomSource"/>, zodat de uitkomst alleen afhangt
    /// van de wereldseed, de stroom en de tick.
    /// </remarks>
    public struct DeterministicRandom
    {
        private const ulong Multiplier = 6364136223846793005UL;

        private ulong _state;
        private ulong _increment;

        public DeterministicRandom(ulong seed, ulong stream = 0)
        {
            _increment = (stream << 1) | 1UL;
            _state = 0;
            NextUInt();
            _state = unchecked(_state + seed);
            NextUInt();
        }

        /// <summary>Huidige toestand; samen met <see cref="Stream"/> genoeg om de generator te herstellen.</summary>
        public ulong State => _state;

        /// <summary>Stroomnummer (63 bit).</summary>
        public ulong Stream => _increment >> 1;

        /// <summary>Herstelt een generator uit <see cref="State"/> en <see cref="Stream"/>.</summary>
        public static DeterministicRandom FromState(ulong state, ulong stream)
        {
            var random = default(DeterministicRandom);
            random._increment = (stream << 1) | 1UL;
            random._state = state;
            return random;
        }

        public uint NextUInt()
        {
            var old = _state;
            _state = unchecked((old * Multiplier) + _increment);
            var xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            var rotation = (int)(old >> 59);
            return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
        }

        /// <summary>Gelijkverdeeld in [0, <paramref name="bound"/>), zonder modulo-afwijking (Lemire). 0 geeft 0.</summary>
        public uint NextUInt(uint bound)
        {
            if (bound == 0)
            {
                return 0;
            }

            var product = (ulong)NextUInt() * bound;
            var low = (uint)product;
            if (low < bound)
            {
                var threshold = unchecked(0u - bound) % bound;
                while (low < threshold)
                {
                    product = (ulong)NextUInt() * bound;
                    low = (uint)product;
                }
            }

            return (uint)(product >> 32);
        }

        /// <summary>Gelijkverdeeld in [<paramref name="min"/>, <paramref name="maxExclusive"/>); een lege reeks geeft <paramref name="min"/>.</summary>
        public int NextInt(int min, int maxExclusive)
        {
            if (maxExclusive <= min)
            {
                return min;
            }

            var range = (uint)((long)maxExclusive - min);
            return (int)(min + (long)NextUInt(range));
        }

        /// <summary>64 willekeurige bits.</summary>
        public ulong NextULong() => ((ulong)NextUInt() << 32) | NextUInt();

        /// <summary>Gelijkverdeeld in [0, 1) met 24 bit precisie.</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>Gelijkverdeeld in [<paramref name="min"/>, <paramref name="max"/>).</summary>
        public float NextFloat(float min, float max) => min + ((max - min) * NextFloat());

        /// <summary>Gelijkverdeeld in [0, 1) met 53 bit precisie.</summary>
        public double NextDouble()
        {
            var high = (ulong)(NextUInt() >> 5);
            var low = (ulong)(NextUInt() >> 6);
            return ((high * 67108864UL) + low) * (1.0 / 9007199254740992.0);
        }

        public bool NextBool() => (NextUInt() >> 31) != 0;

        /// <summary>Waar met kans <paramref name="probability"/> (0 = nooit, 1 = altijd).</summary>
        public bool Chance(float probability) => NextFloat() < probability;

        /// <summary>Punt in de eenheidscirkel, gelijkverdeeld over het oppervlak; zonder goniometrie, dus overal gelijk.</summary>
        public float2 NextInsideUnitCircle()
        {
            while (true)
            {
                var point = new float2(NextFloat(-1f, 1f), NextFloat(-1f, 1f));
                if (math.lengthsq(point) <= 1f)
                {
                    return point;
                }
            }
        }

        /// <summary>
        /// Normaal verdeeld (Marsaglia-polair). Gebruikt <c>log</c> en <c>sqrt</c>; identiek binnen een build en platform.
        /// </summary>
        public float NextGaussian(float mean, float standardDeviation)
        {
            float u;
            float s;
            do
            {
                u = NextFloat(-1f, 1f);
                var v = NextFloat(-1f, 1f);
                s = (u * u) + (v * v);
            }
            while (s >= 1f || s <= 0f);

            return mean + (standardDeviation * u * math.sqrt(-2f * math.log(s) / s));
        }

        /// <summary>
        /// Index gekozen naar gewicht; negatieve gewichten tellen als 0. Geeft -1 als alle gewichten 0 zijn.
        /// Gehele gewichten, zodat de keuze op elk platform gelijk is.
        /// </summary>
        public int NextWeightedIndex(ReadOnlySpan<int> weights)
        {
            long total = 0;
            foreach (var weight in weights)
            {
                total += math.max(weight, 0);
            }

            if (total == 0)
            {
                return -1;
            }

            var roll = total <= uint.MaxValue ? NextUInt((uint)total) : (long)(NextDouble() * total);
            for (var i = 0; i < weights.Length; i++)
            {
                roll -= math.max(weights[i], 0);
                if (roll < 0)
                {
                    return i;
                }
            }

            return weights.Length - 1;
        }

        /// <summary>Fisher-Yates; elke volgorde is even waarschijnlijk.</summary>
        public void Shuffle<T>(Span<T> items)
        {
            for (var i = items.Length - 1; i > 0; i--)
            {
                var j = (int)NextUInt((uint)i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }

        /// <summary>Fisher-Yates voor beheerde lijsten (main thread); dezelfde volgorde als <see cref="Shuffle{T}(Span{T})"/>.</summary>
        public void ShuffleList<T>(IList<T> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            for (var i = items.Count - 1; i > 0; i--)
            {
                var j = (int)NextUInt((uint)i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }

        /// <summary>Slaat <paramref name="steps"/> getallen over in O(log n), bijvoorbeeld om een opname vanaf een punt na te spelen.</summary>
        public void Advance(ulong steps)
        {
            ulong accumulatedMultiplier = 1;
            ulong accumulatedIncrement = 0;
            var multiplier = Multiplier;
            var increment = _increment;
            while (steps > 0)
            {
                if ((steps & 1) != 0)
                {
                    accumulatedMultiplier = unchecked(accumulatedMultiplier * multiplier);
                    accumulatedIncrement = unchecked((accumulatedIncrement * multiplier) + increment);
                }

                increment = unchecked((multiplier + 1) * increment);
                multiplier = unchecked(multiplier * multiplier);
                steps >>= 1;
            }

            _state = unchecked((accumulatedMultiplier * _state) + accumulatedIncrement);
        }
    }
}
