using Cities.Simulation.Utilities;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Utilities
{
    public class PowerShortageTests
    {
        private static readonly float[] Districts = { 10f, 20f, 30f, 40f };

        [Test]
        public void EnoughSupplyKeepsEveryDistrictLit()
        {
            Assert.That(PowerShortage.Shed(100f, Districts, 0), Is.Empty);
            Assert.That(PowerShortage.Shed(0f, new float[0], 0), Is.Empty);
        }

        [Test]
        public void ShortageShedsWholeDistrictsFromTheEnd()
        {
            var dark = PowerShortage.Shed(60f, Districts, 0);

            Assert.That(dark, Is.EquivalentTo(new[] { 3 }), "100 vraag, 60 aanbod: wijk 3 (40) eraf laat precies 60 over");
            Assert.That(PowerShortage.Shed(35f, Districts, 0), Is.EquivalentTo(new[] { 3, 2 }));
            Assert.That(PowerShortage.Shed(0f, Districts, 0), Has.Count.EqualTo(4));
        }

        [Test]
        public void RotationMovesTheBlackoutToAnotherDistrict()
        {
            Assert.That(PowerShortage.Shed(60f, Districts, 1), Is.EquivalentTo(new[] { 0, 3 }));
            Assert.That(PowerShortage.Shed(60f, Districts, 4), Is.EquivalentTo(new[] { 3 }), "een hele ronde is weer het begin");
            Assert.That(PowerShortage.Shed(60f, Districts, -1), Is.EquivalentTo(new[] { 2, 1 }), "negatieve rotatie rekent netjes door");
        }

        [Test]
        public void MissingDemandListIsRejected()
        {
            Assert.Throws<System.ArgumentNullException>(() => PowerShortage.Shed(1f, null, 0));
        }

        [Test]
        public void DarkDistrictsLoseStreetLightsLitOnesFollowTheNight()
        {
            Assert.That(PowerShortage.StreetLight(true, 0f), Is.Zero);
            Assert.That(PowerShortage.StreetLight(false, 0f), Is.EqualTo(1f), "middernacht");
            Assert.That(PowerShortage.StreetLight(false, 0.5f), Is.Zero.Within(1e-3f), "middag");
        }

        [Test]
        public void HappinessFallsInTheDarkRecoversSlowlyAndStaysInRange()
        {
            Assert.That(PowerShortage.Happiness(0.5f, true), Is.EqualTo(0.5f - PowerShortage.HappinessLossPerDay).Within(1e-5f));
            Assert.That(PowerShortage.Happiness(0.5f, false), Is.EqualTo(0.5f + PowerShortage.HappinessRecoveryPerDay).Within(1e-5f));
            Assert.That(PowerShortage.Happiness(0.05f, true), Is.Zero);
            Assert.That(PowerShortage.Happiness(0.99f, false), Is.EqualTo(1f));
        }
    }
}
