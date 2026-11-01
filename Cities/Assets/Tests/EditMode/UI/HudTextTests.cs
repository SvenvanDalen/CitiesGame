using Cities.Presentation.UI;
using Cities.Simulation.Timing;
using NUnit.Framework;

namespace Cities.Tests.EditMode.UI
{
    public class HudTextTests
    {
        [Test]
        public void MoneyAndPopulationUseDutchGrouping()
        {
            Assert.That(HudText.Money(1250000), Is.EqualTo("€ 1.250.000"));
            Assert.That(HudText.Money(-500), Is.EqualTo("€ -500"));
            Assert.That(HudText.Population(12345), Is.EqualTo("12.345"));
        }

        [Test]
        public void DateTimeShowsDayMonthYearAndClock()
        {
            Assert.That(HudText.DateTime(new GameDate(2030, 2, 3), 0.5f), Is.EqualTo("03-02-2030 12:00"));
            Assert.That(HudText.DateTime(new GameDate(2030, 2, 3), 0f), Is.EqualTo("03-02-2030 00:00"));
            Assert.That(HudText.DateTime(new GameDate(2030, 2, 3), 0.99999f), Is.EqualTo("03-02-2030 23:59"));
        }

        [TestCase(SimulationSpeed.Paused, "Pauze")]
        [TestCase(SimulationSpeed.Normal, "1×")]
        [TestCase(SimulationSpeed.Fast, "2×")]
        [TestCase(SimulationSpeed.Fastest, "3×")]
        public void SpeedHasALabel(SimulationSpeed speed, string label) => Assert.That(HudText.Speed(speed), Is.EqualTo(label));

        [Test]
        public void DemandMapsToMeterValueAndColour()
        {
            Assert.That(HudText.Meter(-1f), Is.Zero);
            Assert.That(HudText.Meter(0f), Is.EqualTo(0.5f));
            Assert.That(HudText.Meter(1f), Is.EqualTo(1f));
            Assert.That(HudText.Meter(5f), Is.EqualTo(1f), "vastgezet");
            Assert.That(HudText.MeterColorFor(0.5f), Is.EqualTo(MeterColor.Success));
            Assert.That(HudText.MeterColorFor(-0.5f), Is.EqualTo(MeterColor.Warning));
            Assert.That(HudText.MeterColorFor(0f), Is.EqualTo(MeterColor.Accent));
        }
    }
}
