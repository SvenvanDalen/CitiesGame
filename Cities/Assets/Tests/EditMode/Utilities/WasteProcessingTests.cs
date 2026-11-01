using System;
using Cities.Simulation.Utilities;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Utilities
{
    public class WasteProcessingTests
    {
        [Test]
        public void LandfillTakesEverythingAndPollutesTheGround()
        {
            var result = WasteProcessing.Process(WasteMethod.Landfill, 100f, 1000f);

            Assert.That(result.Processed, Is.EqualTo(100f));
            Assert.That(result.NotAccepted, Is.Zero);
            Assert.That(result.Cost, Is.EqualTo(2000f));
            Assert.That(result.GroundPollution, Is.GreaterThan(0f));
            Assert.That(result.AirPollution, Is.Zero);
            Assert.That(result.Residue, Is.EqualTo(100f), "alles blijft liggen");
        }

        [Test]
        public void IncineratorMakesEnergyAndAirPollutionAndLeavesLittleAsh()
        {
            var result = WasteProcessing.Process(WasteMethod.Incinerator, 100f, 1000f);

            Assert.That(result.EnergyMwh, Is.EqualTo(60f).Within(1e-3f));
            Assert.That(result.AirPollution, Is.GreaterThan(0f));
            Assert.That(result.GroundPollution, Is.Zero);
            Assert.That(result.Residue, Is.EqualTo(10f).Within(1e-3f));
        }

        [Test]
        public void RecyclingAndCompostOnlyTakeTheirShareAndEarnMoney()
        {
            var recycling = WasteProcessing.Process(WasteMethod.Recycling, 100f, 1000f);
            var compost = WasteProcessing.Process(WasteMethod.Compost, 100f, 1000f);

            Assert.That(recycling.Processed, Is.EqualTo(35f).Within(1e-3f));
            Assert.That(recycling.NotAccepted, Is.EqualTo(65f).Within(1e-3f));
            Assert.That(recycling.Revenue, Is.GreaterThan(recycling.Cost), "recycling levert netto geld op");
            Assert.That(compost.Processed, Is.EqualTo(30f).Within(1e-3f));
            Assert.That(compost.Revenue, Is.GreaterThan(0f));
        }

        [Test]
        public void CapacityLimitsWhatIsProcessed()
        {
            var result = WasteProcessing.Process(WasteMethod.Incinerator, 100f, 40f);

            Assert.That(result.Processed, Is.EqualTo(40f));
            Assert.That(result.NotAccepted, Is.EqualTo(60f));
            Assert.That(result.Cost, Is.EqualTo(2400f));
        }

        [Test]
        public void NegativeInputIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => WasteProcessing.Process(WasteMethod.Landfill, -1f, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => WasteProcessing.Process(WasteMethod.Landfill, 1f, -10f));
        }
    }
}
