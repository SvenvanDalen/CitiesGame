using System;
using System.Linq;
using Cities.Simulation.Fields;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Fields
{
    public class DiffusionStepTests
    {
        // Zelfde regel als de job in DiffusionField, maar over een gewone array zodat dit ook in CI draait.
        private static float[] Step(float[] source, int width, int height, in DiffusionSettings settings)
        {
            var target = new float[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                var n = DiffusionStep.Neighbours(i, width, height);
                target[i] = DiffusionStep.Next(source[i], new float4(source[n.x], source[n.y], source[n.z], source[n.w]), settings);
            }

            return target;
        }

        private static float[] Run(float[] map, int size, DiffusionSettings settings, int steps)
        {
            for (var i = 0; i < steps; i++)
            {
                map = Step(map, size, size, settings);
            }

            return map;
        }

        private static float[] Source(int size, int x, int z, float amount)
        {
            var map = new float[size * size];
            map[(z * size) + x] = amount;
            return map;
        }

        [Test]
        public void ASourceSpreadsToItsNeighboursWithoutLosingAnything()
        {
            var map = Run(Source(5, 2, 2, 100f), 5, new DiffusionSettings(0.1f, 0f, float2.zero), 1);

            Assert.That(map[12], Is.EqualTo(60f).Within(1e-4f));
            Assert.That(map[11], Is.EqualTo(10f).Within(1e-4f));
            Assert.That(map[17], Is.EqualTo(10f).Within(1e-4f));
            Assert.That(map.Sum(), Is.EqualTo(100f).Within(1e-3f));
        }

        [Test]
        public void SpreadDoesNotLeakOverTheEdge()
        {
            var map = Run(Source(3, 0, 0, 50f), 3, new DiffusionSettings(0.2f, 0f, float2.zero), 20);

            Assert.That(map.Sum(), Is.EqualTo(50f).Within(1e-3f));
        }

        [Test]
        public void DecayShrinksTheValuesEachStep()
        {
            var map = Run(Source(3, 1, 1, 100f), 3, new DiffusionSettings(0f, 0.25f, float2.zero), 1);

            Assert.That(map[4], Is.EqualTo(75f).Within(1e-4f));
        }

        [Test]
        public void WindCarriesTheValueDownwind()
        {
            var east = new DiffusionSettings(0f, 0f, new float2(0.5f, 0f));
            var map = Run(Source(7, 2, 3, 100f), 7, east, 2);

            Assert.That(map[(3 * 7) + 3], Is.GreaterThan(map[(3 * 7) + 1]), "wind in +x duwt naar hogere x");
            Assert.That(map[(3 * 7) + 3], Is.GreaterThan(map[(4 * 7) + 3]));
            Assert.That(map.Sum(), Is.EqualTo(100f).Within(1e-3f));

            var back = Run(Source(7, 4, 3, 100f), 7, new DiffusionSettings(0f, 0f, new float2(-0.5f, 0.5f)), 1);
            Assert.That(back[(3 * 7) + 3], Is.EqualTo(50f).Within(1e-4f), "wind in -x");
            Assert.That(back[(4 * 7) + 4], Is.EqualTo(50f).Within(1e-4f), "wind in +z");
            Assert.That(back[(3 * 7) + 4], Is.Zero, "de bron is leeg");
        }

        [Test]
        public void NeighboursStayInsideTheMap()
        {
            Assert.That(DiffusionStep.Neighbours(0, 3, 3), Is.EqualTo(new int4(0, 1, 0, 3)));
            Assert.That(DiffusionStep.Neighbours(8, 3, 3), Is.EqualTo(new int4(7, 8, 5, 8)));
            Assert.That(DiffusionStep.Neighbours(4, 3, 3), Is.EqualTo(new int4(3, 5, 1, 7)));
        }

        [Test]
        public void UnstableOrInvalidSettingsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DiffusionSettings(0.3f, 0f, float2.zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DiffusionSettings(0.2f, 0f, new float2(0.3f, 0f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DiffusionSettings(-0.1f, 0f, float2.zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DiffusionSettings(0.1f, 1.5f, float2.zero));
            Assert.That(new DiffusionSettings(0.1f, 0.2f, new float2(0.1f, 0f)).Decay, Is.EqualTo(0.2f));
        }
    }
}
