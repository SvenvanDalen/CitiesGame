using System;
using Cities.World.Buildings;
using Cities.World.Grid;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Buildings
{
    public class BuildingVolumeTests
    {
        [TestCase(2, ZoneDensity.Low, 2)]
        [TestCase(2, ZoneDensity.High, 6)]
        public void VolumeIsAreaTimesDensity(int cells, ZoneDensity density, int expected)
        {
            Assert.That(BuildingVolume.Of(cells, density), Is.EqualTo(expected));
        }

        [TestCase(2, ZoneDensity.Low, BuildingTypology.Terraced)]
        [TestCase(4, ZoneDensity.Low, BuildingTypology.Detached)]
        [TestCase(9, ZoneDensity.Low, BuildingTypology.Detached)]
        [TestCase(12, ZoneDensity.Low, BuildingTypology.Block)]
        [TestCase(2, ZoneDensity.High, BuildingTypology.Terraced)]
        [TestCase(4, ZoneDensity.High, BuildingTypology.Block)]
        public void VolumeDecidesTheTypology(int cells, ZoneDensity density, BuildingTypology expected)
        {
            Assert.That(BuildingVolume.TypologyFor(cells, density), Is.EqualTo(expected));
        }

        [Test]
        public void OnlyBlocksHaveASharedYard()
        {
            Assert.That(BuildingVolume.YardCells(4, ZoneDensity.Low), Is.Zero);
            Assert.That(BuildingVolume.YardCells(16, ZoneDensity.Low), Is.EqualTo(4));
        }

        [Test]
        public void ALotNeedsACell()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BuildingVolume.Of(0, ZoneDensity.Low));
        }
    }
}
