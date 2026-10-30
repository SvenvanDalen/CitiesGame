using System;
using Cities.World.Water;
using NUnit.Framework;

namespace Cities.Tests.EditMode.World
{
    public class ShallowWaterTests
    {
        private const float Cell = 32f;
        private const float Dt = 0.1f;

        private static WaterSettings Closed()
        {
            var settings = WaterSettings.Default;
            settings.Sea = SeaEdges.None;
            return settings;
        }

        private sealed class Basin
        {
            public Basin(int width, int depth, WaterSettings settings)
            {
                Width = width;
                Depth = depth;
                Settings = settings;
                Ground = new float[width * depth];
                Water = new float[width * depth];
                Flux = new float[width * depth * ShallowWater.Directions];
                Source = new float[width * depth];
            }

            public int Width { get; }

            public int Depth { get; }

            public WaterSettings Settings { get; }

            public float[] Ground { get; }

            public float[] Water { get; }

            public float[] Flux { get; }

            public float[] Source { get; }

            public double Volume()
            {
                double total = 0;
                foreach (var depth in Water)
                {
                    total += depth;
                }

                return total * Cell * Cell;
            }

            public void Run(int steps)
            {
                for (var step = 0; step < steps; step++)
                {
                    for (var row = 0; row < Depth; row++)
                    {
                        ShallowWater.UpdateFlux(row, Width, Depth, Cell, Dt, Settings, Ground, Water, Flux);
                    }

                    for (var row = 0; row < Depth; row++)
                    {
                        ShallowWater.UpdateDepth(row, Width, Depth, Cell, Dt, Settings, Ground, Flux, Source, Water);
                    }
                }
            }
        }

        [Test]
        public void ADrySlopeWithoutWaterStaysDry()
        {
            var basin = new Basin(8, 8, Closed());
            for (var i = 0; i < basin.Ground.Length; i++)
            {
                basin.Ground[i] = i % 8;
            }

            basin.Run(50);

            Assert.That(basin.Volume(), Is.EqualTo(0.0));
        }

        [Test]
        public void WaterInAClosedBasinIsNeitherLostNorCreated()
        {
            var basin = new Basin(16, 16, Closed());
            for (var i = 0; i < basin.Ground.Length; i++)
            {
                basin.Ground[i] = (i % 16) * 0.5f;
            }

            basin.Water[(8 * 16) + 8] = 6f;
            var before = basin.Volume();

            basin.Run(400);

            Assert.That(basin.Volume(), Is.EqualTo(before).Within(before * 1e-4));
            Assert.That(basin.Water, Has.All.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void WaterRunsDownhillAndSettlesLevel()
        {
            var basin = new Basin(12, 4, Closed());
            for (var i = 0; i < basin.Ground.Length; i++)
            {
                basin.Ground[i] = 4f - ((i % 12) * 0.25f);
            }

            for (var z = 0; z < 4; z++)
            {
                basin.Water[(z * 12) + 0] = 1.5f;
            }

            basin.Run(3000);

            var surfaceLow = basin.Ground[11] + basin.Water[11];
            var surfaceNext = basin.Ground[10] + basin.Water[10];
            Assert.That(basin.Water[0], Is.LessThan(0.01f), "de hoge kant is leeggelopen");
            Assert.That(basin.Water[11], Is.GreaterThan(0.1f), "het water ligt aan de lage kant");
            Assert.That(surfaceLow, Is.EqualTo(surfaceNext).Within(0.05f), "de waterspiegel is vlak geworden");
        }

        [Test]
        public void ASourceAddsItsDischargeToTheBasin()
        {
            var basin = new Basin(8, 8, Closed());
            basin.Source[(4 * 8) + 4] = 50f;

            basin.Run(100);

            Assert.That(basin.Volume(), Is.EqualTo(50.0 * 100 * Dt).Within(1.0));
        }

        [Test]
        public void TheSeaFillsALowEdgeAndDrainsAHighOne()
        {
            var settings = Closed();
            settings.Sea = SeaEdges.West;
            settings.SeaLevel = 2f;
            var low = new Basin(6, 4, settings);
            var high = new Basin(6, 4, settings);
            for (var i = 0; i < low.Ground.Length; i++)
            {
                low.Ground[i] = 0f;
                high.Ground[i] = 10f;
                high.Water[i] = 1f;
            }

            low.Run(2000);
            high.Run(2000);

            Assert.That(low.Water[0] + low.Ground[0], Is.EqualTo(2f).Within(0.05f), "de rand staat op zeeniveau");
            Assert.That(low.Water[5], Is.GreaterThan(1.5f), "de zee loopt het land op");
            Assert.That(high.Water[0], Is.EqualTo(0f), "water boven zee loopt weg");
        }

        [Test]
        public void ADownsampledGridKeepsEveryNthCorner()
        {
            var fine = new float[9 * 9];
            for (var i = 0; i < fine.Length; i++)
            {
                fine[i] = i;
            }

            var coarse = new float[3 * 3];
            for (var row = 0; row < 3; row++)
            {
                ShallowWater.DownsampleRow(row, fine, 9, 4, 3, coarse);
            }

            Assert.That(coarse, Is.EqualTo(new float[] { 0, 4, 8, 36, 40, 44, 72, 76, 80 }));
        }

        [Test]
        public void UpsamplingFollowsTheFineTerrainAtTheShore()
        {
            // Grof: 2×2 hoekpunten, links 1 m water op grond 0, rechts droog op grond 0. Fijn: 5 hoekpunten breed.
            var coarseGround = new float[4];
            var coarseWater = new[] { 1f, 0f, 1f, 0f };
            var fineGround = new float[5 * 5];
            for (var i = 0; i < fineGround.Length; i++)
            {
                fineGround[i] = (i % 5) * 0.2f;
            }

            var fine = new float[5 * 5];
            for (var row = 0; row < 5; row++)
            {
                ShallowWater.UpsampleRow(row, coarseGround, coarseWater, 2, 2, 4, fineGround, 5, fine);
            }

            Assert.That(fine[0], Is.EqualTo(1f).Within(1e-4f), "diep water links");
            Assert.That(fine[2], Is.EqualTo(0.5f - 0.4f).Within(1e-4f), "halverwege: spiegel 0,5 boven terrein 0,4");
            Assert.That(fine[4], Is.EqualTo(0f), "rechts is het droog, ook al is de grove buur nat");
        }

        [Test]
        public void ADryNeighbourhoodStaysDryEvenOnLowGround()
        {
            var fine = new float[5 * 5];
            for (var row = 0; row < 5; row++)
            {
                ShallowWater.UpsampleRow(row, new float[4], new float[4], 2, 2, 4, new float[5 * 5], 5, fine);
            }

            Assert.That(fine, Has.All.EqualTo(0f));
        }
    }
}
