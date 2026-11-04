using System;
using Cities.Core.Ids;
using Cities.Core.Persistence;
using Cities.Simulation.Network;
using Cities.Simulation.Vehicles;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Network
{
    public class TrafficManagementTests
    {
        private static readonly EntityId<SegmentKind> A = new EntityId<SegmentKind>(1, 1);
        private static readonly EntityId<SegmentKind> B = new EntityId<SegmentKind>(2, 1);

        [Test]
        public void DefaultsAllowEverythingForFree()
        {
            var m = new TrafficManagement();

            Assert.That(m.Count, Is.EqualTo(0));
            Assert.That(m.Allows(A, VehicleCategory.Truck), Is.True);
            Assert.That(m.TollFor(A, VehicleCategory.PassengerCar), Is.EqualTo(0));
            Assert.That(m.EffectiveSpeedLimit(A, 10f, VehicleCategory.PassengerCar), Is.EqualTo(10f));
        }

        [Test]
        public void TollIsChargedExceptForPriorityVehicles()
        {
            var m = new TrafficManagement();
            m.SetToll(A, 250);

            Assert.That(m.TollFor(A, VehicleCategory.PassengerCar), Is.EqualTo(250));
            Assert.That(m.TollFor(A, VehicleCategory.Bus), Is.EqualTo(0));
            Assert.That(m.TollFor(A, VehicleCategory.Service), Is.EqualTo(0));
            Assert.That(m.TollFor(B, VehicleCategory.PassengerCar), Is.EqualTo(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => m.SetToll(A, -1));
        }

        [Test]
        public void TruckBanBlocksTrucksAndVans()
        {
            var m = new TrafficManagement();
            m.SetTruckBan(A, true);

            Assert.That(m.Allows(A, VehicleCategory.Truck), Is.False);
            Assert.That(m.Allows(A, VehicleCategory.Van), Is.False);
            Assert.That(m.Allows(A, VehicleCategory.PassengerCar), Is.True);
            Assert.That(m.Allows(B, VehicleCategory.Truck), Is.True);
        }

        [Test]
        public void PriorityLaneAdmitsOnlyTransitAndEmergency()
        {
            var m = new TrafficManagement();
            m.SetPriorityLane(A, true);

            Assert.That(m.Allows(A, VehicleCategory.Bus), Is.True);
            Assert.That(m.Allows(A, VehicleCategory.Tram), Is.True);
            Assert.That(m.Allows(A, VehicleCategory.Service), Is.True);
            Assert.That(m.Allows(A, VehicleCategory.PassengerCar), Is.False);
            Assert.That(m.Allows(A, VehicleCategory.Bicycle), Is.False);
        }

        [Test]
        public void SpeedLimitOverridesRoadAndEmergencyMayExceedIt()
        {
            var m = new TrafficManagement();
            m.SetSpeedLimit(A, 8f);

            Assert.That(m.EffectiveSpeedLimit(A, 20f, VehicleCategory.PassengerCar), Is.EqualTo(8f));
            Assert.That(m.EffectiveSpeedLimit(A, 20f, VehicleCategory.Service), Is.EqualTo(8f * TrafficManagement.EmergencySpeedFactor));
            m.SetSpeedLimit(A, 0f);
            Assert.That(m.EffectiveSpeedLimit(A, 20f, VehicleCategory.PassengerCar), Is.EqualTo(20f));
            Assert.Throws<ArgumentOutOfRangeException>(() => m.SetSpeedLimit(A, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => m.SetSpeedLimit(A, float.NaN));
        }

        [Test]
        public void ClearingEverythingRemovesTheEntryAndPolicyEquals()
        {
            var m = new TrafficManagement();
            m.SetTruckBan(A, true);
            m.SetToll(A, 5);
            Assert.That(m.Count, Is.EqualTo(1));
            Assert.That(m.Get(A), Is.EqualTo(new SegmentPolicy(5, true, false, 0f)));
            Assert.That(m.Get(A).GetHashCode(), Is.EqualTo(new SegmentPolicy(5, true, false, 0f).GetHashCode()));
            Assert.That(m.Get(A).Equals((object)"x"), Is.False);

            m.SetTruckBan(A, false);
            m.SetToll(A, 0);

            Assert.That(m.Count, Is.EqualTo(0));
        }

        [Test]
        public void SaveRoundTripIsDeterministicAndPrunesDeadSegments()
        {
            var m = new TrafficManagement();
            m.SetToll(B, 100);
            m.SetTruckBan(A, true);
            m.SetPriorityLane(A, true);
            m.SetSpeedLimit(A, 12f);

            var bytes = SaveBytes.Write(new SaveMetadata(), new TrafficManagementSection(m));
            var loaded = new TrafficManagement();
            SaveBytes.Read(bytes, new TrafficManagementSection(loaded));

            Assert.That(loaded.Get(A), Is.EqualTo(m.Get(A)));
            Assert.That(loaded.Get(B), Is.EqualTo(m.Get(B)));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new TrafficManagementSection(loaded)), Is.EqualTo(bytes));

            var pruned = new TrafficManagementSection(loaded, s => s == A);
            SaveBytes.Write(new SaveMetadata(), pruned);
            Assert.That(loaded.Count, Is.EqualTo(1));
            Assert.Throws<ArgumentNullException>(() => new TrafficManagementSection(null));
            Assert.That(pruned.Id, Is.EqualTo("TMGT"));
        }
    }
}
