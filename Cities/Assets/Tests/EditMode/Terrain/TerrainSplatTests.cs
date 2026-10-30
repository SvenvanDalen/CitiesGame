using Cities.World.Grid;
using Cities.World.Terrain;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Tests.EditMode.Terrain
{
    public class TerrainSplatTests
    {
        private static readonly TerrainSplatSettings Settings = TerrainSplatSettings.Default;

        private static float4 Natural(float slope, float height, float water = 0f) =>
            TerrainSplat.Weights(slope, height, water, GroundCover.Natural, Settings);

        [Test]
        public void FlatLowlandIsGrass()
        {
            Assert.That(Natural(3f, 45f), Is.EqualTo(float4.zero));
        }

        [Test]
        public void SteepSlopesTurnToRock()
        {
            Assert.That(Natural(20f, 45f).y, Is.EqualTo(0f));
            Assert.That(Natural(37f, 45f).y, Is.InRange(0.2f, 0.8f), "zachte overgang");
            Assert.That(Natural(50f, 45f).y, Is.EqualTo(1f));
        }

        [Test]
        public void HighPeaksAreRockEvenWhenFlat()
        {
            Assert.That(Natural(2f, 200f).y, Is.EqualTo(0f));
            Assert.That(Natural(2f, 400f).y, Is.EqualTo(1f));
        }

        [Test]
        public void SandLiesAtShoresAndBeaches()
        {
            Assert.That(Natural(2f, 45f, water: 0.5f).x, Is.EqualTo(1f), "oever");
            Assert.That(Natural(2f, 0.5f).x, Is.EqualTo(1f), "strand op zeeniveau");
            Assert.That(Natural(2f, 30f, water: 0.01f).x, Is.EqualTo(0f), "een plas maakt nog geen oever");
        }

        [Test]
        public void SandDoesNotStayOnSlopesOrRock()
        {
            Assert.That(Natural(25f, 45f, water: 0.5f).x, Is.EqualTo(0f));
            var cliff = Natural(50f, 45f, water: 0.5f);
            Assert.That(cliff.x, Is.EqualTo(0f));
            Assert.That(cliff.y, Is.EqualTo(1f));
        }

        [Test]
        public void BuiltGroundOverridesEverything()
        {
            Assert.That(TerrainSplat.Weights(50f, 400f, 1f, GroundCover.Built, Settings), Is.EqualTo(new float4(0f, 0f, 0f, 1f)));
        }

        [Test]
        public void FieldsReplaceGrassButNotRock()
        {
            Assert.That(TerrainSplat.Weights(3f, 45f, 0.5f, GroundCover.Field, Settings), Is.EqualTo(new float4(0f, 0f, 1f, 0f)));
            var steep = TerrainSplat.Weights(50f, 45f, 0f, GroundCover.Field, Settings);
            Assert.That(steep.y, Is.EqualTo(1f));
            Assert.That(steep.z, Is.EqualTo(0f));
        }

        [Test]
        public void WeightsNeverExceedOne()
        {
            for (var slope = 0f; slope < 60f; slope += 3f)
            {
                for (var height = 0f; height < 400f; height += 20f)
                {
                    Assert.That(math.csum(Natural(slope, height, 1f)), Is.LessThanOrEqualTo(1f + 1e-5f), $"{slope}° op {height} m");
                }
            }
        }

        [Test]
        public void SlopeFromCornerHeights()
        {
            Assert.That(TerrainSplat.SlopeDegrees(0f, 0f, 0f, 0f, 8f), Is.EqualTo(0f));
            Assert.That(TerrainSplat.SlopeDegrees(0f, 8f, 0f, 8f, 8f), Is.EqualTo(45f).Within(1e-3f));
            Assert.That(TerrainSplat.SlopeDegrees(0f, 0f, 8f, 8f, 8f), Is.EqualTo(45f).Within(1e-3f));
        }

        [Test]
        public void EncodeRoundsAndClamps()
        {
            Assert.That(TerrainSplat.Encode(new float4(0f, 0.5f, 1f, 2f)), Is.EqualTo(new Color32(0, 128, 255, 255)));
        }
    }
}
