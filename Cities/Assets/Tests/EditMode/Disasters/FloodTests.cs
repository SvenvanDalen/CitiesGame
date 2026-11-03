using System;
using Cities.Simulation.Disasters;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Disasters
{
    public class FloodTests
    {
        [Test]
        public void TheRiverRisesToThePeakAndFallsBack()
        {
            Assert.That(Flood.RiverLevel(2f, 6f, 4f, 0f), Is.EqualTo(2f));
            Assert.That(Flood.RiverLevel(2f, 6f, 4f, 2f), Is.EqualTo(4f).Within(1e-5f));
            Assert.That(Flood.RiverLevel(2f, 6f, 4f, 4f), Is.EqualTo(6f).Within(1e-5f));
            Assert.That(Flood.RiverLevel(2f, 6f, 4f, 6f), Is.EqualTo(4f).Within(1e-5f));
            Assert.That(Flood.RiverLevel(2f, 6f, 4f, 99f), Is.EqualTo(2f), "na de piek is het weer op basispeil");
            Assert.That(Flood.RiverLevel(2f, 1f, 4f, 4f), Is.EqualTo(2f), "een piek onder het basispeil doet niets");
            Assert.Throws<ArgumentOutOfRangeException>(() => Flood.RiverLevel(0f, 1f, 0f, 1f));
        }

        [Test]
        public void TheWaterFollowsTheTerrain()
        {
            Assert.That(Flood.Depth(5f, 3f), Is.EqualTo(2f));
            Assert.That(Flood.Depth(5f, 8f), Is.Zero, "hoger gelegen land blijft droog");
        }

        [Test]
        public void ATsunamiLosesHeightInland()
        {
            Assert.That(Flood.TsunamiLevel(0f, 10f, 0f), Is.EqualTo(10f));
            Assert.That(Flood.TsunamiLevel(0f, 10f, 500f), Is.EqualTo(5f).Within(1e-5f));
            Assert.That(Flood.TsunamiLevel(1f, 10f, 5000f), Is.EqualTo(1f), "de golf is uitgedoofd");
            Assert.That(Flood.TsunamiLevel(0f, 10f, -50f), Is.EqualTo(10f));
        }

        [Test]
        public void DamageStopsAtTheWaterlineOfTheBuilding()
        {
            Assert.That(Flood.Damage(0f, 12f), Is.Zero);
            Assert.That(Flood.Damage(3f, 12f), Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(Flood.Damage(30f, 12f), Is.EqualTo(1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Flood.Damage(1f, 0f));
        }

        [Test]
        public void TheSpecsAreNamed()
        {
            Assert.That(Flood.RiverSpec.Name, Is.EqualTo("Overstroming"));
            Assert.That(Flood.TsunamiSpec.Name, Is.EqualTo("Tsunami"));
        }
    }
}
