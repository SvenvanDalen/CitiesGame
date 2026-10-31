using System;
using Cities.Simulation.Utilities;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Utilities
{
    public class WaterWorksTests
    {
        [Test]
        public void RiverTakesOverPollutionGroundwaterIsFilteredBySoil()
        {
            Assert.That(WaterQuality.Intake(WaterSource.River, 0.4f), Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(WaterQuality.Intake(WaterSource.Groundwater, 0.4f), Is.EqualTo(0.92f).Within(1e-5f));
            Assert.That(WaterQuality.Intake(WaterSource.River, 3f), Is.Zero, "begrensd");
            Assert.That(WaterQuality.Intake(WaterSource.River, -1f), Is.EqualTo(1f));
        }

        [Test]
        public void TreatmentRemovesTheGivenShareOfThePollution()
        {
            Assert.That(WaterQuality.Treat(0.6f, 0.5f), Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(WaterQuality.Treat(0.6f, 0f), Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(WaterQuality.Treat(0.6f, 1f), Is.EqualTo(1f));
            Assert.That(WaterQuality.Treat(2f, 2f), Is.EqualTo(1f), "begrensd");
            Assert.That(WaterQuality.Treat(0.6f, 0.5f), Is.LessThanOrEqualTo(WaterQuality.Drinkable + 1e-5f));
            Assert.That(WaterQuality.Treat(0.6f, 0.6f), Is.GreaterThan(WaterQuality.Drinkable));
        }

        [Test]
        public void TowerBuffersSurplusAndCoversShortagesUntilEmpty()
        {
            var tower = new WaterTower(100f, 20f);

            Assert.That(tower.Step(50f, 30f), Is.EqualTo(30f), "overschot gaat in de toren");
            Assert.That(tower.Level, Is.EqualTo(40f));
            Assert.That(tower.Step(0f, 25f), Is.EqualTo(25f), "tekort uit de toren");
            Assert.That(tower.Step(0f, 25f), Is.EqualTo(15f), "leeg: er is niet meer");
            Assert.That(tower.Level, Is.Zero);
        }

        [Test]
        public void TowerOverflowsAtCapacityAndRejectsBadInput()
        {
            var tower = new WaterTower(100f, 500f);

            Assert.That(tower.Level, Is.EqualTo(100f), "startniveau begrensd");
            Assert.That(tower.Step(40f, 0f), Is.Zero);
            Assert.That(tower.Level, Is.EqualTo(100f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WaterTower(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => tower.Step(-1f, 0f));
        }
    }
}
