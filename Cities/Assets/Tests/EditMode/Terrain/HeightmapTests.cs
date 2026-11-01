using System;
using Cities.World.Terrain;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Terrain
{
    public class HeightmapTests
    {
        // 3×3, zuid naar noord: de middelste kolom is hoger.
        private static Heightmap Hill(float waterLevel = 0f) =>
            new Heightmap(new[] { 0f, 0.5f, 0f, 0f, 1f, 0f, 0f, 0.5f, 0f }, 3, 3, 10f, 100f, waterLevel);

        [Test]
        public void TheGridIsScaledToMetresAndInterpolated()
        {
            var map = Hill();

            Assert.That(map.GroundHeight(new float2(10f, 10f)), Is.EqualTo(100f).Within(1e-3f), "het hoogste punt in het midden");
            Assert.That(map.GroundHeight(new float2(10f, 0f)), Is.EqualTo(50f).Within(1e-3f));
            Assert.That(map.GroundHeight(new float2(5f, 10f)), Is.EqualTo(50f).Within(1e-3f), "halverwege tussen 0 en 1");
            Assert.That(map.GroundHeight(new float2(5f, 5f)), Is.EqualTo(37.5f).Within(1e-3f), "bilineair");
            Assert.That(map.Size, Is.EqualTo(new float2(20f, 20f)));
        }

        [Test]
        public void OutsideTheMapTheEdgeHeightApplies()
        {
            var map = Hill();

            Assert.That(map.GroundHeight(new float2(-50f, 10f)), Is.Zero);
            Assert.That(map.GroundHeight(new float2(10f, 500f)), Is.EqualTo(50f).Within(1e-3f));
            Assert.That(map.GroundHeight(new float2(500f, 500f)), Is.Zero);
        }

        [Test]
        public void WaterCoversLowLandAndTheSurfaceFollowsTheHigherOne()
        {
            var map = Hill(waterLevel: 20f);

            Assert.That(map.IsUnderWater(new float2(0f, 0f)), Is.True);
            Assert.That(map.IsUnderWater(new float2(10f, 10f)), Is.False);
            Assert.That(map.SurfaceHeight(new float2(0f, 0f)), Is.EqualTo(20f), "water");
            Assert.That(map.SurfaceHeight(new float2(10f, 10f)), Is.EqualTo(100f).Within(1e-3f), "land");
        }

        [Test]
        public void Raw16IsReadLittleEndian()
        {
            var data = new byte[] { 0x00, 0x00, 0xFF, 0xFF, 0x00, 0x80, 0xFF, 0x7F };

            var map = Heightmap.FromRaw16(data, 2, 2, 1f, 65535f, 0f);

            Assert.That(map.GroundHeight(new float2(0f, 0f)), Is.Zero);
            Assert.That(map.GroundHeight(new float2(1f, 0f)), Is.EqualTo(65535f).Within(1e-2f));
            Assert.That(map.GroundHeight(new float2(0f, 1f)), Is.EqualTo(32768f).Within(1e-2f));
            Assert.That(map.GroundHeight(new float2(1f, 1f)), Is.EqualTo(32767f).Within(1e-2f));
        }

        [Test]
        public void Gray8IsReadAsFractions()
        {
            var map = Heightmap.FromGray8(new byte[] { 0, 255, 51, 102 }, 2, 2, 1f, 100f, 0f);

            Assert.That(map.GroundHeight(new float2(1f, 0f)), Is.EqualTo(100f).Within(1e-3f));
            Assert.That(map.GroundHeight(new float2(0f, 1f)), Is.EqualTo(20f).Within(1e-3f));
            Assert.That(map.GroundHeight(new float2(1f, 1f)), Is.EqualTo(40f).Within(1e-3f));
        }

        [Test]
        public void BadInputIsRejected()
        {
            var ok = new float[4];

            Assert.Throws<ArgumentNullException>(() => new Heightmap(null, 2, 2, 1f, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Heightmap(new float[1], 1, 1, 1f, 1f, 0f));
            Assert.Throws<ArgumentException>(() => new Heightmap(new float[3], 2, 2, 1f, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Heightmap(ok, 2, 2, 0f, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Heightmap(ok, 2, 2, 1f, 0f, 0f));
            Assert.Throws<ArgumentNullException>(() => Heightmap.FromRaw16(null, 2, 2, 1f, 1f, 0f));
            Assert.Throws<ArgumentException>(() => Heightmap.FromRaw16(new byte[7], 2, 2, 1f, 1f, 0f));
            Assert.Throws<ArgumentNullException>(() => Heightmap.FromGray8(null, 2, 2, 1f, 1f, 0f));
            Assert.Throws<ArgumentException>(() => Heightmap.FromGray8(new byte[3], 2, 2, 1f, 1f, 0f));
        }

        [Test]
        public void TheSourceArrayIsCopied()
        {
            var samples = new[] { 0f, 1f, 0f, 1f };
            var map = new Heightmap(samples, 2, 2, 1f, 10f, 0f);

            samples[1] = 0f;

            Assert.That(map.GroundHeight(new float2(1f, 0f)), Is.EqualTo(10f).Within(1e-3f));
        }
    }
}
