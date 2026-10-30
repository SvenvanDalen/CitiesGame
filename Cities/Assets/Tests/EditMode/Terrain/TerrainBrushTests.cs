using Cities.World.Grid;
using Cities.World.Terrain;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Tests.EditMode.Terrain
{
    /// <summary>De hoogtelaag is een NativeArray; draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class TerrainBrushTests
    {
        private static readonly WorldDimensions Dimensions = new WorldDimensions(1, 1, 32, 8f);
        private static readonly float2 Middle = new float2(128f, 128f);

        [Test]
        public void RaiseLiftsTheMiddleMostAndLeavesTheOutsideAlone()
        {
            using (var map = new WorldMap(Dimensions, 40f))
            {
                var changed = TerrainBrush.Apply(map, TerraformMode.Raise, Middle, 40f, 2f);

                Assert.That(map.Height[16, 16], Is.EqualTo(42f));
                Assert.That(map.Height[19, 16], Is.InRange(40.1f, 41.9f), "zachte rand");
                Assert.That(map.Height[22, 16], Is.EqualTo(40f), "buiten het penseel");
                Assert.That(changed, Is.EqualTo(new RectInt(11, 11, 11, 11)));
            }
        }

        [Test]
        public void LowerStopsAtTheDeepestPoint()
        {
            using (var map = new WorldMap(Dimensions, TerrainBrush.MinHeight + 1f))
            {
                TerrainBrush.Apply(map, TerraformMode.Lower, Middle, 40f, 5f);

                Assert.That(map.Height[16, 16], Is.EqualTo(TerrainBrush.MinHeight));
            }
        }

        [Test]
        public void FlattenAndSetHeightMoveTowardsTheTargetWithoutOvershooting()
        {
            using (var map = new WorldMap(Dimensions, 40f))
            {
                map.Height[16, 16] = 50f;
                map.Height[17, 16] = 40.5f;

                TerrainBrush.Apply(map, TerraformMode.Flatten, Middle, 40f, 4f, 41f);

                Assert.That(map.Height[16, 16], Is.EqualTo(46f), "hoogstens één stap omlaag");
                Assert.That(map.Height[17, 16], Is.EqualTo(41f), "niet voorbij het doel");
            }
        }

        [Test]
        public void SmoothPullsASpikeTowardsItsNeighbours()
        {
            using (var map = new WorldMap(Dimensions, 40f))
            {
                map.Height[16, 16] = 58f;

                TerrainBrush.Apply(map, TerraformMode.Smooth, Middle, 40f, 100f);

                Assert.That(map.Height[16, 16], Is.EqualTo(42f), "gemiddelde van 3 × 3 vóór de streek");
                Assert.That(map.Height[17, 16], Is.EqualTo(42f), "de buur leest de oude piek, niet de al gladde");
            }
        }

        [Test]
        public void TheBrushIsClippedToTheMap()
        {
            using (var map = new WorldMap(Dimensions, 40f))
            {
                var changed = TerrainBrush.Apply(map, TerraformMode.Raise, new float2(-8f, 4f), 20f, 1f);

                Assert.That(changed, Is.EqualTo(new RectInt(0, 0, 2, 4)));
                Assert.That(map.Height[0, 0], Is.GreaterThan(40f));
                Assert.That(TerrainBrush.Apply(map, TerraformMode.Raise, new float2(-500f, 4f), 20f, 1f).width, Is.EqualTo(0));
            }
        }

        [Test]
        public void FalloffIsSmoothFromMiddleToEdge()
        {
            Assert.That(TerrainBrush.Falloff(0f, 10f), Is.EqualTo(1f));
            Assert.That(TerrainBrush.Falloff(5f, 10f), Is.EqualTo(0.5625f));
            Assert.That(TerrainBrush.Falloff(10f, 10f), Is.EqualTo(0f));
            Assert.That(TerrainBrush.Falloff(30f, 10f), Is.EqualTo(0f));
        }
    }
}
