using System;
using Cities.World.Grid;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Cities.Tests.EditMode.World
{
    /// <summary>Gebruikt NativeArray en draait daarom alleen in Unity, niet in de CI-analyse.</summary>
    [Category("UnityRuntime")]
    public class WorldMapTests
    {
        private static readonly WorldDimensions World = new WorldDimensions(2, 2, 8, 8f);

        [Test]
        public void AllocatesLayersWithExpectedResolution()
        {
            using var map = new WorldMap(World);

            Assert.That(map.Height.Layout, Is.EqualTo(LayerLayout.ForCorners(World)));
            Assert.That(map.WaterDepth.Values.Length, Is.EqualTo(17 * 17));
            Assert.That(map.Zones.Values.Length, Is.EqualTo(16 * 16));
            Assert.That(map.GroundPollution.Values.Length, Is.EqualTo(4 * 4));
            Assert.That(map.LandValue.Layout.Spacing, Is.EqualTo(32f));
            Assert.That(map.MemoryBytes, Is.EqualTo((2 * 289 * 4) + (256 * 2) + (2 * 16 * 4)));
        }

        [Test]
        public void FullMapFitsMemoryBudget()
        {
            using var map = new WorldMap(WorldDimensions.Default);

            Assert.That(map.MemoryBytes, Is.LessThan(64L * 1024 * 1024));
        }

        [Test]
        public void BaseHeightFillsTerrain()
        {
            using var map = new WorldMap(World, 40f);

            Assert.That(map.Height.Range(), Is.EqualTo((40f, 40f)));
            Assert.That(map.WaterDepth.Range(), Is.EqualTo((0f, 0f)));
        }

        [Test]
        public void LayersAreWritableThroughMap()
        {
            using var map = new WorldMap(World);

            map.Height[3, 4] = 12.5f;
            map.Zones.SetForCell(new CellCoord(2, 2), new ZoneCell(ZoneType.Industrial, ZoneDensity.Low));
            map.LandValue.SetForCell(new CellCoord(5, 6), 120f);

            Assert.That(map.Height[3, 4], Is.EqualTo(12.5f));
            Assert.That(map.Zones.GetForCell(new CellCoord(2, 2)).Type, Is.EqualTo(ZoneType.Industrial));
            Assert.That(map.LandValue.GetForCell(new CellCoord(4, 4)), Is.EqualTo(120f), "Grove laag deelt waarde binnen 4×4 cellen");
        }

        [Test]
        public void HeightSamplesBilinearly()
        {
            using var map = new WorldMap(World);
            map.Height[1, 1] = 10f;
            map.Height[2, 1] = 20f;
            map.Height[1, 2] = 30f;
            map.Height[2, 2] = 40f;

            Assert.That(map.Height.Sample(new Vector2(12f, 12f)), Is.EqualTo(25f).Within(1e-4f));
            Assert.That(map.Height.Sample(new Vector2(8f, 8f)), Is.EqualTo(10f).Within(1e-4f));
        }

        [Test]
        public void WaterSurfaceAndUnderwater()
        {
            using var map = new WorldMap(World, 5f);
            map.WaterDepth[3, 3] = 2f;

            Assert.That(map.WaterSurface(3, 3), Is.EqualTo(7f));
            Assert.That(map.IsUnderwater(new CellCoord(2, 2)), Is.True, "Hoekpunt (3,3) is de noordoosthoek van cel (2,2)");
            Assert.That(map.IsUnderwater(new CellCoord(3, 3)), Is.True);
            Assert.That(map.IsUnderwater(new CellCoord(5, 5)), Is.False);
        }

        [Test]
        public void CopyFromRequiresSameLayout()
        {
            using var map = new WorldMap(World);
            var copy = new CellLayer<float>(map.Height.Layout, Allocator.Temp);
            var other = new CellLayer<float>(map.LandValue.Layout, Allocator.Temp);
            try
            {
                map.Height[0, 0] = 3f;
                copy.CopyFrom(map.Height);

                Assert.That(copy[0, 0], Is.EqualTo(3f));
                Assert.Throws<ArgumentException>(() => other.CopyFrom(map.Height));
            }
            finally
            {
                copy.Dispose();
                other.Dispose();
            }
        }

        [Test]
        public void DisposeIsIdempotent()
        {
            var map = new WorldMap(World);

            map.Dispose();
            map.Dispose();

            Assert.That(map.IsDisposed, Is.True);
            Assert.That(map.Height.IsCreated, Is.False);
        }

        [Test]
        public void RejectsDimensionsNotDivisibleByCoarseFactor()
        {
            Assert.Throws<ArgumentException>(() => _ = new WorldMap(new WorldDimensions(1, 1, 6, 8f)));
        }
    }
}
