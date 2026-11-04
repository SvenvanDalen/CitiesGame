using System;
using Cities.Simulation.Fields;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Fields
{
    public class ForestSinkTests
    {
        [Test]
        public void NoForestAbsorbsNothingAndFullForestTheMaximum()
        {
            Assert.That(ForestSink.Absorption(ForestPollutant.Air, 0f), Is.Zero);
            Assert.That(ForestSink.Absorption(ForestPollutant.Air, 1f), Is.EqualTo(ForestSink.MaxAirAbsorption).Within(1e-6f));
            Assert.That(ForestSink.Absorption(ForestPollutant.Soil, 1f), Is.EqualTo(ForestSink.MaxSoilAbsorption).Within(1e-6f));
            Assert.That(ForestSink.Absorption(ForestPollutant.Air, 7f), Is.EqualTo(ForestSink.MaxAirAbsorption).Within(1e-6f), "begrensd");
            Assert.That(ForestSink.Absorption(ForestPollutant.Air, -1f), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => ForestSink.Absorption((ForestPollutant)9, 0.5f));
        }

        [Test]
        public void TheResponseSaturates()
        {
            var first = ForestSink.Response(0.25f) - ForestSink.Response(0f);
            var last = ForestSink.Response(1f) - ForestSink.Response(0.75f);

            Assert.That(first, Is.GreaterThan(last));
            Assert.That(ForestSink.Response(0.5f), Is.GreaterThan(0.5f));
            Assert.That(ForestSink.Response(0.2f), Is.LessThan(ForestSink.Response(0.6f)));
        }

        [Test]
        public void SoilIsCleanedFarSlowerThanAir()
        {
            Assert.That(ForestSink.Absorption(ForestPollutant.Soil, 0.8f), Is.LessThan(ForestSink.Absorption(ForestPollutant.Air, 0.8f) / 5f));
        }

        [Test]
        public void ForestReducesNoise()
        {
            Assert.That(ForestSink.NoiseFactor(0f), Is.EqualTo(1f));
            Assert.That(ForestSink.NoiseFactor(1f), Is.EqualTo(1f - ForestSink.MaxNoiseReduction).Within(1e-6f));
            Assert.That(ForestSink.NoiseFactor(0.3f), Is.GreaterThan(ForestSink.NoiseFactor(0.9f)));
        }

        [Test]
        public void DensityMapsToCover()
        {
            Assert.That(ForestSink.Cover(0), Is.Zero);
            Assert.That(ForestSink.Cover(255), Is.EqualTo(1f));
        }

        [Test]
        public void AbsorbReducesOnlyForestedCellsAndReportsTheTotal()
        {
            var values = new[] { 100f, 100f, 100f };
            var cover = new[] { 0f, 0.5f, 1f };

            var absorbed = ForestSink.Absorb(values, cover, ForestPollutant.Air);

            Assert.That(values[0], Is.EqualTo(100f));
            Assert.That(values[1], Is.LessThan(100f));
            Assert.That(values[2], Is.LessThan(values[1]));
            Assert.That(absorbed, Is.EqualTo(300f - values[0] - values[1] - values[2]).Within(1e-3f));
        }

        [Test]
        public void ARepeatedStepKeepsTheValuePositiveAndFalling()
        {
            var values = new[] { 10f };
            var cover = new[] { 1f };
            var previous = values[0];
            for (var i = 0; i < 200; i++)
            {
                ForestSink.Absorb(values, cover, ForestPollutant.Air);
                Assert.That(values[0], Is.LessThan(previous));
                Assert.That(values[0], Is.GreaterThan(0f));
                previous = values[0];
            }
        }

        [Test]
        public void AbsorbChecksItsArguments()
        {
            Assert.Throws<ArgumentNullException>(() => ForestSink.Absorb((float[])null, new float[1], ForestPollutant.Air));
            Assert.Throws<ArgumentNullException>(() => ForestSink.Absorb(new float[1], null, ForestPollutant.Air));
            Assert.Throws<ArgumentException>(() => ForestSink.Absorb(new float[2], new float[1], ForestPollutant.Air));
            Assert.Throws<ArgumentNullException>(() => ForestSink.Absorb((SoilPollution)null, new float[1]));
            Assert.Throws<ArgumentNullException>(() => ForestSink.Absorb(new SoilPollution(1, 1), null));
            Assert.Throws<ArgumentException>(() => ForestSink.Absorb(new SoilPollution(2, 2), new float[3]));
        }

        [Test]
        public void SoilUnderForestRecoversButBareSoilDoesNot()
        {
            var soil = new SoilPollution(2, 1);
            soil.Add(0, 0, 100f);
            soil.Add(1, 0, 100f);

            var absorbed = ForestSink.Absorb(soil, new[] { 1f, 0f });

            Assert.That(soil[0, 0], Is.LessThan(100f));
            Assert.That(soil[1, 0], Is.EqualTo(100f));
            Assert.That(absorbed, Is.EqualTo(100f - soil[0, 0]).Within(1e-4f));
        }

        [Test]
        public void CoverMapAveragesBlocks()
        {
            // 4x2 fijne cellen naar 2x1: links 255,255,0,0 geeft 0,5; rechts allemaal 255 geeft 1.
            var density = new byte[] { 255, 255, 255, 255, 0, 0, 255, 255 };

            var cover = ForestSink.CoverMap(density, 4, 2, 2, 1);

            Assert.That(cover, Has.Length.EqualTo(2));
            Assert.That(cover[0], Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(cover[1], Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void CoverMapChecksItsArguments()
        {
            Assert.Throws<ArgumentNullException>(() => ForestSink.CoverMap(null, 2, 2, 1, 1));
            Assert.Throws<ArgumentException>(() => ForestSink.CoverMap(new byte[3], 2, 2, 1, 1));
            Assert.Throws<ArgumentException>(() => ForestSink.CoverMap(new byte[4], 2, 2, 4, 4));
        }

        [Test]
        public void AForestBetweenFactoryAndTownCatchesTheDrift()
        {
            const int w = 24;
            const int h = 5;
            var settings = new DiffusionSettings(0.05f, 0.002f, new Unity.Mathematics.float2(0.3f, 0f));
            var cover = new float[w * h];
            for (var z = 0; z < h; z++)
            {
                for (var x = 8; x < 14; x++)
                {
                    cover[(z * w) + x] = 1f;
                }
            }

            var open = Simulate(settings, new float[w * h], w, h);
            var wooded = Simulate(settings, cover, w, h);

            Assert.That(wooded[(2 * w) + 20], Is.LessThan(open[(2 * w) + 20]));
        }

        private static float[] Simulate(DiffusionSettings settings, float[] cover, int w, int h)
        {
            var values = new float[w * h];
            for (var step = 0; step < 150; step++)
            {
                values[(2 * w) + 2] += 20f;
                var next = new float[values.Length];
                for (var i = 0; i < values.Length; i++)
                {
                    var n = DiffusionStep.Neighbours(i, w, h);
                    var neighbours = new Unity.Mathematics.float4(values[n.x], values[n.y], values[n.z], values[n.w]);
                    next[i] = DiffusionStep.Next(values[i], neighbours, settings);
                }

                values = next;
                ForestSink.Absorb(values, cover, ForestPollutant.Air);
            }

            return values;
        }
    }
}
