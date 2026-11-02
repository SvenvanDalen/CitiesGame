using System;
using Cities.Simulation.Fields;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Fields
{
    public class NoiseModelTests
    {
        [Test]
        public void LevelDropsSixDecibelPerDoubledDistance()
        {
            var near = NoiseModel.Level(NoiseKind.Industry, 10f, false);
            var far = NoiseModel.Level(NoiseKind.Industry, 20f, false);

            Assert.That(near - far, Is.EqualTo(6.02f).Within(0.01f));
        }

        [Test]
        public void LouderSourcesCarryFurtherAndTheBackgroundIsTheFloor()
        {
            Assert.That(NoiseModel.Level(NoiseKind.Airport, 100f, false), Is.GreaterThan(NoiseModel.Level(NoiseKind.Industry, 100f, false)));
            Assert.That(NoiseModel.Level(NoiseKind.Industry, 100f, false), Is.GreaterThan(NoiseModel.Level(NoiseKind.Road, 100f, false)));
            Assert.That(NoiseModel.LevelAt(new float2(100000f, 0f), new[] { new NoiseSource(float2.zero, NoiseKind.Road) }), Is.EqualTo(NoiseModel.Background).Within(0.01f));
        }

        [Test]
        public void ABarrierRemovesTenDecibelAndAZeroDistanceIsFinite()
        {
            var open = NoiseModel.Level(NoiseKind.Road, 20f, false);
            var walled = NoiseModel.Level(NoiseKind.Road, 20f, true);

            Assert.That(open - walled, Is.EqualTo(NoiseModel.BarrierReduction).Within(1e-4f));
            Assert.That(NoiseModel.Level(NoiseKind.Road, 0f, false), Is.EqualTo(75f));
        }

        [Test]
        public void TwoEqualSourcesAreThreeDecibelLouder()
        {
            Assert.That(NoiseModel.Combine(70f, 70f), Is.EqualTo(73.01f).Within(0.01f));
            Assert.That(NoiseModel.Combine(70f, 30f), Is.EqualTo(70f).Within(0.01f), "een veel zachtere bron verandert bijna niets");
        }

        [Test]
        public void AnnoyanceRunsFromQuietToUnbearable()
        {
            Assert.That(NoiseModel.Annoyance(40f), Is.Zero);
            Assert.That(NoiseModel.Annoyance(65f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(NoiseModel.Annoyance(120f), Is.EqualTo(1f));
        }

        [Test]
        public void TheMapIsLoudestAtTheSourceAndQuietFarAway()
        {
            var map = new float[8 * 8];
            var sources = new[] { new NoiseSource(new float2(4f, 4f), NoiseKind.Industry), new NoiseSource(new float2(4f, 4f), NoiseKind.Road, true) };

            NoiseModel.Fill(map, 8, 8, 8f, sources);

            Assert.That(map[0], Is.GreaterThan(map[(7 * 8) + 7]));
            Assert.That(map[(7 * 8) + 7], Is.GreaterThan(NoiseModel.Background));
            Assert.That(NoiseModel.LevelAt(new float2(500000f, 0f), sources), Is.EqualTo(NoiseModel.Background).Within(0.01f));
        }

        [Test]
        public void BadInputIsRejected()
        {
            var none = Array.Empty<NoiseSource>();

            Assert.Throws<ArgumentException>(() => NoiseModel.Fill(new float[3], 2, 2, 8f, none));
            Assert.Throws<ArgumentException>(() => NoiseModel.Fill(new float[4], 2, 2, 0f, none));
            Assert.Throws<ArgumentException>(() => NoiseModel.Fill(null, 2, 2, 8f, none));
            Assert.Throws<ArgumentOutOfRangeException>(() => NoiseModel.SourceLevel((NoiseKind)99));
        }
    }
}
