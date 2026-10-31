using Cities.Gameplay.Zoning;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Zoning
{
    public class ZoneRulesTests
    {
        private const float Cell = 8f;

        [Test]
        public void ARowRightNextToTheRoadIsAllowedUpToFourCellsDeep()
        {
            Assert.That(ZoneRules.Reason(4f, Cell, underwater: false), Is.Null, "eerste rij: midden een halve cel van de rand");
            Assert.That(ZoneRules.Reason(28f, Cell, underwater: false), Is.Null, "vierde rij");
            Assert.That(ZoneRules.Reason(32f, Cell, underwater: false), Is.Null, "precies op de grens");
        }

        [Test]
        public void CellsOnTheRoadOrTooFarAreRefused()
        {
            Assert.That(ZoneRules.Reason(-2f, Cell, underwater: false), Does.Contain("weg"));
            Assert.That(ZoneRules.Reason(1f, Cell, underwater: false), Does.Contain("weg"), "een cel die half op het wegdek zou liggen");
            Assert.That(ZoneRules.Reason(36f, Cell, underwater: false), Does.Contain("Te ver"));
            Assert.That(ZoneRules.Reason(float.PositiveInfinity, Cell, underwater: false), Does.Contain("Te ver"), "geen weg");
        }

        [Test]
        public void WaterBeatsEverything()
        {
            Assert.That(ZoneRules.Reason(10f, Cell, underwater: true), Is.EqualTo("Onder water"));
        }
    }
}
