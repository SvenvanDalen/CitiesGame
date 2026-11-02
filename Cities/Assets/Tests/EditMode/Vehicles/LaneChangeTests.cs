using System;
using Cities.Simulation.Vehicles;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Vehicles
{
    public class LaneChangeTests
    {
        private static readonly Idm Car = new Idm(50f / 3.6f, 2.5f, 3f);

        private static VehicleAgent Agent(float position, float speed) =>
            new VehicleAgent { Position = position, Speed = speed, Length = 4.5f, Driver = Car };

        // Eigen lane: een langzame voorligger vlak voor ons.
        private static float Stuck(in VehicleAgent car) => Car.Acceleration(car.Speed, 5.5f, 2f);

        [Test]
        public void ACarChangesToAnEmptyLaneWhenItIsStuckBehindASlowLeader()
        {
            var car = Agent(50f, 10f);

            Assert.That(LaneChange.CanChange(car, Stuck(car), Array.Empty<VehicleAgent>()), Is.True);
        }

        [Test]
        public void ACarStaysWhenTheOtherLaneIsNoBetter()
        {
            var car = Agent(50f, 10f);

            Assert.That(LaneChange.CanChange(car, Car.Acceleration(car.Speed), Array.Empty<VehicleAgent>()), Is.False, "geen winst, dus niet wisselen");
        }

        [Test]
        public void ACarDoesNotCutInFrontOfAFastFollower()
        {
            var car = Agent(50f, 10f);

            Assert.That(LaneChange.CanChange(car, Stuck(car), new[] { Agent(40f, 14f) }), Is.False, "de achterligger moet te hard remmen");
        }

        [Test]
        public void ACarDoesNotChangeIntoAnOccupiedSpot()
        {
            var car = Agent(50f, 10f);

            Assert.That(LaneChange.CanChange(car, Stuck(car), new[] { Agent(52f, 10f) }), Is.False, "voorligger overlapt");
            Assert.That(LaneChange.CanChange(car, Stuck(car), new[] { Agent(48f, 10f) }), Is.False, "achterligger overlapt");
        }

        [Test]
        public void ACarChangesBetweenTwoFarApartVehiclesInTheOtherLane()
        {
            var car = Agent(100f, 10f);
            var lane = new[] { Agent(180f, 14f), Agent(20f, 10f) };

            Assert.That(LaneChange.CanChange(car, Stuck(car), lane), Is.True);
        }

        [Test]
        public void ANullLaneIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => LaneChange.CanChange(Agent(0f, 0f), 0f, null));
        }
    }
}
