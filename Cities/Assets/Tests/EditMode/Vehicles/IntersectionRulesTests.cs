using System;
using Cities.Simulation.Vehicles;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Vehicles
{
    public class IntersectionRulesTests
    {
        private static readonly int[] Crossing = { 1, 2 };

        private static Approach Car(int id, RightOfWay rule, float bearing = 180f, bool stopped = false, float wait = 0f, bool circulating = false) =>
            new Approach(id, rule, Crossing, bearing, stopped, wait, circulating);

        [Test]
        public void PriorityTrafficAlwaysMayTry()
        {
            Assert.That(IntersectionRules.MayProceed(Car(1, RightOfWay.Priority), new[] { Car(2, RightOfWay.Yield) }), Is.True);
        }

        [Test]
        public void YieldingWaitsForPriorityTrafficOnTheSameParts()
        {
            var me = Car(1, RightOfWay.Yield);

            Assert.That(IntersectionRules.MayProceed(me, new[] { Car(2, RightOfWay.Priority) }), Is.False);
            Assert.That(IntersectionRules.MayProceed(me, new[] { Car(3, RightOfWay.Yield) }), Is.True, "een andere wachtende telt niet");
            Assert.That(IntersectionRules.MayProceed(me, new[] { new Approach(4, RightOfWay.Priority, new[] { 9 }, 0f, false, 0f) }), Is.True, "geen gedeeld kruispuntdeel");
            Assert.That(IntersectionRules.MayProceed(me, new[] { me }), Is.True, "het voertuig zelf telt niet");
        }

        [Test]
        public void StoppingNeedsAFullStopAndThenFirstComeFirstServed()
        {
            Assert.That(IntersectionRules.MayProceed(Car(1, RightOfWay.Stop, stopped: false), Array.Empty<Approach>()), Is.False, "rijdt nog");
            var me = Car(1, RightOfWay.Stop, stopped: true, wait: 2f);

            Assert.That(IntersectionRules.MayProceed(me, new[] { Car(2, RightOfWay.Stop, stopped: true, wait: 3f) }), Is.False, "ander stond eerder");
            Assert.That(IntersectionRules.MayProceed(me, new[] { Car(2, RightOfWay.Stop, stopped: true, wait: 1f) }), Is.True);
            Assert.That(IntersectionRules.MayProceed(me, new[] { Car(2, RightOfWay.Stop, stopped: false, wait: 9f) }), Is.True, "nog niet gestopt");
            Assert.That(IntersectionRules.MayProceed(me, new[] { Car(2, RightOfWay.Priority) }), Is.False);
        }

        [Test]
        public void ATieOnArrivalGoesToTheLowerVehicleId()
        {
            var low = Car(1, RightOfWay.Stop, stopped: true, wait: 2f);
            var high = Car(2, RightOfWay.Stop, stopped: true, wait: 2f);

            Assert.That(IntersectionRules.MayProceed(low, new[] { high }), Is.True);
            Assert.That(IntersectionRules.MayProceed(high, new[] { low }), Is.False);
        }

        [Test]
        public void RightBeforeLeftYieldsToTheRightHandSideOnly()
        {
            var fromSouth = Car(1, RightOfWay.RightBeforeLeft, bearing: 180f);

            Assert.That(IntersectionRules.MayProceed(fromSouth, new[] { Car(2, RightOfWay.RightBeforeLeft, bearing: 90f) }), Is.False, "oost is rechts");
            Assert.That(IntersectionRules.MayProceed(fromSouth, new[] { Car(2, RightOfWay.RightBeforeLeft, bearing: 270f) }), Is.True, "west is links");
            Assert.That(IntersectionRules.MayProceed(fromSouth, new[] { Car(2, RightOfWay.RightBeforeLeft, bearing: 0f) }), Is.True, "noord is tegenover");
            Assert.That(IntersectionRules.MayProceed(fromSouth, new[] { Car(2, RightOfWay.Priority, bearing: 270f) }), Is.False);
        }

        [Test]
        public void FourWayRightBeforeLeftResolvesAfterThePatienceAndOnlyOneGetsTheReservation()
        {
            var cars = new[]
            {
                Car(1, RightOfWay.RightBeforeLeft, 0f, wait: 6f),
                Car(2, RightOfWay.RightBeforeLeft, 90f, wait: 6f),
                Car(3, RightOfWay.RightBeforeLeft, 180f, wait: 6f),
                Car(4, RightOfWay.RightBeforeLeft, 270f, wait: 6f),
            };
            var reservations = new IntersectionReservations();

            var entered = 0;
            foreach (var car in cars)
            {
                entered += IntersectionRules.TryEnter(car, cars, reservations) ? 1 : 0;
            }

            Assert.That(entered, Is.EqualTo(1));
            Assert.That(IntersectionRules.MayProceed(Car(1, RightOfWay.RightBeforeLeft, 0f, wait: 1f), cars), Is.False, "zonder geduld blokkeren ze elkaar");
        }

        [Test]
        public void ARoundaboutEntryYieldsToCirculatingTraffic()
        {
            var entering = Car(1, RightOfWay.Roundabout);

            Assert.That(IntersectionRules.MayProceed(entering, new[] { Car(2, RightOfWay.Roundabout, circulating: true) }), Is.False);
            Assert.That(IntersectionRules.MayProceed(entering, new[] { Car(3, RightOfWay.Roundabout) }), Is.True, "een andere inrijder telt niet");
            Assert.That(IntersectionRules.MayProceed(Car(2, RightOfWay.Roundabout, circulating: true), new[] { entering }), Is.True);
        }

        [Test]
        public void TheRulesGoThroughTheReservationLayer()
        {
            var reservations = new IntersectionReservations();
            Assert.That(reservations.TryReserve(9, new[] { 2 }), Is.True);

            Assert.That(IntersectionRules.TryEnter(Car(1, RightOfWay.Priority), Array.Empty<Approach>(), reservations), Is.False, "deel is bezet");
            reservations.Release(9);
            Assert.That(IntersectionRules.TryEnter(Car(1, RightOfWay.Priority), Array.Empty<Approach>(), reservations), Is.True);
            Assert.That(reservations.IsReserved(1), Is.True);
        }

        [Test]
        public void BadInputIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => IntersectionRules.MayProceed(Car(1, RightOfWay.Stop), null));
            Assert.Throws<ArgumentNullException>(() => IntersectionRules.TryEnter(Car(1, RightOfWay.Stop), Array.Empty<Approach>(), null));
            Assert.Throws<ArgumentNullException>(() => new Approach(1, RightOfWay.Stop, null, 0f, false, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => IntersectionRules.MayProceed(Car(1, (RightOfWay)99), Array.Empty<Approach>()));
        }
    }
}
