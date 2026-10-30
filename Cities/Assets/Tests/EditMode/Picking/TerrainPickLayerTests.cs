using System;
using Cities.World.Picking;
using Cities.World.Terrain;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Picking
{
    public class TerrainPickLayerTests
    {
        private static readonly float2 Map = new float2(18432f, 18432f);

        /// <summary>Glooiend landschap met een steile berg rond (6000, 6000).</summary>
        private sealed class HillyTerrain : ITerrainHeight
        {
            public int Samples { get; private set; }

            public float SurfaceHeight(float2 position)
            {
                Samples++;
                var hill = 400f * math.exp(-math.lengthsq(position - new float2(6000f, 6000f)) / (2f * 300f * 300f));
                return 20f + (8f * math.sin(position.x * 0.01f) * math.cos(position.y * 0.013f)) + hill;
            }
        }

        [Test]
        public void StraightDownHitsTheFlatGroundExactly()
        {
            var layer = new TerrainPickLayer(new FlatTerrain(12f), Map);

            Assert.That(layer.Raycast(new PickRay(new float3(500f, 800f, 700f), new float3(0f, -1f, 0f)), 10_000f, out var hit), Is.True);
            Assert.That(hit.Kind, Is.EqualTo(PickKind.Terrain));
            Assert.That(hit.Point, Is.EqualTo(new float3(500f, 12f, 700f)));
            Assert.That(hit.Distance, Is.EqualTo(788f).Within(0.01f));
            Assert.That(hit.Id, Is.Zero);
        }

        [Test]
        public void SlantedRaysLandOnTheSurfaceWithinCentimetres()
        {
            var terrain = new HillyTerrain();
            var layer = new TerrainPickLayer(terrain, Map);
            var origin = new float3(2000f, 900f, 2000f);

            for (var angle = 10; angle < 360; angle += 35)
            {
                math.sincos(math.radians(angle), out var sin, out var cos);
                var ray = new PickRay(origin, new float3(sin, -0.6f, cos));

                Assert.That(layer.Raycast(ray, 50_000f, out var hit), Is.True, $"Hoek {angle}");
                Assert.That(hit.Point.y, Is.EqualTo(terrain.SurfaceHeight(hit.Point.xz)).Within(0.001f));
                Assert.That(math.distance(ray.Point(hit.Distance).xz, hit.Point.xz), Is.LessThan(0.05f));
            }
        }

        [Test]
        public void AHillHidesTheValleyBehindIt()
        {
            var terrain = new HillyTerrain();
            var layer = new TerrainPickLayer(terrain, Map);

            // Laag over de grond richting de berg; het punt erachter op (7000, 6000) is niet zichtbaar.
            var origin = new float3(5000f, 150f, 6000f);
            var ray = new PickRay(origin, new float3(7000f, 20f, 6000f) - origin);

            Assert.That(layer.Raycast(ray, 50_000f, out var hit), Is.True);
            Assert.That(hit.Point.x, Is.LessThan(6000f), "Raakt de voorkant van de berg");
            Assert.That(hit.Point.y, Is.GreaterThan(100f));
        }

        [Test]
        public void HighAboveTheGroundTakesFewSamples()
        {
            var terrain = new HillyTerrain();
            var layer = new TerrainPickLayer(terrain, Map);

            layer.Raycast(new PickRay(new float3(9000f, 3000f, 9000f), new float3(0.3f, -1f, 0.2f)), 50_000f, out _);

            Assert.That(terrain.Samples, Is.LessThan(150));
        }

        [Test]
        public void RaysThatNeverReachTheGroundMiss()
        {
            var layer = new TerrainPickLayer(new FlatTerrain(0f), Map);

            Assert.That(layer.Raycast(new PickRay(new float3(500f, 100f, 500f), new float3(0f, 1f, 0f)), 50_000f, out _), Is.False, "Omhoog");
            Assert.That(layer.Raycast(new PickRay(new float3(500f, 100f, 500f), new float3(1f, 0f, 0f)), 50_000f, out _), Is.False, "Horizontaal");
            Assert.That(layer.Raycast(new PickRay(new float3(500f, 100f, 500f), new float3(0f, -1f, 0f)), 50f, out _), Is.False, "Te kort");
            Assert.That(layer.Raycast(new PickRay(new float3(-500f, 100f, 500f), new float3(0f, -1f, 0f)), 50_000f, out _), Is.False, "Naast de kaart");
            Assert.That(layer.Raycast(new PickRay(new float3(-500f, 100f, 500f), new float3(-1f, -0.1f, 0f)), 50_000f, out _), Is.False, "Van de kaart af");
        }

        [Test]
        public void RaysFromOutsideTheMapLandOnIt()
        {
            var layer = new TerrainPickLayer(new FlatTerrain(0f), Map);

            Assert.That(layer.Raycast(new PickRay(new float3(-1000f, 500f, 900f), new float3(1f, -0.25f, 0f)), 50_000f, out var hit), Is.True);
            Assert.That(hit.Point.x, Is.EqualTo(1000f).Within(0.05f));
        }

        [Test]
        public void StartingBelowTheSurfaceHitsImmediately()
        {
            var layer = new TerrainPickLayer(new FlatTerrain(50f), Map);

            Assert.That(layer.Raycast(new PickRay(new float3(100f, 10f, 100f), new float3(0f, -1f, 0f)), 1000f, out var hit), Is.True);
            Assert.That(hit.Distance, Is.Zero);
            Assert.That(hit.Point.y, Is.EqualTo(50f));
        }

        [Test]
        public void ArgumentsAreChecked()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new TerrainPickLayer(null, Map));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new TerrainPickLayer(FlatTerrain.Zero, new float2(0f, 10f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new TerrainPickLayer(FlatTerrain.Zero, Map, maxSlope: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new TerrainPickLayer(FlatTerrain.Zero, Map, minStep: -1f));
            var layer = new TerrainPickLayer(FlatTerrain.Zero, Map, 1.5f, 2f);
            Assert.That(layer.IsSolid, Is.True);
            Assert.That(layer.MaxSlope, Is.EqualTo(1.5f));
            Assert.That(layer.MinStep, Is.EqualTo(2f));
        }
    }
}
