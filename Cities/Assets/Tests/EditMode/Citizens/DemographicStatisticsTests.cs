using Cities.Core.Ids;
using Cities.Simulation.Citizens;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Citizens
{
    public class DemographicStatisticsTests
    {
        private static Citizen Make(byte age, byte education = 0, bool works = false)
        {
            var citizen = new Citizen { Age = age, Education = education };
            if (works)
            {
                citizen.Work = new EntityId<BuildingKind>(1, 1);
            }

            return citizen;
        }

        [Test]
        public void EmptyCityGivesZeroes()
        {
            var report = DemographicStatistics.Compute(new Citizen[0]);

            Assert.That(report.Adults, Is.Zero);
            Assert.That(report.UnemploymentRate, Is.Zero);
        }

        [Test]
        public void AgePyramidGroupsByFiveYearsAndClampsTheOldest()
        {
            var report = DemographicStatistics.Compute(new[] { Make(0), Make(4), Make(5), Make(Citizen.MaxAge), Make(200) });

            Assert.That(report.AgeBands[0], Is.EqualTo(2));
            Assert.That(report.AgeBands[1], Is.EqualTo(1));
            Assert.That(report.AgeBands[report.AgeBands.Length - 1], Is.EqualTo(2));
        }

        [Test]
        public void UnemploymentCountsOnlyAdults()
        {
            var report = DemographicStatistics.Compute(new[] { Make(30, works: true), Make(40), Make(10), Make(70) });

            Assert.That(report.Adults, Is.EqualTo(2));
            Assert.That(report.Unemployed, Is.EqualTo(1));
            Assert.That(report.UnemploymentRate, Is.EqualTo(0.5f));
        }

        [Test]
        public void EducationSkipsChildrenAndIncomeFollowsWage()
        {
            var report = DemographicStatistics.Compute(new[] { Make(10, 2), Make(30, 0, true), Make(30, 1, true), Make(30, 2, true), Make(30, 2), Make(70, 1) });

            Assert.That(report.Education, Is.EqualTo(new[] { 1, 2, 2 }));
            Assert.That(report.Income, Is.EqualTo(new[] { 1, 1, 1, 1 }));
        }
    }
}
