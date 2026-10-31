using System.Collections.Generic;
using Cities.Simulation.Utilities;
using Cities.World.Utilities;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Utilities
{
    public class PowerPlantTests
    {
        private static PowerPlantSpec Plant(PowerSource source) => new PowerPlantSpec
        {
            Source = source,
            FootprintWidth = 2,
            FootprintDepth = 2,
            CapacityMw = 100f,
            ConstructionCost = 1_000_000,
            UpkeepPerMonth = 10_000,
            FuelCostPerMwh = source == PowerSource.Coal ? 25f : 0f,
            AirPollution = source == PowerSource.Coal ? 80f : 0f,
            ConstructionDays = 30,
        };

        private static List<string> Errors(PowerPlantSpec spec)
        {
            var errors = new List<string>();
            spec.Validate(errors);
            return errors;
        }

        [Test]
        public void CoalAlwaysDeliversFullCapacity()
        {
            var coal = Plant(PowerSource.Coal);

            Assert.That(coal.Output(0f, 0f), Is.EqualTo(100f));
            Assert.That(coal.Output(0.5f, 30f), Is.EqualTo(100f));
        }

        [Test]
        public void SolarFollowsTheSunAndIsZeroAtNight()
        {
            var solar = Plant(PowerSource.Solar);

            Assert.That(solar.Output(0f, 0f), Is.Zero, "middernacht");
            Assert.That(solar.Output(0.25f, 0f), Is.Zero, "zonsopgang");
            Assert.That(solar.Output(0.5f, 0f), Is.EqualTo(100f).Within(1e-3f), "middag");
            Assert.That(solar.Output(0.375f, 0f), Is.EqualTo(70.71f).Within(0.01f), "halverwege de ochtend");
            Assert.That(solar.Output(0.8f, 0f), Is.Zero, "avond");
            Assert.That(solar.Output(1.5f, 0f), Is.EqualTo(100f).Within(1e-3f), "de dag erna");
        }

        [Test]
        public void WindNeedsAnBreezeAndStopsInAStorm()
        {
            var wind = Plant(PowerSource.Wind);

            Assert.That(wind.Output(0.5f, 2f), Is.Zero, "te weinig wind");
            Assert.That(wind.Output(0.5f, PowerCurves.WindCutIn), Is.Zero);
            Assert.That(wind.Output(0.5f, 7.5f), Is.EqualTo(100f * 0.125f).Within(1e-3f), "halverwege de curve");
            Assert.That(wind.Output(0.5f, PowerCurves.WindRated), Is.EqualTo(100f));
            Assert.That(wind.Output(0.5f, 20f), Is.EqualTo(100f), "vlak boven het nominale vermogen");
            Assert.That(wind.Output(0.5f, PowerCurves.WindCutOut), Is.Zero, "storm");
        }

        [Test]
        public void PollutionAndFuelFollowTheOutput()
        {
            var coal = Plant(PowerSource.Coal);

            Assert.That(coal.PollutionAt(100f), Is.EqualTo(80f));
            Assert.That(coal.PollutionAt(25f), Is.EqualTo(20f));
            Assert.That(coal.PollutionAt(500f), Is.EqualTo(80f), "niet meer dan vol bedrijf");
            Assert.That(coal.FuelCostPerHour(40f), Is.EqualTo(1000f));
            Assert.That(new PowerPlantSpec().PollutionAt(10f), Is.Zero, "zonder capaciteit");
        }

        [Test]
        public void ValidSpecsPassAndOddOnesAreReported()
        {
            Assert.That(Errors(Plant(PowerSource.Coal)), Is.Empty);
            Assert.That(Errors(Plant(PowerSource.Wind)), Is.Empty);
            Assert.That(Errors(Plant(PowerSource.Solar)), Is.Empty);

            var greenCoal = Plant(PowerSource.Coal);
            greenCoal.FuelCostPerMwh = 0f;
            Assert.That(Errors(greenCoal), Has.Count.EqualTo(1));

            var dirtyWind = Plant(PowerSource.Wind);
            dirtyWind.AirPollution = 5f;
            Assert.That(Errors(dirtyWind), Has.Count.EqualTo(1));

            var none = Plant(PowerSource.Solar);
            none.CapacityMw = 0f;
            none.ConstructionCost = -1;
            Assert.That(Errors(none), Has.Count.EqualTo(2));
        }
    }
}
