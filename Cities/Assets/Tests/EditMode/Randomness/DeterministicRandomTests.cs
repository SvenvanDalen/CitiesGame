using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Randomness;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Randomness
{
    public class DeterministicRandomTests
    {
        [Test]
        public void MatchesThePcg32ReferenceOutput()
        {
            // pcg32-demo van pcg-random.org: pcg32_srandom_r(&rng, 42u, 54u).
            var random = new DeterministicRandom(42, 54);
            var expected = new uint[] { 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e };

            var actual = expected.Select(_ => random.NextUInt()).ToArray();

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void SameSeedAndStreamGiveTheSameSequence()
        {
            var a = new DeterministicRandom(1234, 7);
            var b = new DeterministicRandom(1234, 7);
            var otherStream = new DeterministicRandom(1234, 8);
            var otherSeed = new DeterministicRandom(1235, 7);

            var first = Take(ref a, 100);

            Assert.That(Take(ref b, 100), Is.EqualTo(first));
            Assert.That(Take(ref otherStream, 100), Is.Not.EqualTo(first));
            Assert.That(Take(ref otherSeed, 100), Is.Not.EqualTo(first));
            Assert.That(a.Stream, Is.EqualTo(7UL));
        }

        [Test]
        public void StateRestoresTheGenerator()
        {
            var random = new DeterministicRandom(99, 3);
            random.NextUInt();
            var restored = DeterministicRandom.FromState(random.State, random.Stream);

            Assert.That(Take(ref restored, 20), Is.EqualTo(Take(ref random, 20)));
        }

        [TestCase(0UL)]
        [TestCase(1UL)]
        [TestCase(1000UL)]
        [TestCase(123457UL)]
        public void AdvanceSkipsExactlyThatManyNumbers(ulong steps)
        {
            var stepped = new DeterministicRandom(5, 11);
            var jumped = stepped;
            for (ulong i = 0; i < steps; i++)
            {
                stepped.NextUInt();
            }

            jumped.Advance(steps);

            Assert.That(jumped.State, Is.EqualTo(stepped.State));
        }

        [Test]
        public void BoundedNumbersAreUniform()
        {
            var random = new DeterministicRandom(2024);
            const int Buckets = 10;
            const int Samples = 200_000;
            var counts = new int[Buckets];
            for (var i = 0; i < Samples; i++)
            {
                counts[random.NextUInt(Buckets)]++;
            }

            // Chi-kwadraat met 9 vrijheidsgraden; 27,9 is de grens bij p = 0,001.
            const double Expected = Samples / (double)Buckets;
            var chiSquare = counts.Sum(c => (c - Expected) * (c - Expected) / Expected);
            Assert.That(chiSquare, Is.LessThan(27.9));
            Assert.That(random.NextUInt(0), Is.Zero);
            Assert.That(random.NextUInt(1), Is.Zero);
        }

        [Test]
        public void LargeBoundsStayInRange()
        {
            var random = new DeterministicRandom(8);
            const uint Bound = 3_000_000_001u;

            for (var i = 0; i < 10_000; i++)
            {
                Assert.That(random.NextUInt(Bound), Is.LessThan(Bound));
            }
        }

        [TestCase(-5, 5)]
        [TestCase(0, 1)]
        [TestCase(int.MinValue, int.MaxValue)]
        [TestCase(100, 103)]
        public void IntegersStayInTheirRange(int min, int maxExclusive)
        {
            var random = new DeterministicRandom(17);
            var seen = new HashSet<int>();
            for (var i = 0; i < 5000; i++)
            {
                var value = random.NextInt(min, maxExclusive);
                Assert.That(value, Is.GreaterThanOrEqualTo(min).And.LessThan(maxExclusive));
                seen.Add(value);
            }

            Assert.That(seen.Count, Is.EqualTo(Math.Min(5000L, (long)maxExclusive - min)).Within(seen.Count / 100), "Elke waarde komt voor bij kleine reeksen");
        }

        [Test]
        public void AnEmptyRangeGivesItsMinimum()
        {
            var random = new DeterministicRandom(1);

            Assert.That(random.NextInt(5, 5), Is.EqualTo(5));
            Assert.That(random.NextInt(9, 2), Is.EqualTo(9));
        }

        [Test]
        public void FractionsCoverTheUnitInterval()
        {
            var random = new DeterministicRandom(3);
            double floatSum = 0;
            double doubleSum = 0;
            const int Samples = 100_000;
            for (var i = 0; i < Samples; i++)
            {
                var f = random.NextFloat();
                var d = random.NextDouble();
                Assert.That(f, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                Assert.That(d, Is.GreaterThanOrEqualTo(0d).And.LessThan(1d));
                floatSum += f;
                doubleSum += d;
            }

            Assert.That(floatSum / Samples, Is.EqualTo(0.5).Within(0.01));
            Assert.That(doubleSum / Samples, Is.EqualTo(0.5).Within(0.01));
            var ranged = random.NextFloat(10f, 20f);
            Assert.That(ranged, Is.GreaterThanOrEqualTo(10f).And.LessThan(20f));
            Assert.That(random.NextULong(), Is.Not.EqualTo(random.NextULong()));
        }

        [Test]
        public void CoinFlipsAndChances()
        {
            var random = new DeterministicRandom(4);
            var heads = Enumerable.Range(0, 10_000).Count(_ => random.NextBool());
            var hits = Enumerable.Range(0, 10_000).Count(_ => random.Chance(0.2f));

            Assert.That(heads, Is.EqualTo(5000).Within(250));
            Assert.That(hits, Is.EqualTo(2000).Within(200));
            Assert.That(Enumerable.Range(0, 1000).Any(_ => random.Chance(0f)), Is.False);
            Assert.That(Enumerable.Range(0, 1000).All(_ => random.Chance(1f)), Is.True);
        }

        [Test]
        public void GaussianHasTheRequestedMeanAndSpread()
        {
            var random = new DeterministicRandom(5);
            var samples = Enumerable.Range(0, 50_000).Select(_ => (double)random.NextGaussian(40f, 12f)).ToList();
            var mean = samples.Average();
            var deviation = Math.Sqrt(samples.Sum(s => (s - mean) * (s - mean)) / samples.Count);

            Assert.That(mean, Is.EqualTo(40).Within(0.3));
            Assert.That(deviation, Is.EqualTo(12).Within(0.3));
        }

        [Test]
        public void PointsLieInsideTheUnitCircleAndFillIt()
        {
            var random = new DeterministicRandom(6);
            var points = Enumerable.Range(0, 20_000).Select(_ => random.NextInsideUnitCircle()).ToList();

            Assert.That(points.All(p => math.lengthsq(p) <= 1f), Is.True);
            // Binnen straal 0,5 ligt een kwart van het oppervlak.
            Assert.That(points.Count(p => math.length(p) < 0.5f) / (double)points.Count, Is.EqualTo(0.25).Within(0.015));
            Assert.That(points.Count(p => p.x < 0f) / (double)points.Count, Is.EqualTo(0.5).Within(0.015));
        }

        [Test]
        public void WeightedChoiceFollowsTheWeights()
        {
            var random = new DeterministicRandom(7);
            var weights = new[] { 1, 0, 3, -4, 6 };
            var counts = new int[weights.Length];
            for (var i = 0; i < 100_000; i++)
            {
                counts[random.NextWeightedIndex(weights)]++;
            }

            Assert.That(counts[1], Is.Zero);
            Assert.That(counts[3], Is.Zero, "Negatieve gewichten tellen als 0");
            Assert.That(counts[0] / 100_000.0, Is.EqualTo(0.1).Within(0.01));
            Assert.That(counts[2] / 100_000.0, Is.EqualTo(0.3).Within(0.01));
            Assert.That(counts[4] / 100_000.0, Is.EqualTo(0.6).Within(0.01));
            Assert.That(random.NextWeightedIndex(new[] { 0, -1 }), Is.EqualTo(-1));
            Assert.That(random.NextWeightedIndex(ReadOnlySpan<int>.Empty), Is.EqualTo(-1));
        }

        [Test]
        public void WeightsAboveTheUIntRangeStillWork()
        {
            var random = new DeterministicRandom(8);
            var weights = new[] { int.MaxValue, int.MaxValue, int.MaxValue };
            var counts = new int[3];
            for (var i = 0; i < 30_000; i++)
            {
                counts[random.NextWeightedIndex(weights)]++;
            }

            Assert.That(counts.All(c => Math.Abs(c - 10_000) < 600), Is.True);
        }

        [Test]
        public void ShuffleIsAPermutationAndListsMatchSpans()
        {
            var array = Enumerable.Range(0, 50).ToArray();
            var list = Enumerable.Range(0, 50).ToList();
            var a = new DeterministicRandom(9);
            var b = new DeterministicRandom(9);

            a.Shuffle(array.AsSpan());
            b.ShuffleList(list);

            Assert.That(array, Is.Not.EqualTo(Enumerable.Range(0, 50)));
            Assert.That(array.OrderBy(x => x), Is.EqualTo(Enumerable.Range(0, 50)));
            Assert.That(list, Is.EqualTo(array));
            Assert.Throws<ArgumentNullException>(() => a.ShuffleList<int>(null));
        }

        [Test]
        public void EveryOrderOfThreeIsEquallyLikely()
        {
            var random = new DeterministicRandom(10);
            var counts = new Dictionary<string, int>();
            var items = new int[3];
            for (var i = 0; i < 60_000; i++)
            {
                items[0] = 0;
                items[1] = 1;
                items[2] = 2;
                random.Shuffle(items.AsSpan());
                var key = string.Concat(items);
                counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;
            }

            Assert.That(counts.Count, Is.EqualTo(6));
            Assert.That(counts.Values.All(c => Math.Abs(c - 10_000) < 500), Is.True);
        }

        private static uint[] Take(ref DeterministicRandom random, int count)
        {
            var values = new uint[count];
            for (var i = 0; i < count; i++)
            {
                values[i] = random.NextUInt();
            }

            return values;
        }
    }
}
