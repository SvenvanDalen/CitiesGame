using System;
using Cities.Simulation.Network;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Network
{
    public class RoadConditionTests
    {
        [Test]
        public void ANewRoadIsInPerfectConditionAndFullSpeed()
        {
            var road = new RoadCondition();

            Assert.That(road.Value, Is.EqualTo(1f));
            Assert.That(road.NeedsMaintenance, Is.False);
            Assert.That(road.SpeedFactor, Is.EqualTo(1f));
        }

        [Test]
        public void TrafficWearsTheRoadAndHeavyVehiclesWearItMore()
        {
            var cars = new RoadCondition();
            var trucks = new RoadCondition();

            cars.Wear(1000f, 0f);
            trucks.Wear(0f, 1000f);

            Assert.That(cars.Value, Is.EqualTo(1f - (1000f * RoadCondition.WearPerVehicle)).Within(1e-5f));
            Assert.That(1f - trucks.Value, Is.EqualTo((1f - cars.Value) * RoadCondition.HeavyWeight).Within(1e-4f));
        }

        [Test]
        public void ConditionNeverDropsBelowZeroAndAWornRoadIsSlowAndNeedsMaintenance()
        {
            var road = new RoadCondition();

            road.Wear(1_000_000f, 0f);

            Assert.That(road.Value, Is.Zero);
            Assert.That(road.NeedsMaintenance, Is.True);
            Assert.That(road.SpeedFactor, Is.EqualTo(RoadCondition.MinSpeedFactor).Within(1e-5f));
        }

        [Test]
        public void RepairRestoresTheConditionAtACostThatGrowsWithTheDamage()
        {
            var worn = new RoadCondition();
            worn.Wear(1_000_000f, 0f);
            var slightlyWorn = new RoadCondition();
            slightlyWorn.Wear(40_000f, 0f);

            var expensive = worn.Repair(300f, 30L);
            var cheap = slightlyWorn.Repair(300f, 30L);

            Assert.That(worn.Value, Is.EqualTo(RoadCondition.RepairPerDay).Within(1e-5f));
            Assert.That(expensive, Is.GreaterThan(cheap));
            Assert.That(expensive, Is.EqualTo(3L * 300L), "kapot: 300 m maal 30 per maand gedeeld door 30, driemaal");
        }

        [Test]
        public void ARoadInPerfectConditionNeedsNoRepairAndCostsNothing()
        {
            var road = new RoadCondition();

            Assert.That(road.Repair(500f, 100L), Is.Zero);
            Assert.That(road.Value, Is.EqualTo(1f));
        }

        [Test]
        public void RepairDoesNotGoAboveFull()
        {
            var road = new RoadCondition();
            road.Wear(2500f, 0f);

            road.Repair(100f, 10L);

            Assert.That(road.Value, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void NegativeInputIsRejected()
        {
            var road = new RoadCondition();

            Assert.Throws<ArgumentOutOfRangeException>(() => road.Wear(-1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => road.Wear(0f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => road.Repair(-1f, 10L));
            Assert.Throws<ArgumentOutOfRangeException>(() => road.Repair(1f, -1L));
        }
    }
}
