using Cities.World.Terrain;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Terrain
{
    public class WorldSeedTests
    {
        [Test]
        public void SameWorldSeedGivesSameSeeds()
        {
            Assert.That(WorldSeed.Terrain(12345UL), Is.EqualTo(WorldSeed.Terrain(12345UL)));
            Assert.That(WorldSeed.Forest(12345UL), Is.EqualTo(WorldSeed.Forest(12345UL)));
        }

        [Test]
        public void DifferentWorldSeedsGiveDifferentSeeds()
        {
            Assert.That(WorldSeed.Terrain(1UL), Is.Not.EqualTo(WorldSeed.Terrain(2UL)));
            Assert.That(WorldSeed.Forest(1UL), Is.Not.EqualTo(WorldSeed.Forest(2UL)));
        }

        [Test]
        public void TerrainSeedIsNeverNegative()
        {
            for (ulong seed = 0; seed < 500; seed++)
            {
                Assert.That(WorldSeed.Terrain(seed * 7919UL), Is.GreaterThanOrEqualTo(0));
            }
        }
    }
}
