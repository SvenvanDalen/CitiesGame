using System.Collections.Generic;
using Cities.World.Terrain;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Terrain
{
    public class TerrainShapeTests
    {
        private static readonly TerrainShapeSettings Settings = TerrainShapeSettings.Default;

        [Test]
        public void SameSeedGivesTheSameLandscape()
        {
            var p = new float2(4321f, 8765f);

            Assert.That(TerrainShape.Height(p, Settings), Is.EqualTo(TerrainShape.Height(p, Settings)));
        }

        [Test]
        public void OtherSeedGivesAnotherLandscape()
        {
            var other = Settings;
            other.Seed = 2;
            var differences = 0;
            for (var i = 0; i < 20; i++)
            {
                var p = new float2(800f * i, 9000f);
                if (math.abs(TerrainShape.Height(p, Settings) - TerrainShape.Height(p, other)) > 0.5f)
                {
                    differences++;
                }
            }

            Assert.That(differences, Is.GreaterThan(15));
        }

        [Test]
        public void LandscapeHasLowlandsAndMountainsOverTheWholeMap()
        {
            var min = float.MaxValue;
            var max = float.MinValue;
            for (var z = 0f; z < 18432f; z += 256f)
            {
                for (var x = 0f; x < 18432f; x += 256f)
                {
                    var h = TerrainShape.Height(new float2(x, z), Settings);
                    min = math.min(min, h);
                    max = math.max(max, h);
                }
            }

            Assert.That(min, Is.GreaterThanOrEqualTo(0f));
            Assert.That(min, Is.LessThan(Settings.BaseHeight));
            Assert.That(max, Is.GreaterThan(150f), "er hoort een bergrug te zijn");
            Assert.That(max, Is.LessThan(Settings.BaseHeight + Settings.PlainsRelief + Settings.HillHeight + Settings.MountainHeight));
        }

        [Test]
        public void RiverBedLiesBelowTheValleySides()
        {
            var depth = 0f;
            var samples = 0;
            for (var z = 1000f; z < 18000f; z += 1500f)
            {
                var river = RiverPoint(z);
                var bed = TerrainShape.Height(river, Settings);
                var away = new float2(Settings.ValleyWidth * 0.5f, 0f);
                var left = TerrainShape.Height(river - away, Settings);
                var right = TerrainShape.Height(river + away, Settings);

                Assert.That(TerrainShape.RiverDistance(river, Settings), Is.LessThan(1f));
                Assert.That(bed, Is.LessThan(math.min(left, right)), $"bij z = {z} is de bedding niet het laagste punt");
                Assert.That(bed, Is.LessThan(Settings.BaseHeight));
                depth += ((left + right) * 0.5f) - bed;
                samples++;
            }

            Assert.That(depth / samples, Is.GreaterThan(Settings.RiverDepth), "het dal is gemiddeld minstens zo diep als de bedding");
        }

        [Test]
        public void MostOfTheMapIsGentleAndOnlyMountainFlanksAreSteep()
        {
            // Hoogteverschil tussen twee hoekpunten (8 m): het grootste deel is bebouwbaar, alleen bergflanken zijn steil,
            // en nergens is het zo steil dat de mesh van 8 m als een muur oogt.
            var rises = new List<float>();
            for (var z = 0f; z < 18432f; z += 97f)
            {
                for (var x = 0f; x < 18432f; x += 211f)
                {
                    var p = new float2(x, z);
                    rises.Add(math.abs(TerrainShape.Height(p + new float2(8f, 0f), Settings) - TerrainShape.Height(p, Settings)));
                }
            }

            rises.Sort();
            Assert.That(rises[rises.Count / 2], Is.LessThan(0.7f), "mediaan onder 5°");
            Assert.That(rises[(int)(rises.Count * 0.9f)], Is.LessThan(2f), "90% onder 14°");
            Assert.That(rises[(int)(rises.Count * 0.99f)], Is.LessThan(6f), "99% onder 37°");
            Assert.That(rises[rises.Count - 1], Is.LessThan(16f), "nergens steiler dan 63°");
        }

        /// <summary>Zoekt op hoogte <paramref name="z"/> de X van de rivieras (de kleinste afstand).</summary>
        private static float2 RiverPoint(float z)
        {
            var best = new float2(0f, z);
            var bestDistance = float.MaxValue;
            for (var x = 0f; x < 18432f; x += 0.5f)
            {
                var d = TerrainShape.RiverDistance(new float2(x, z), Settings);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = new float2(x, z);
                }
            }

            return best;
        }
    }
}
