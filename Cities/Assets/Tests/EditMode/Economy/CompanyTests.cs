using System;
using Cities.Simulation.Economy;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Economy
{
    public class CompanyTests
    {
        // 10 werknemers à 100 loon geven 50 eenheden; verkoop voor 30, kosten 5 per eenheid.
        private static Company Make() => new Company(10, 100f, 5f, 30f, 5f, 0f);

        [Test]
        public void ProductionFollowsDemandUpToCapacity()
        {
            var company = Make();

            Assert.That(company.Step(20f).Sold, Is.EqualTo(20f));
            Assert.That(company.Step(500f).Sold, Is.EqualTo(50f), "begrensd door de capaciteit");
            Assert.That(company.Step(-5f).Sold, Is.Zero);
        }

        [Test]
        public void ProfitIsRevenueMinusWagesAndMaterials()
        {
            var company = Make();

            var week = company.Step(50f);

            Assert.That(week.Revenue, Is.EqualTo(1500f));
            Assert.That(week.Wages, Is.EqualTo(1000f));
            Assert.That(week.Materials, Is.EqualTo(250f));
            Assert.That(week.Profit, Is.EqualTo(250f));
            Assert.That(company.Cash, Is.EqualTo(250f));
        }

        [Test]
        public void WagesKeepRunningWithoutDemand()
        {
            var company = Make();

            Assert.That(company.Step(0f).Profit, Is.EqualTo(-1000f));
            Assert.That(company.LossWeeks, Is.EqualTo(1));
        }

        [Test]
        public void StructuralLossClosesTheCompanyButAProfitableWeekResetsTheCount()
        {
            var company = Make();
            for (var i = 0; i < Company.BankruptWeeks - 1; i++)
            {
                company.Step(0f);
            }

            company.Step(50f);
            Assert.That(company.LossWeeks, Is.Zero);
            Assert.That(company.IsClosed, Is.False);

            for (var i = 0; i < Company.BankruptWeeks; i++)
            {
                company.Step(0f);
            }

            var cash = company.Cash;
            Assert.That(company.IsClosed, Is.True);
            Assert.That(company.Step(50f).Sold, Is.Zero, "gesloten bedrijf produceert niet");
            Assert.That(company.Cash, Is.EqualTo(cash));
        }

        [Test]
        public void BadInputIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Company(-1, 1f, 1f, 1f, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Company(1, -1f, 1f, 1f, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Company(1, 1f, 1f, float.NaN, 1f, 0f));
        }
    }
}
