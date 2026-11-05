using Cities.Gameplay.Visuals;
using Cities.Simulation.Timing;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Atmosphere
{
    public class CityAtmosphereTests
    {
        private static readonly GameDate Date = new GameDate(2030, 6, 21);

        [Test]
        public void NoonIsBrighterThanMidnight()
        {
            var atmosphere = new CityAtmosphere(5);
            atmosphere.Weather.Force(WeatherKind.Clear, 0.5f, 100f, 3f);

            atmosphere.Update(Date, 0.5f, 0f, 10f, 0.5f, 0.5f, 0f);
            var noon = atmosphere.SunLight;
            atmosphere.Update(Date, 0f, 0f, 10f, 0.5f, 0.5f, 0f);

            Assert.That(noon, Is.GreaterThan(atmosphere.SunLight));
            Assert.That(atmosphere.Sky.IsNight, Is.True);
        }

        [Test]
        public void CloudsDimTheSun()
        {
            var atmosphere = new CityAtmosphere(5);
            atmosphere.Weather.Force(WeatherKind.Clear, 0.5f, 100f, 3f);
            atmosphere.Update(Date, 0.5f, 0f, 10f, 0.5f, 0.5f, 0f);
            var clear = atmosphere.SunLight;

            atmosphere.Weather.Force(WeatherKind.Storm, 1f, 100f, 20f);
            atmosphere.Update(Date, 0.5f, 0f, 10f, 0.5f, 0.5f, 0f);

            Assert.That(atmosphere.SunLight, Is.LessThan(clear));
        }

        [Test]
        public void FogComesFromTheWeather()
        {
            var atmosphere = new CityAtmosphere(5);
            atmosphere.Weather.Force(WeatherKind.Fog, 0.8f, 100f, 1f);
            atmosphere.Update(Date, 0.5f, 0f, 10f, 0f, 0f, 0f);
            Assert.That(atmosphere.Fog, Is.GreaterThan(0f));
        }

        [Test]
        public void WeatherChangesOverTime()
        {
            var atmosphere = new CityAtmosphere(9);
            var seen = new System.Collections.Generic.HashSet<WeatherKind>();
            for (var hour = 0; hour < 24 * 60; hour++)
            {
                atmosphere.Update(Date, (hour % 24) / 24f, 1f, 10f, 0f, 0f, 0f);
                seen.Add(atmosphere.Weather.Kind);
            }

            Assert.That(seen.Count, Is.GreaterThan(1));
        }

        [Test]
        public void CityHumFollowsTheBuiltUpArea()
        {
            var atmosphere = new CityAtmosphere(5);
            atmosphere.Update(Date, 0.5f, 0f, 10f, 1f, 0f, 0f);
            var city = atmosphere.Sound.City;
            atmosphere.Update(Date, 0.5f, 0f, 10f, 0f, 0f, 0f);

            Assert.That(city, Is.GreaterThan(atmosphere.Sound.City));
        }
    }
}
