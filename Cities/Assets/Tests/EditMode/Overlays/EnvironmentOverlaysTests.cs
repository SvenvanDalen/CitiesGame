using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Gameplay.Overlays;
using Cities.Simulation.Fields;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Overlays
{
    public sealed class EnvironmentOverlaysTests
    {
        private static void AddOne(List<OverlayEntityValue> output) =>
            output.Add(new OverlayEntityValue(1, float3.zero, float3.zero, 10f, 50f));

        [Test]
        public void NoSourcesMakesNoOverlays()
        {
            Assert.That(EnvironmentOverlays.Create(4, 4, new EnvironmentSources()), Is.Empty);
        }

        [Test]
        public void NullSourcesThrows()
        {
            Assert.Throws<ArgumentNullException>(() => EnvironmentOverlays.Create(4, 4, null));
        }

        [Test]
        public void EveryFilledSourceMakesAnOverlay()
        {
            var all = new EnvironmentSources
            {
                AirConcentration = (x, z) => 0f,
                NoiseDecibel = (x, z) => 40f,
                SoilConcentration = (x, z) => 0f,
                WaterLevel = (x, z) => 0f,
                WindSpeed = (x, z) => 5f,
                Happiness = AddOne,
                Health = AddOne,
                Education = AddOne,
                Crime = AddOne,
                FireRisk = AddOne,
                Parking = AddOne,
            };
            var ids = EnvironmentOverlays.Create(4, 4, all).Select(s => s.Id).ToList();
            Assert.That(ids, Is.EquivalentTo(new[]
            {
                EnvironmentOverlays.AirId, EnvironmentOverlays.NoiseId, EnvironmentOverlays.SoilId, EnvironmentOverlays.WaterId, EnvironmentOverlays.WindId,
                EnvironmentOverlays.HappinessId, EnvironmentOverlays.HealthId, EnvironmentOverlays.EducationId, EnvironmentOverlays.CrimeId,
                EnvironmentOverlays.FireId, EnvironmentOverlays.ParkingId,
            }));
        }

        [Test]
        public void AirConcentrationBecomesPercentage()
        {
            var sources = new EnvironmentSources { AirConcentration = (x, z) => AirPollution.HalfLevel };
            var air = (ICellOverlaySource)EnvironmentOverlays.Create(4, 4, sources).Single();
            Assert.That(air.Value(1, 1), Is.EqualTo(50f).Within(0.01f));
        }

        [Test]
        public void SoilConcentrationBecomesPercentage()
        {
            var sources = new EnvironmentSources { SoilConcentration = (x, z) => SoilPollution.HalfLevel };
            var soil = (ICellOverlaySource)EnvironmentOverlays.Create(4, 4, sources).Single();
            Assert.That(soil.Value(0, 0), Is.EqualTo(50f).Within(0.01f));
        }

        [Test]
        public void WaterWithoutWaterHasNoData()
        {
            var sources = new EnvironmentSources { WaterLevel = (x, z) => x == 0 ? float.NaN : 2f };
            var water = (ICellOverlaySource)EnvironmentOverlays.Create(2, 2, sources).Single();
            Assert.That(float.IsNaN(water.Value(0, 0)), Is.True);
            Assert.That(water.Value(1, 0), Is.EqualTo(100f));
        }

        [Test]
        public void NoiseAndWindPassThrough()
        {
            var sources = new EnvironmentSources { NoiseDecibel = (x, z) => 65f, WindSpeed = (x, z) => 7f };
            var overlays = EnvironmentOverlays.Create(2, 2, sources).Cast<ICellOverlaySource>().ToDictionary(s => s.Id);
            Assert.That(overlays[EnvironmentOverlays.NoiseId].Value(0, 0), Is.EqualTo(65f));
            Assert.That(overlays[EnvironmentOverlays.WindId].Value(1, 1), Is.EqualTo(7f));
        }

        [Test]
        public void BuildingOverlayCollectsValues()
        {
            var sources = new EnvironmentSources { Happiness = AddOne };
            var happiness = (IEntityOverlaySource)EnvironmentOverlays.Create(2, 2, sources).Single();
            var output = new List<OverlayEntityValue>();
            happiness.Collect(output);
            Assert.That(output.Single().Value, Is.EqualTo(50f));
            Assert.That(happiness.Target, Is.EqualTo(OverlayTarget.Building));
        }

        [Test]
        public void VersionFollowsTheCallback()
        {
            long v = 3;
            var sources = new EnvironmentSources { WindSpeed = (x, z) => 1f };
            var wind = EnvironmentOverlays.Create(2, 2, sources, () => v).Single();
            v = 4;
            Assert.That(wind.Version, Is.EqualTo(4));
        }
    }
}
