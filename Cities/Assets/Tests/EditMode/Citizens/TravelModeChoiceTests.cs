using Cities.Core.Randomness;
using Cities.Simulation.Citizens;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Citizens
{
    public sealed class TravelModeChoiceTests
    {
        private static TravelMode Pick(TripOptions trip, ulong seed = 1)
        {
            var random = new DeterministicRandom(seed);
            return TravelModeChoice.Choose(trip, ref random);
        }

        [Test]
        public void ShortTripWalks()
        {
            Assert.AreEqual(TravelMode.Walk, Pick(new TripOptions(300, true, true, 200, 2, 3)));
        }

        [Test]
        public void MediumTripWithBikeCycles()
        {
            Assert.AreEqual(TravelMode.Bike, Pick(new TripOptions(5000, true, false, -1, 0, 0)));
        }

        [Test]
        public void CarOwnerDrivesLongTripWhenCheap()
        {
            Assert.AreEqual(TravelMode.Car, Pick(new TripOptions(20000, false, true, 4000, 5, 2)));
        }

        [Test]
        public void ExpensiveCarLosesToTransit()
        {
            Assert.AreEqual(TravelMode.Transit, Pick(new TripOptions(20000, false, true, 2400, 2, 50)));
        }

        [Test]
        public void NothingAvailableFallsBackToWalking()
        {
            Assert.AreEqual(TravelMode.Walk, Pick(new TripOptions(20000, false, false, -1, 0, 0)));
        }

        [Test]
        public void CostIsNegativeWhenModeUnavailable()
        {
            var trip = new TripOptions(20000, false, false, -1, 0, 0);
            Assert.Less(TravelModeChoice.Cost(trip, TravelMode.Walk), 0f);
            Assert.Less(TravelModeChoice.Cost(trip, TravelMode.Bike), 0f);
            Assert.Less(TravelModeChoice.Cost(trip, TravelMode.Car), 0f);
            Assert.Less(TravelModeChoice.Cost(trip, TravelMode.Transit), 0f);
        }

        [Test]
        public void ChoiceIsDeterministic()
        {
            var trip = new TripOptions(3000, true, true, 1500, 2, 4);
            Assert.AreEqual(Pick(trip, 7), Pick(trip, 7));
        }

        [Test]
        public void OwnershipIsStableAndGrowsWithWealth()
        {
            Assert.AreEqual(TravelOwnership.OwnsCar(5, 0.5f, 3), TravelOwnership.OwnsCar(5, 0.5f, 3));
            int poor = 0, rich = 0, bikes = 0;
            for (uint h = 1; h <= 1000; h++)
            {
                poor += TravelOwnership.OwnsCar(h, 0f, 9) ? 1 : 0;
                rich += TravelOwnership.OwnsCar(h, 1f, 9) ? 1 : 0;
                bikes += TravelOwnership.OwnsBike(h, 9) ? 1 : 0;
            }

            Assert.Greater(rich, poor);
            Assert.That(bikes, Is.InRange(600, 800));
        }
    }
}
