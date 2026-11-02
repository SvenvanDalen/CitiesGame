using System;
using Cities.Simulation.Disasters;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Disasters
{
    public class EarthquakeTests
    {
        [Test]
        public void IntensityFadesWithDistance()
        {
            Assert.That(Earthquake.IntensityAt(7f, 0f), Is.EqualTo(7f));
            Assert.That(Earthquake.IntensityAt(7f, 9000f), Is.EqualTo(5f).Within(1e-4f));
            Assert.That(Earthquake.IntensityAt(1f, 1e6f), Is.Zero);
            Assert.That(Earthquake.IntensityAt(7f, -5f), Is.EqualTo(7f));
        }

        [Test]
        public void StricterCodeAndLowerLevelResistBetter()
        {
            Assert.That(Earthquake.ResistanceOf(BuildingCode.Reinforced, 1), Is.GreaterThan(Earthquake.ResistanceOf(BuildingCode.Basic, 1)));
            Assert.That(Earthquake.ResistanceOf(BuildingCode.Basic, 1), Is.GreaterThan(Earthquake.ResistanceOf(BuildingCode.None, 1)));
            Assert.That(Earthquake.ResistanceOf(BuildingCode.Basic, 3), Is.LessThan(Earthquake.ResistanceOf(BuildingCode.Basic, 1)));
        }

        [Test]
        public void DamageGrowsFromNothingToCollapse()
        {
            Assert.That(Earthquake.Damage(3f, BuildingCode.None, 1), Is.Zero);
            Assert.That(Earthquake.Damage(5f, BuildingCode.None, 1), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(Earthquake.Collapses(6f, BuildingCode.None, 1), Is.True);
            Assert.That(Earthquake.Collapses(6f, BuildingCode.Reinforced, 1), Is.False);
        }

        [Test]
        public void TheSpecIsValidAndHasNoWarning()
        {
            Assert.That(Earthquake.Spec.WarningDays, Is.Zero);
            Assert.That(Earthquake.Spec.Name, Is.EqualTo("Aardbeving"));
        }

        [Test]
        public void BadInputIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Earthquake.ResistanceOf(BuildingCode.Basic, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Earthquake.ResistanceOf((BuildingCode)9, 1));
        }
    }
}
