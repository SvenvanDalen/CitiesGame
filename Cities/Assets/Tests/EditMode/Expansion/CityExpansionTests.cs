using System;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Gameplay.Expansion;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Expansion
{
    public class CityExpansionTests
    {
        [Test]
        public void ConsequencesAreOffByDefaultAndDisplayFeaturesAreOn()
        {
            var expansion = new CityExpansion(7);
            var settings = expansion.Settings;

            Assert.That(settings.Seasons && settings.Hints && settings.Timelapse, Is.True);
            Assert.That(settings.Accidents || settings.Winter || settings.Heatwaves || settings.ClimateEffects, Is.False);
            expansion.Daily(10, 1, 1000, 50, snowing: true, sinks: 0);
            Assert.That(expansion.TrafficGrip, Is.EqualTo(1f));
            Assert.That(expansion.WaterDemandFactor, Is.EqualTo(1f));
            Assert.That(expansion.EnergyDemandFactor, Is.EqualTo(1f));
            Assert.That(expansion.Accidents.Enabled, Is.False);
            Assert.That(expansion.Winter.SnowDepth, Is.EqualTo(0f));
        }

        [Test]
        public void TemperatureFollowsTheSeasons()
        {
            Assert.That(CityExpansion.BaseTemperature(7), Is.GreaterThan(CityExpansion.BaseTemperature(1)));
            Assert.That(CityExpansion.BaseTemperature(1), Is.LessThan(5f));
            Assert.That(CityExpansion.BaseTemperature(7), Is.GreaterThan(15f));
        }

        [Test]
        public void WinterBuildsSnowSlowsTrafficAndRaisesHeating()
        {
            var expansion = new CityExpansion(1);
            expansion.Settings.Winter = true;
            for (var day = 0; day < 20; day++)
            {
                expansion.Daily(day, 1, 1000, 50, snowing: true, sinks: 0);
            }

            Assert.That(expansion.Winter.SnowDepth, Is.GreaterThan(5f));
            Assert.That(expansion.TrafficGrip, Is.LessThan(1f));
            Assert.That(expansion.EnergyDemandFactor, Is.GreaterThan(1f));
        }

        [Test]
        public void HeatwavesComeInSummerRunOutAndAreDeterministic()
        {
            var first = RunSummers(5);
            var second = RunSummers(5);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Is.GreaterThan(0), "Over twintig zomers komt er minstens één hittegolf.");
            var winter = new CityExpansion(5);
            winter.Settings.Heatwaves = true;
            for (var day = 0; day < 60; day++)
            {
                winter.Daily(day, 1, 1000, 10, false, 0);
            }

            Assert.That(winter.Heatwaves.Current, Is.Null);
        }

        private static int RunSummers(ulong seed)
        {
            var expansion = new CityExpansion(seed);
            expansion.Settings.Heatwaves = true;
            var started = 0;
            var wasOn = false;
            for (var day = 0; day < 365 * 20; day++)
            {
                var month = 1 + ((day % 365) / 31);
                expansion.Daily(day, Math.Min(month, 12), 1000, 10, false, 0);
                var on = expansion.Heatwaves.Current != null;
                if (on && !wasOn)
                {
                    started++;
                    Assert.That(expansion.WaterDemandFactor, Is.GreaterThan(1f));
                    Assert.That(expansion.EnergyDemandFactor, Is.GreaterThan(1f));
                }

                wasOn = on;
            }

            return started;
        }

        [Test]
        public void EmissionsAndSinksBalanceAndWarmingNeedsTheSwitch()
        {
            var expansion = new CityExpansion(2);
            for (var day = 0; day < 10; day++)
            {
                expansion.Daily(day, 6, 100_000, 10, false, sinks: 0);
            }

            Assert.That(expansion.Carbon.TotalEmitted, Is.GreaterThan(1000d));
            Assert.That(expansion.Carbon.Warming, Is.EqualTo(0f));
            expansion.Daily(10, 6, 0, 10, false, sinks: 100);
            Assert.That(expansion.Carbon.Absorbed, Is.GreaterThan(0d));
            expansion.Settings.ClimateEffects = true;
            expansion.Daily(11, 6, 0, 10, false, sinks: 0);
            Assert.That(expansion.Carbon.ClimateEffects, Is.True);
        }

        [Test]
        public void TimelapseRecordsAndCanBeSwitchedOff()
        {
            var expansion = new CityExpansion(3);
            expansion.Daily(0, 1, 10, 1, false, 0);
            expansion.Daily(7, 1, 20, 2, false, 0);
            Assert.That(expansion.Timelapse.Frames.Count, Is.EqualTo(2));
            expansion.Settings.Timelapse = false;
            expansion.Daily(14, 1, 30, 3, false, 0);
            Assert.That(expansion.Timelapse.Frames.Count, Is.EqualTo(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => expansion.Daily(-1, 1, 0, 0, false, 0));
        }

        [Test]
        public void SectionsRoundTrip()
        {
            var expansion = new CityExpansion(4);
            expansion.Settings.Heatwaves = true;
            expansion.Settings.ClimateEffects = true;
            expansion.Daily(0, 6, 50_000, 5, false, 0);
            var sections = expansion.Sections().ToList();
            var bytes = SaveBytes.Write(new SaveMetadata(), sections.ToArray());

            var other = new CityExpansion(4);
            SaveBytes.Read(bytes, other.Sections().ToArray());

            Assert.That(sections.Count, Is.EqualTo(3));
            Assert.That(other.Carbon.TotalEmitted, Is.EqualTo(expansion.Carbon.TotalEmitted).Within(1e-6));
        }
    }
}
