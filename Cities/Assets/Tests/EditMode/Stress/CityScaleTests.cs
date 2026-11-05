using System.Diagnostics;
using Cities.Gameplay.Environment;
using Cities.Gameplay.Population;
using Cities.Gameplay.Transit;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Stress
{
    /// <summary>
    /// Rekenwerk van de stadsmodellen bij een grote stad (E26-16): 50.000 inwoners. De grenzen zijn ruim, zodat de test op een trage
    /// buildmachine niet faalt; ze vangen alleen een kwadratische val. De framerate in de build meet de stresstest van E23.
    /// </summary>
    public class CityScaleTests
    {
        private const int Homes = 4200;

        [Test]
        public void FiftyThousandResidentsGrowAndTickQuickly()
        {
            var population = new CityPopulation { ServicesAppeal = 0.9f, LandValueAppeal = 0.9f, TaxAppeal = 0.9f };
            for (var i = 0; i < Homes; i++)
            {
                population.AddBuilding(4, 0, 0);
            }

            for (var i = 0; i < 600; i++)
            {
                population.AddBuilding(0, 55, 0);
            }

            var watch = Stopwatch.StartNew();
            for (var day = 0; day < 365; day++)
            {
                population.Daily();
            }

            watch.Stop();
            Assert.That(population.Population, Is.GreaterThan(10_000));
            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(5_000), "een jaar bevolkingsrekenen met 4800 gebouwen");
        }

        [Test]
        public void EnvironmentOfALargeCityIsQuickToEvaluate()
        {
            var environment = new CityEnvironment();
            var random = new Unity.Mathematics.Random(7);
            for (var i = 0; i < Homes; i++)
            {
                environment.AddHome(random.NextFloat2(0, 8000));
            }

            for (var i = 0; i < 300; i++)
            {
                environment.AddIndustry(random.NextFloat2(0, 8000));
            }

            for (var i = 0; i < 100; i++)
            {
                environment.AddStop(random.NextFloat2(0, 8000));
            }

            var watch = Stopwatch.StartNew();
            var appeal = environment.HomeAppeal(null);
            watch.Stop();

            Assert.That(appeal, Is.InRange(0f, 1f));
            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(2_000), "gemiddelde landwaarde van alle woningen");
        }

        [Test]
        public void ManyBusesMoveWithoutSlowdown()
        {
            var transit = new CityTransit();
            for (var i = 0; i < 40; i++)
            {
                transit.AddStop(new float2(i * 200, (i % 5) * 50));
            }

            var line = transit.CreateLineFromUnlinked("Lijn 1", 100, 10f);
            for (var day = 0; day < 30; day++)
            {
                transit.Daily(50_000);
            }

            var watch = Stopwatch.StartNew();
            for (var tick = 0; tick < 600; tick++)
            {
                transit.Fleet.Tick(0.1f);
                foreach (var bus in transit.Fleet.Buses)
                {
                    transit.PositionOf(bus);
                }
            }

            watch.Stop();
            Assert.That(line, Is.GreaterThanOrEqualTo(0));
            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(5_000), "60 seconden bussen bij een grote lijn");
        }
    }
}
