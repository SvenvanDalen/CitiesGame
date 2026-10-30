using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Core.Randomness;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Randomness
{
    public class SimulationRandomTests
    {
        [Test]
        public void HashesArePinnedSoSavesAndReplaysStayValid()
        {
            // Deze waarden mogen nooit veranderen: opnames en saves rekenen erop.
            Assert.That(SeedHash.Mix(0), Is.EqualTo(0xE220A8397B1DCDAFUL), "Eerste SplitMix64-uitvoer bij seed 0");
            Assert.That(SeedHash.Of("Verkeer"), Is.EqualTo(0xF8606B1249816FB6UL));
            Assert.That(SeedHash.Of(null), Is.EqualTo(SeedHash.Of(string.Empty)));
        }

        [Test]
        public void CombineDependsOnOrderAndEveryArgument()
        {
            Assert.That(SeedHash.Combine(1, 2), Is.Not.EqualTo(SeedHash.Combine(2, 1)));
            Assert.That(SeedHash.Combine(1, 2, 3), Is.Not.EqualTo(SeedHash.Combine(1, 2, 4)));
            Assert.That(SeedHash.Combine(0, 0), Is.Not.EqualTo(0UL));
            Assert.That(SeedHash.NewSeed(), Is.Not.EqualTo(SeedHash.NewSeed()));
        }

        [Test]
        public void EveryTickAndObjectGetsItsOwnNumbers()
        {
            var random = new SimulationRandom(777);
            var source = random.Source(random.Register("Burgers.Leeftijd"));
            var values = new HashSet<uint>();

            for (var tick = 0; tick < 50; tick++)
            {
                values.Add(source.At(tick).NextUInt());
                for (var index = 0; index < 20; index++)
                {
                    values.Add(source.At(tick, index).NextUInt());
                }
            }

            Assert.That(values.Count, Is.EqualTo(50 * 21));
            Assert.That(source.At(12, 3).NextUInt(), Is.EqualTo(source.At(12, 3).NextUInt()), "Zelfde tick en index, zelfde getal, in welke volgorde ook");
            Assert.That(source.Seed, Is.EqualTo(777UL));
        }

        [Test]
        public void StreamsAndSeedsAreIndependent()
        {
            var random = new SimulationRandom(1);
            var traffic = random.Source(random.Register("Verkeer"));
            var fires = random.Source(random.Register("Branden"));
            var otherCity = new SimulationRandom(2).Source(RandomStreamKey.Of("Verkeer"));

            Assert.That(traffic.At(5).NextUInt(), Is.Not.EqualTo(fires.At(5).NextUInt()));
            Assert.That(traffic.At(5).NextUInt(), Is.Not.EqualTo(otherCity.At(5).NextUInt()));
            Assert.That(traffic.Stream, Is.EqualTo(RandomStreamKey.Of("Verkeer")));
            Assert.That(traffic.Stream == fires.Stream, Is.False);
            Assert.That(traffic.Stream != fires.Stream, Is.True);
            Assert.That(traffic.Stream.Equals((object)RandomStreamKey.Of("Verkeer")), Is.True);
            Assert.That(traffic.Stream.Equals("Verkeer"), Is.False);
            Assert.That(traffic.Stream.GetHashCode(), Is.EqualTo(RandomStreamKey.Of("Verkeer").GetHashCode()));
        }

        [Test]
        public void RegisteringTwiceIsFineButNamesAreRequired()
        {
            var random = new SimulationRandom(1);

            Assert.That(random.Register("Verkeer"), Is.EqualTo(random.Register("Verkeer")));
            Assert.Throws<ArgumentException>(() => random.Register(""));
            Assert.Throws<ArgumentException>(() => random.Register(null));
        }

        [Test]
        public void ReseedRaisesAnEventOnlyOnChange()
        {
            var random = new SimulationRandom(5);
            var events = new List<ulong>();
            random.Reseeded += events.Add;

            random.Reseed(5);
            random.Reseed(6);

            Assert.That(events, Is.EqualTo(new[] { 6UL }));
            Assert.That(random.Seed, Is.EqualTo(6UL));
            Assert.That(random.Source(RandomStreamKey.Of("A")).Seed, Is.EqualTo(6UL));
        }

        [TestCase("12345", true, 12345UL)]
        [TestCase("  18446744073709551615 ", true, ulong.MaxValue)]
        [TestCase("0x3039", true, 12345UL)]
        [TestCase("0XFFFFFFFFFFFFFFFF", true, ulong.MaxValue)]
        [TestCase("", false, 0UL)]
        [TestCase(null, false, 0UL)]
        [TestCase("-1", false, 0UL)]
        [TestCase("1.5", false, 0UL)]
        [TestCase("0xZZ", false, 0UL)]
        [TestCase("18446744073709551616", false, 0UL)]
        public void SeedsAreParsedFromText(string text, bool valid, ulong expected)
        {
            Assert.That(SimulationRandom.TryParse(text, out var seed), Is.EqualTo(valid));
            Assert.That(seed, Is.EqualTo(expected));
        }

        [Test]
        public void SeedsFormatAsDecimalAndRoundTrip()
        {
            Assert.That(SimulationRandom.Format(ulong.MaxValue), Is.EqualTo("18446744073709551615"));
            Assert.That(SimulationRandom.TryParse(SimulationRandom.Format(987654321UL), out var seed), Is.True);
            Assert.That(seed, Is.EqualTo(987654321UL));
        }

        [Test]
        public void CommandLineSeedIsRead()
        {
            Assert.That(SimulationRandom.TryReadCommandLine(new[] { "Cities.exe", "-loglevel", "Verkeer=Debug", "-SEED", "42" }, out var seed, out var error), Is.True);
            Assert.That(seed, Is.EqualTo(42UL));
            Assert.That(error, Is.Null);

            Assert.That(SimulationRandom.TryReadCommandLine(new[] { "Cities.exe" }, out _, out error), Is.False);
            Assert.That(error, Is.Null, "Geen argument is geen fout");
            Assert.That(SimulationRandom.TryReadCommandLine(null, out _, out error), Is.False);
            Assert.That(error, Is.Null);

            Assert.That(SimulationRandom.TryReadCommandLine(new[] { "-seed", "appel" }, out _, out error), Is.False);
            Assert.That(error, Does.Contain("-seed"));
            Assert.That(SimulationRandom.TryReadCommandLine(new[] { "-seed" }, out _, out error), Is.False);
            Assert.That(error, Is.Not.Null);
        }

        [Test]
        public void SeedSurvivesSaveAndLoad()
        {
            var original = new SimulationRandom(0xDEADBEEFCAFEF00DUL);
            var bytes = SaveBytes.Write(new SaveMetadata(), new RandomSection(original));
            var loaded = new SimulationRandom(1);

            SaveBytes.Read(bytes, new RandomSection(loaded));

            Assert.That(loaded.Seed, Is.EqualTo(original.Seed));
            Assert.That(new RandomSection(loaded).Id, Is.EqualTo(RandomSection.SectionId));
            Assert.Throws<ArgumentNullException>(() => _ = new RandomSection(null));
        }

        [Test]
        public void SourcesFromTheSameSeedAgreeAcrossInstances()
        {
            var first = Enumerable.Range(0, 10).Select(i => new SimulationRandom(3).Source(RandomStreamKey.Of("X")).At(i).NextUInt());
            var second = Enumerable.Range(0, 10).Select(i => new SimulationRandom(3).Source(RandomStreamKey.Of("X")).At(i).NextUInt());

            Assert.That(first, Is.EqualTo(second));
        }
    }
}
