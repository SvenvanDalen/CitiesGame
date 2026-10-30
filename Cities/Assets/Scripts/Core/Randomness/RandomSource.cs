namespace Cities.Core.Randomness
{
    /// <summary>Stabiele sleutel van een willekeurigheidsstroom, afgeleid van een naam zoals "Verkeer.Bestemming".</summary>
    public readonly struct RandomStreamKey : System.IEquatable<RandomStreamKey>
    {
        public RandomStreamKey(ulong value) => Value = value;

        public ulong Value { get; }

        public static RandomStreamKey Of(string name) => new RandomStreamKey(SeedHash.Of(name));

        public bool Equals(RandomStreamKey other) => Value == other.Value;

        public override bool Equals(object obj) => obj is RandomStreamKey other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public static bool operator ==(RandomStreamKey left, RandomStreamKey right) => left.Equals(right);

        public static bool operator !=(RandomStreamKey left, RandomStreamKey right) => !left.Equals(right);
    }

    /// <summary>
    /// Wereldseed plus stroom: levert per tick (en per object) een eigen generator. Omdat elke generator alleen van
    /// seed, stroom, tick en index afhangt, maakt de volgorde waarin jobs draaien niet uit. Een struct zonder
    /// verwijzingen, dus mee te geven aan Burst-jobs.
    /// </summary>
    /// <example>
    /// <code>
    /// var random = tick.Random(DestinationStream).At(tick.Tick, citizenIndex);
    /// var target = random.NextInt(0, candidates.Length);
    /// </code>
    /// </example>
    public readonly struct RandomSource
    {
        public RandomSource(ulong seed, RandomStreamKey stream)
        {
            Seed = seed;
            Stream = stream;
        }

        public ulong Seed { get; }

        public RandomStreamKey Stream { get; }

        /// <summary>Generator voor deze tick; voor werk op één thread.</summary>
        public DeterministicRandom At(long tick) =>
            new DeterministicRandom(SeedHash.Combine(Seed, Stream.Value, unchecked((ulong)tick)), Stream.Value);

        /// <summary>Generator voor object <paramref name="index"/> in deze tick; voor parallelle jobs.</summary>
        public DeterministicRandom At(long tick, long index) =>
            new DeterministicRandom(SeedHash.Combine(SeedHash.Combine(Seed, Stream.Value, unchecked((ulong)tick)), unchecked((ulong)index)), Stream.Value);
    }
}
