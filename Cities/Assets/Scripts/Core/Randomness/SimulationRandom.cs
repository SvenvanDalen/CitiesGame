using System;
using System.Collections.Generic;
using System.Globalization;

namespace Cities.Core.Randomness
{
    /// <summary>
    /// De wereldseed van een stad. Alle willekeur in de simulatie volgt uit deze seed, de stroomnaam en de tick, zodat
    /// dezelfde seed met dezelfde commands dezelfde stad oplevert. De seed gaat mee in de save (sectie <c>RAND</c>).
    /// </summary>
    public sealed class SimulationRandom
    {
        private readonly Dictionary<ulong, string> _streams = new Dictionary<ulong, string>();

        public SimulationRandom(ulong seed) => Seed = seed;

        public ulong Seed { get; private set; }

        /// <summary>Na <see cref="Reseed"/>, bijvoorbeeld bij laden of een nieuwe stad.</summary>
        public event Action<ulong> Reseeded;

        public void Reseed(ulong seed)
        {
            if (seed == Seed)
            {
                return;
            }

            Seed = seed;
            Reseeded?.Invoke(seed);
        }

        /// <summary>
        /// Meldt een stroom aan en geeft de sleutel terug. Twee verschillende namen met dezelfde hash geven een fout,
        /// zodat systemen nooit ongemerkt dezelfde getallen delen.
        /// </summary>
        public RandomStreamKey Register(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Een stroom heeft een naam nodig.", nameof(name));
            }

            var key = RandomStreamKey.Of(name);
            if (_streams.TryGetValue(key.Value, out var existing) && existing != name)
            {
                throw new InvalidOperationException($"Stroom '{name}' botst met '{existing}'; kies een andere naam.");
            }

            _streams[key.Value] = name;
            return key;
        }

        public RandomSource Source(RandomStreamKey stream) => new RandomSource(Seed, stream);

        /// <summary>Decimale weergave, zoals spelers een seed delen en op de opdrachtregel opgeven.</summary>
        public static string Format(ulong seed) => seed.ToString(CultureInfo.InvariantCulture);

        /// <summary>Leest een decimale seed of een hexadecimale met <c>0x</c>; spaties eromheen mogen.</summary>
        public static bool TryParse(string text, out ulong seed)
        {
            seed = 0;
            var trimmed = text?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return false;
            }

            return trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? ulong.TryParse(trimmed.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out seed)
                : ulong.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out seed);
        }

        /// <summary>
        /// Zoekt <c>-seed &lt;waarde&gt;</c> in de opstartargumenten. Geeft <c>false</c> zonder argument; een ongeldige
        /// waarde komt in <paramref name="error"/>.
        /// </summary>
        public static bool TryReadCommandLine(IReadOnlyList<string> args, out ulong seed, out string error)
        {
            seed = 0;
            error = null;
            if (args == null)
            {
                return false;
            }

            for (var i = 0; i < args.Count; i++)
            {
                if (!string.Equals(args[i], "-seed", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (i + 1 < args.Count && TryParse(args[i + 1], out seed))
                {
                    return true;
                }

                error = "Opstartargument -seed verwacht een getal, bijvoorbeeld -seed 12345 of -seed 0x3039.";
                return false;
            }

            return false;
        }
    }
}
