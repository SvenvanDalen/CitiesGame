using System;
using Cities.Core.Persistence;

namespace Cities.Core.Randomness
{
    /// <summary>
    /// Sectie <c>RAND</c>: de wereldseed. Meer is niet nodig, omdat elke generator uit seed, stroom en tick volgt; samen
    /// met de tick uit <c>CLCK</c> loopt een geladen stad precies zo verder als het origineel.
    /// </summary>
    public sealed class RandomSection : ISaveSection
    {
        public const string SectionId = "RAND";

        private readonly SimulationRandom _random;

        public RandomSection(SimulationRandom random) => _random = random ?? throw new ArgumentNullException(nameof(random));

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer) => writer.Write(unchecked((long)_random.Seed));

        public Action Read(SaveReader reader, int version)
        {
            var seed = unchecked((ulong)reader.ReadInt64());
            reader.ExpectEnd();
            return () => _random.Reseed(seed);
        }
    }
}
