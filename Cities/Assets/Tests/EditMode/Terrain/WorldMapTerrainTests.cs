using System;
using Cities.World.Grid;
using Cities.World.Terrain;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Terrain
{
    /// <summary>Gebruikt NativeArray-lagen en draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class WorldMapTerrainTests
    {
        [Test]
        public void SurfaceIsTerrainPlusWater()
        {
            using var map = new WorldMap(new WorldDimensions(1, 1, 8, 8f), 10f);
            map.WaterDepth[2, 2] = 3f;
            var terrain = new WorldMapTerrain(map);

            Assert.That(terrain.SurfaceHeight(new float2(16f, 16f)), Is.EqualTo(13f).Within(1e-4f));
            Assert.That(terrain.SurfaceHeight(new float2(48f, 48f)), Is.EqualTo(10f).Within(1e-4f));
            Assert.That(terrain.SurfaceHeight(new float2(20f, 16f)), Is.EqualTo(11.5f).Within(1e-4f), "Halverwege interpoleert");
        }

        [Test]
        public void RequiresMap()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new WorldMapTerrain(null));
        }
    }
}
