using System.Collections.Generic;
using Cities.Simulation.Network;
using Cities.Simulation.Vehicles;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Vehicles
{
    public class VehicleTypesTests
    {
        private static VehicleSpec Of(VehicleCategory category, float speed = 50f) => new VehicleSpec
        {
            Category = category, LengthMeters = 2f, WidthMeters = 0.7f, MaxSpeedKmh = speed,
            AccelerationMs2 = 2f, BrakingMs2 = 3f, PassengerCapacity = 1, CargoCapacityTonnes = 1f,
        };

        [TestCase(VehicleCategory.Bicycle, LaneTypes.Bike)]
        [TestCase(VehicleCategory.Motorcycle, LaneTypes.Car)]
        [TestCase(VehicleCategory.PassengerCar, LaneTypes.Car)]
        [TestCase(VehicleCategory.Truck, LaneTypes.Car)]
        [TestCase(VehicleCategory.Bus, LaneTypes.Bus | LaneTypes.Car)]
        public void EachTypeUsesItsOwnLanes(VehicleCategory category, LaneTypes expected) =>
            Assert.That(Of(category).AllowedLanes, Is.EqualTo(expected));

        [Test]
        public void HeadwayAndGapDifferPerType()
        {
            Assert.That(Of(VehicleCategory.Truck).TimeHeadwaySeconds, Is.GreaterThan(Of(VehicleCategory.PassengerCar).TimeHeadwaySeconds));
            Assert.That(Of(VehicleCategory.Motorcycle).TimeHeadwaySeconds, Is.LessThan(Of(VehicleCategory.PassengerCar).TimeHeadwaySeconds));
            Assert.That(Of(VehicleCategory.Bicycle).MinGapMeters, Is.LessThan(Of(VehicleCategory.Motorcycle).MinGapMeters));
            Assert.That(Of(VehicleCategory.Truck).MinGapMeters, Is.GreaterThan(Of(VehicleCategory.PassengerCar).MinGapMeters));
        }

        [Test]
        public void OnlyNarrowVehiclesCanFilter()
        {
            Assert.That(Of(VehicleCategory.Motorcycle).CanFilter, Is.True);
            Assert.That(Of(VehicleCategory.Bicycle).CanFilter, Is.True);
            Assert.That(Of(VehicleCategory.PassengerCar).CanFilter, Is.False);
        }

        [Test]
        public void TheDriverModelFollowsTheTypeBehaviour()
        {
            var truck = Idm.For(Of(VehicleCategory.Truck), 30f);
            var bike = Idm.For(Of(VehicleCategory.Bicycle, 22f), 30f);

            Assert.That(truck.TimeHeadway, Is.EqualTo(2f));
            Assert.That(bike.MinGap, Is.EqualTo(1f));
            Assert.That(truck.DesiredGap(10f, 0f), Is.GreaterThan(bike.DesiredGap(10f, 0f)));
        }

        [Test]
        public void ABicycleCannotBeFast()
        {
            var errors = new List<string>();
            Of(VehicleCategory.Bicycle, 60f).Validate(errors);

            Assert.That(errors, Has.Some.Contains("fiets"));
        }
    }
}
