using System;
using Cities.Simulation.Utilities;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Utilities
{
    public class LandfillTests
    {
        [Test]
        public void ItTakesWasteUntilFullAndThenCloses()
        {
            var landfill = new Landfill(100f);

            Assert.That(landfill.Dump(60f, 0.02f), Is.EqualTo(60f));
            Assert.That(landfill.IsClosed, Is.False);
            Assert.That(landfill.Fill, Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(landfill.Dump(60f, 0.02f), Is.EqualTo(40f), "past niet meer helemaal");
            Assert.That(landfill.IsClosed, Is.True);
            Assert.That(landfill.Dump(10f, 0.02f), Is.Zero, "dicht");
        }

        [Test]
        public void ThePollutionGrowsWithWhatIsDumped()
        {
            var landfill = new Landfill(100f);

            landfill.Dump(50f, 0.02f);

            Assert.That(landfill.GroundPollution, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void ResidualPollutionOnlyRecoversAfterClosingAndNeverVanishesAtOnce()
        {
            var landfill = new Landfill(100f);
            landfill.Dump(50f, 0.02f);
            landfill.Step();
            Assert.That(landfill.GroundPollution, Is.EqualTo(1f), "open: geen herstel");

            landfill.Dump(50f, 0.02f);
            var atClosing = landfill.GroundPollution;
            landfill.Step();

            Assert.That(landfill.GroundPollution, Is.LessThan(atClosing));
            Assert.That(landfill.GroundPollution, Is.GreaterThan(atClosing * 0.99f));
            for (var i = 0; i < 365; i++)
            {
                landfill.Step();
            }

            Assert.That(landfill.GroundPollution, Is.GreaterThan(atClosing * 0.8f), "na een jaar nog het meeste over");
        }

        [Test]
        public void BadInputIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Landfill(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Landfill(10f).Dump(-1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Landfill(10f).Dump(1f, -1f));
        }
    }
}
