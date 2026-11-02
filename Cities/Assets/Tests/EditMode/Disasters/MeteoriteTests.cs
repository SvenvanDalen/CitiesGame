using System;
using Cities.Simulation.Disasters;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Disasters
{
    public class MeteoriteTests
    {
        [Test]
        public void TheCraterScalesWithTheMeteorite()
        {
            Assert.That(Meteorite.CraterRadius(10f), Is.EqualTo(100f));
            Assert.That(Meteorite.BlastRadius(10f), Is.EqualTo(800f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Meteorite.CraterRadius(0f));
        }

        [Test]
        public void EverythingInTheCraterIsGoneAndDamageFadesOutside()
        {
            Assert.That(Meteorite.DamageAt(10f, 0f), Is.EqualTo(1f));
            Assert.That(Meteorite.DamageAt(10f, 100f), Is.EqualTo(1f));
            Assert.That(Meteorite.DamageAt(10f, 450f), Is.EqualTo(0.25f).Within(1e-5f), "halverwege tussen krater en schokgolfrand");
            Assert.That(Meteorite.DamageAt(10f, 800f), Is.Zero);
            Assert.That(Meteorite.DamageAt(10f, 5000f), Is.Zero);
            Assert.That(Meteorite.DamageAt(10f, -3f), Is.EqualTo(1f));
        }

        [Test]
        public void FireReachesLessFarThanTheBlast()
        {
            Assert.That(Meteorite.FireChanceAt(10f, 100f), Is.EqualTo(1f));
            Assert.That(Meteorite.FireChanceAt(10f, 250f), Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(Meteorite.FireChanceAt(10f, 400f), Is.Zero);
            Assert.That(Meteorite.DamageAt(10f, 400f), Is.GreaterThan(0f));
        }

        [Test]
        public void ItComesWithoutWarning()
        {
            Assert.That(Meteorite.Spec.WarningDays, Is.Zero);
            Assert.That(Meteorite.Spec.Name, Is.EqualTo("Meteorietinslag"));
        }
    }
}
