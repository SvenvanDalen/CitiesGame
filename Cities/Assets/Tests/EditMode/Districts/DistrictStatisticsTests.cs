using System;
using System.Linq;
using Cities.World.Districts;
using Cities.World.Grid;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Districts
{
    public class DistrictStatisticsTests
    {
        /// <summary>32 cellen van 8 m: 8×8 blokken van 32 m; 256 m kaart.</summary>
        private static readonly WorldDimensions Dimensions = new WorldDimensions(1, 1, 32, 8f);

        private static DistrictMap MapWithCentre(out int id)
        {
            var map = new DistrictMap(Dimensions);
            id = map.Book.Create("Centrum");
            map.Paint(new float2(48f, 48f), 40f, id);
            return map;
        }

        [Test]
        public void CountsPopulationAndEmploymentPerDistrict()
        {
            var map = MapWithCentre(out var id);
            var residents = new[]
            {
                new DistrictResident(40f, 40f, true, true),
                new DistrictResident(50f, 50f, true, false),
                new DistrictResident(45f, 45f, false, false),
                new DistrictResident(240f, 240f, true, true),
            };

            var rows = DistrictStatistics.Compute(map, residents, null, null, null);

            var centre = rows.Single(r => r.Id == id);
            Assert.That(centre.Population, Is.EqualTo(3));
            Assert.That(centre.Workforce, Is.EqualTo(2));
            Assert.That(centre.Unemployed, Is.EqualTo(1));
            Assert.That(centre.UnemploymentRate, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(rows[0].Id, Is.EqualTo(0));
            Assert.That(rows[0].Population, Is.EqualTo(1));
            Assert.That(rows[0].UnemploymentRate, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(centre.DensityPerKm2, Is.GreaterThan(0f));
        }

        [Test]
        public void CountsJobsAndVacancies()
        {
            var map = MapWithCentre(out var id);
            var work = new[]
            {
                new DistrictWorkplace(40f, 40f, 10, 7),
                new DistrictWorkplace(50f, 50f, 5, 9),
                new DistrictWorkplace(200f, 200f, -3, 1),
            };

            var rows = DistrictStatistics.Compute(map, null, work, null, null);

            var centre = rows.Single(r => r.Id == id);
            Assert.That(centre.Jobs, Is.EqualTo(15));
            Assert.That(centre.FilledJobs, Is.EqualTo(12));
            Assert.That(centre.Vacancies, Is.EqualTo(3));
            Assert.That(rows[0].Jobs, Is.EqualTo(0));
        }

        [Test]
        public void SumsIncomePerDistrictAndPerResident()
        {
            var map = MapWithCentre(out var id);
            var income = new[] { new DistrictIncome(40f, 40f, 300), new DistrictIncome(48f, 48f, 100), new DistrictIncome(250f, 10f, 70) };
            var residents = new[] { new DistrictResident(40f, 40f, true, true), new DistrictResident(41f, 41f, true, true) };

            var rows = DistrictStatistics.Compute(map, residents, null, income, null);

            var centre = rows.Single(r => r.Id == id);
            Assert.That(centre.Income, Is.EqualTo(400));
            Assert.That(centre.IncomePerResident, Is.EqualTo(200));
            Assert.That(rows[0].Income, Is.EqualTo(70));
            Assert.That(rows[0].IncomePerResident, Is.EqualTo(0));
        }

        [Test]
        public void AveragesAndPeaksPollutionOverBlocks()
        {
            var map = MapWithCentre(out var id);
            var rows = DistrictStatistics.Compute(map, null, null, null, (x, z) => x > 32f && x < 64f && z > 32f && z < 64f ? 2f : 0.1f);

            var centre = rows.Single(r => r.Id == id);
            Assert.That(centre.PeakPollution, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(centre.AveragePollution, Is.InRange(0.1f, 1f));
            Assert.That(rows[0].AveragePollution, Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(rows[0].PeakPollution, Is.EqualTo(0.1f).Within(0.001f));
        }

        [Test]
        public void SkipsPollutionWithoutSamplerAndRejectsMissingMap()
        {
            var map = MapWithCentre(out _);

            var rows = DistrictStatistics.Compute(map, null, null, null, null);

            Assert.That(rows.Select(r => r.Name), Is.EqualTo(new[] { "Buiten districten", "Centrum" }));
            Assert.That(rows.All(r => r.Population == 0 && r.PeakPollution < 0.0001f), Is.True);
            Assert.Throws<ArgumentNullException>(() => DistrictStatistics.Compute(null, null, null, null, null));
        }

        [Test]
        public void EmptyAreaHasNoDensity()
        {
            var report = new DistrictReport(3, "Leeg", 0f);

            Assert.That(report.DensityPerKm2, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(report.IncomePerResident, Is.EqualTo(0));
        }
    }
}
