using System;
using Cities.Simulation.Demand;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Demands
{
    public class DemandModelTests
    {
        [Test]
        public void AnEmptyCityOnlyAsksForHomes()
        {
            var demand = DemandModel.Compute(new DemandInputs(0, 0, 0, 0, 0f, 0f));

            Assert.That(demand.Residential, Is.GreaterThan(0.5f));
            Assert.That(demand.Work, Is.Zero);
            Assert.That(demand.Commerce, Is.Zero);
        }

        [Test]
        public void UnemployedResidentsAskForWorkAndOpenJobsAskForResidents()
        {
            var jobless = DemandModel.Compute(new DemandInputs(100, 5, 40, 0, 0f, 0f));
            var vacancies = DemandModel.Compute(new DemandInputs(100, 5, 0, 40, 0f, 0f));

            Assert.That(jobless.Work, Is.EqualTo(1f));
            Assert.That(vacancies.Work, Is.EqualTo(-1f), "te veel banen voor te weinig mensen");
            Assert.That(vacancies.Residential, Is.GreaterThan(jobless.Residential));
        }

        [Test]
        public void EmptyHomesCoolDownTheDemandForHomes()
        {
            var tight = DemandModel.Compute(new DemandInputs(100, 0, 0, 0, 0f, 0f));
            var balanced = DemandModel.Compute(new DemandInputs(100, 5, 0, 0, 0f, 0f));
            var glut = DemandModel.Compute(new DemandInputs(100, 40, 0, 0, 0f, 0f));

            Assert.That(tight.Residential, Is.GreaterThan(balanced.Residential));
            Assert.That(balanced.Residential, Is.EqualTo(0f).Within(0.01f), "vijf procent leegstand is in evenwicht");
            Assert.That(glut.Residential, Is.LessThan(0f));
        }

        [Test]
        public void ShopsFollowSpendingPower()
        {
            Assert.That(DemandModel.Compute(new DemandInputs(10, 0, 0, 0, 1000f, 250f)).Commerce, Is.EqualTo(0.75f).Within(1e-4f));
            Assert.That(DemandModel.Compute(new DemandInputs(10, 0, 0, 0, 100f, 400f)).Commerce, Is.EqualTo(-0.75f).Within(1e-4f));
        }

        [Test]
        public void InvalidCountsAreRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DemandModel.Compute(new DemandInputs(10, 0, -1, 0, 0f, 0f)));
            Assert.Throws<ArgumentException>(() => DemandModel.Compute(new DemandInputs(10, 11, 0, 0, 0f, 0f)));
        }

        [Test]
        public void TheMetersApproachTheirTargetWithoutOvershooting()
        {
            var target = new Demand(1f, -1f, 0f);
            var next = DemandModel.Approach(new Demand(0f, 0f, 0.05f), target, 0.5f, 0.2f);

            Assert.That(next.Residential, Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(next.Work, Is.EqualTo(-0.1f).Within(1e-5f));
            Assert.That(next.Commerce, Is.EqualTo(0f), "binnen één stap: precies op het doel");
        }
    }
}
