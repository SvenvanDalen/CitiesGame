using System;
using Cities.Core.Ids;
using Cities.Simulation.Citizens;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Citizens
{
    public class EducationEconomyTests
    {
        private static Citizen Worker(byte education) =>
            new Citizen { Education = education, Work = new EntityId<BuildingKind>(1, 1) };

        [Test]
        public void HigherEducationEarnsMore()
        {
            Assert.That(EducationEconomy.Wage(0), Is.LessThan(EducationEconomy.Wage(1)));
            Assert.That(EducationEconomy.Wage(1), Is.LessThan(EducationEconomy.Wage(2)));
            Assert.Throws<ArgumentOutOfRangeException>(() => EducationEconomy.Wage(3));
        }

        [Test]
        public void OnlyWorkersEarnAndTheTotalAddsUp()
        {
            var citizens = new[] { Worker(0), Worker(2), new Citizen { Education = 2 } };

            Assert.That(EducationEconomy.WeeklyIncome(citizens[2]), Is.Zero);
            Assert.That(EducationEconomy.TotalWeeklyIncome(citizens), Is.EqualTo(EducationEconomy.Wage(0) + EducationEconomy.Wage(2)));
        }

        [Test]
        public void JobsSplitByShareAndTheRemainderIsNotLost()
        {
            var split = EducationEconomy.SplitJobs(100, 2f, 2f, 1f);

            Assert.That(split, Is.EqualTo(new[] { 40, 40, 20 }));
            Assert.That(EducationEconomy.SplitJobs(10, 1f, 1f, 1f), Is.EqualTo(new[] { 4, 3, 3 }));
            Assert.That(EducationEconomy.SplitJobs(0, 1f, 0f, 0f), Is.EqualTo(new[] { 0, 0, 0 }));
        }

        [Test]
        public void BadSplitsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => EducationEconomy.SplitJobs(-1, 1f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => EducationEconomy.SplitJobs(5, -1f, 1f, 1f));
            Assert.Throws<ArgumentException>(() => EducationEconomy.SplitJobs(5, 0f, 0f, 0f));
        }
    }
}
