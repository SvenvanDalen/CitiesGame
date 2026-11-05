using System.Collections.Generic;
using Cities.Gameplay.Policies;
using Cities.Simulation.Economy;
using Cities.Simulation.Policies;
using Cities.World.Grid;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.PolicyHost
{
    public class CityPoliciesTests
    {
        private static CityPolicies New() => new CityPolicies(
            new Dictionary<string, PolicySpec>
            {
                ["policy.city"] = new PolicySpec { Scope = PolicyScope.City, ActivationCost = 100, MonthlyCost = 300 },
                ["policy.district"] = new PolicySpec { Scope = PolicyScope.District, ActivationCost = 10, MonthlyCostPerCitizen = 0.1f },
            },
            new WorldDimensions(1, 1, 32, 8f));

        [Test]
        public void CityWidePopulationIsTheCityPopulation()
        {
            Assert.That(New().PopulationIn(PolicyBook.CityWide, 500), Is.EqualTo(500));
        }

        [Test]
        public void UnpaintedDistrictHasNoPopulation()
        {
            var policies = New();
            var id = policies.Districts.Book.Create("Noord");
            Assert.That(policies.PopulationIn(id, 500), Is.EqualTo(0));
        }

        [Test]
        public void DistrictsShareThePopulationByArea()
        {
            var policies = New();
            var a = policies.Districts.Book.Create("A");
            var b = policies.Districts.Book.Create("B");
            policies.Districts.Paint(new float2(40, 40), 40, a);
            policies.Districts.Paint(new float2(200, 200), 20, b);
            var total = policies.PopulationIn(a, 1000) + policies.PopulationIn(b, 1000);
            Assert.That(total, Is.InRange(990, 1000));
            Assert.That(policies.PopulationIn(a, 1000), Is.GreaterThan(policies.PopulationIn(b, 1000)));
        }

        [Test]
        public void WeeklyChargeIsTheMonthlyCostScaledToAWeek()
        {
            var policies = New();
            policies.Book.Activate("policy.city");
            var ledger = new Ledger(10_000);

            var charged = policies.ChargeWeek(ledger, 1, 0);

            Assert.That(charged, Is.EqualTo(300L * Ledger.DaysPerWeek / CityPolicies.DaysPerMonth));
            Assert.That(ledger.Balance, Is.EqualTo(10_000 - charged));
        }

        [Test]
        public void NoPoliciesCostNothing()
        {
            var ledger = new Ledger(500);
            Assert.That(New().ChargeWeek(ledger, 1, 100), Is.EqualTo(0));
            Assert.That(ledger.Balance, Is.EqualTo(500));
        }
    }
}
