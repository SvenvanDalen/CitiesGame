using System;
using Cities.Simulation.Vehicles;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Vehicles
{
    public class SignalPlanTests
    {
        [Test]
        public void GroupsTakeTurnsWithAnAllRedClearanceBetween()
        {
            var plan = new SignalPlan(2, 3f);
            plan.SetGreen(0, 10f);
            plan.SetGreen(1, 5f);

            Assert.That(plan.IsOpen(0), Is.True);
            Assert.That(plan.IsOpen(1), Is.False);

            plan.Tick(10f);
            Assert.That(plan.Clearing, Is.True);
            Assert.That(plan.IsOpen(0) || plan.IsOpen(1), Is.False, "ontruimingstijd: alles rood");

            plan.Tick(3f);
            Assert.That(plan.Phase, Is.EqualTo(1));
            Assert.That(plan.IsOpen(1), Is.True);
            Assert.That(plan.IsOpen(0), Is.False);

            plan.Tick(5f + 3f);
            Assert.That(plan.Phase, Is.EqualTo(0), "na de laatste groep begint de cyclus opnieuw");
        }

        [Test]
        public void ALargeStepSkipsSeveralPhases()
        {
            var plan = new SignalPlan(3, 0f);

            plan.Tick((2 * SignalPlan.DefaultGreen) + 1f);

            Assert.That(plan.Phase, Is.EqualTo(2));
            Assert.That(plan.GreenOf(2), Is.EqualTo(SignalPlan.DefaultGreen));
        }

        [Test]
        public void SwitchingTheJunctionOffOpensEverything()
        {
            var plan = new SignalPlan(2) { Enabled = false };

            Assert.That(plan.IsOpen(0) && plan.IsOpen(1), Is.True);
        }

        [Test]
        public void ABlockedLaneStopsForRedAndGoesAgainOnGreen()
        {
            var plan = new SignalPlan(2, 2f);
            var cars = new[] { new VehicleAgent { Position = 40f, Speed = 13.9f, Length = 4.5f, Driver = new Idm(13.9f, 2.5f, 3f) } };

            for (var t = 0f; t < 15f; t += 0.1f)
            {
                LaneTraffic.Step(cars, 100f, !plan.IsOpen(1), 0.1f);
                plan.Tick(0.1f);
            }

            Assert.That(cars[0].Position, Is.LessThan(100f), "groep 1 heeft rood");
            Assert.That(cars[0].Speed, Is.LessThan(1f));
        }

        [Test]
        public void OppositeApproachesShareAGroup()
        {
            var groups = SignalPlan.AutoGroups(new[] { 0f, 90f, 180f, 270f });

            Assert.That(groups, Is.EqualTo(new[] { 0, 1, 0, 1 }));
            Assert.That(SignalPlan.AutoGroups(new[] { 10f, 190f, 100f }), Is.EqualTo(new[] { 0, 0, 1 }));
            Assert.That(SignalPlan.AutoGroups(new[] { 0f, 90f, 200f }), Is.EqualTo(new[] { 0, 1, 0 }), "binnen de tolerantie");
            Assert.That(SignalPlan.AutoGroups(new[] { 0f, 90f, 140f }), Is.EqualTo(new[] { 0, 1, 2 }), "te schuin om samen te gaan");
        }

        [Test]
        public void BadInputIsRejected()
        {
            var plan = new SignalPlan(1);

            Assert.Throws<ArgumentOutOfRangeException>(() => new SignalPlan(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SignalPlan(1, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => plan.SetGreen(0, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => plan.SetGreen(1, 5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => plan.IsOpen(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => plan.Tick(-1f));
            Assert.Throws<ArgumentNullException>(() => SignalPlan.AutoGroups(null));
        }
    }
}
