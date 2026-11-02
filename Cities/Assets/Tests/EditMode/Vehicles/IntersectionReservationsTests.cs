using System;
using Cities.Simulation.Vehicles;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Vehicles
{
    public class IntersectionReservationsTests
    {
        [Test]
        public void AVehicleThatCannotGetAllPartsHoldsNone()
        {
            var reservations = new IntersectionReservations();
            Assert.That(reservations.TryReserve(1, new[] { 1, 2 }), Is.True);

            Assert.That(reservations.TryReserve(2, new[] { 3, 2 }), Is.False);

            Assert.That(reservations.IsReserved(3), Is.False, "deel 3 blijft vrij voor anderen");
            Assert.That(reservations.Count, Is.EqualTo(2));
        }

        [Test]
        public void TheWaitingVehicleGetsItsTurnAfterTheOtherLeaves()
        {
            var reservations = new IntersectionReservations();
            reservations.TryReserve(1, new[] { 1, 2 });

            reservations.Release(1);

            Assert.That(reservations.TryReserve(2, new[] { 2, 3 }), Is.True);
            Assert.That(reservations.Count, Is.EqualTo(2));
        }

        [Test]
        public void VehiclesWantingEachOthersPartsNeverDeadlock()
        {
            var reservations = new IntersectionReservations();

            var first = reservations.TryReserve(1, new[] { 1, 2 });
            var second = reservations.TryReserve(2, new[] { 2, 1 });

            Assert.That(first && !second, Is.True, "precies één van de twee rijdt door");
            reservations.Release(1);
            Assert.That(reservations.TryReserve(2, new[] { 2, 1 }), Is.True);
        }

        [Test]
        public void AVehicleCanExtendItsOwnReservation()
        {
            var reservations = new IntersectionReservations();
            reservations.TryReserve(1, new[] { 1 });

            Assert.That(reservations.TryReserve(1, new[] { 1, 2 }), Is.True);
            Assert.That(reservations.Count, Is.EqualTo(2));
        }

        [Test]
        public void AClosedEndFromAFailedReservationMakesTheCarWait()
        {
            var reservations = new IntersectionReservations();
            reservations.TryReserve(1, new[] { 5 });
            var cars = new[] { new VehicleAgent { Position = 0f, Speed = 10f, Length = 4.5f, Driver = new Idm(14f, 2.5f, 3f) } };

            for (var i = 0; i < 600; i++)
            {
                LaneTraffic.Step(cars, 100f, !reservations.TryReserve(2, new[] { 5 }), 0.1f);
            }

            Assert.That(cars[0].Speed, Is.LessThan(0.05f));
            Assert.That(cars[0].Position, Is.LessThan(100f));
        }

        [Test]
        public void ANullPartListIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new IntersectionReservations().TryReserve(1, null));
        }
    }
}
