using Cities.Gameplay.Population;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Balance
{
    public class CityHintsTests
    {
        private const long Rich = 1_000_000;

        [Test]
        public void WithoutRoadsTheFirstStepIsARoadAndThenAZone()
        {
            Assert.That(CityHints.Next(new HintInputs(0, 0, 0, 0, 0, Rich, roads: 0)), Does.StartWith("Stap 1"));
            Assert.That(CityHints.Next(new HintInputs(0, 0, 0, 0, 0, Rich, roads: 1)), Does.StartWith("Stap 2"));
        }

        [Test]
        public void ANewCityIsToldToBuildRoadsAndZones()
        {
            Assert.That(CityHints.Next(new HintInputs(0, 0, 0, 0, 0, Rich)), Does.Contain("weg"));
        }

        [Test]
        public void HomesWithoutJobsGetAJobsHint()
        {
            Assert.That(CityHints.Next(new HintInputs(3, 0, 0, 0, 0, Rich)), Does.Contain("banen"));
        }

        [Test]
        public void ABiggerCityWithoutServicesIsToldToBuildThem()
        {
            Assert.That(CityHints.Next(new HintInputs(20, 100, CityHints.ServicesFromPopulation, 0, 0, Rich)), Does.Contain("brandweer"));
        }

        [Test]
        public void ALargeCityWithoutTransitIsToldToBuildStops()
        {
            Assert.That(CityHints.Next(new HintInputs(80, 400, CityHints.TransitFromPopulation, 2, 0, Rich)), Does.Contain("bushaltes"));
        }

        [Test]
        public void LowMoneyComesFirst()
        {
            Assert.That(CityHints.Next(new HintInputs(0, 0, 0, 0, 0, 10)), Does.Contain("geld"));
        }

        [Test]
        public void AWellRunCityHearsNothing()
        {
            Assert.That(CityHints.Next(new HintInputs(80, 400, 900, 3, 4, Rich)), Is.Empty);
        }
    }
}
