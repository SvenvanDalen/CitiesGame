using System;
using Cities.Core.Persistence;
using Cities.Simulation.Bikes;
using Cities.Simulation.Economy;
using Cities.Simulation.Network;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Bikes
{
    public class BikeTests
    {
        [Test]
        public void ProfilesAreClassifiedByTheirBikeSpace()
        {
            Assert.That(BikeFacilities.Classify(LaneProfiles.Road(1, bikeLanes: false, sidewalks: true)), Is.EqualTo(BikeFacility.None));
            Assert.That(BikeFacilities.Classify(LaneProfiles.Road(2, bikeLanes: true, sidewalks: true)), Is.EqualTo(BikeFacility.Lane));
            Assert.That(BikeFacilities.Classify(LaneProfiles.Road(0, bikeLanes: true, sidewalks: false)), Is.EqualTo(BikeFacility.Path));
            Assert.Throws<ArgumentNullException>(() => BikeFacilities.Classify(null));
        }

        [Test]
        public void ASeparatedPathIsFasterAndSaferThanSharingTheRoad()
        {
            Assert.That(BikeFacilities.Speed(BikeFacility.Path), Is.GreaterThan(BikeFacilities.Speed(BikeFacility.Lane)));
            Assert.That(BikeFacilities.Speed(BikeFacility.Lane), Is.GreaterThan(BikeFacilities.Speed(BikeFacility.None)));
            Assert.That(BikeFacilities.Risk(BikeFacility.Path), Is.LessThan(BikeFacilities.Risk(BikeFacility.Lane)));
            Assert.That(BikeFacilities.Risk(BikeFacility.Lane), Is.LessThan(BikeFacilities.Risk(BikeFacility.None)));
            Assert.That(BikeFacilities.TravelSeconds(BikeFacility.Lane, 450f), Is.EqualTo(100f).Within(1e-3f));
            Assert.That(BikeFacilities.TravelSeconds(BikeFacility.Lane, -5f), Is.Zero);
        }

        [Test]
        public void BuildingAndUpkeepCostsScaleWithLength()
        {
            Assert.That(BikeFacilities.BuildCost(BikeFacility.Lane, 100f), Is.EqualTo(100 * BikeFacilities.LaneBuildCostPerMeter));
            Assert.That(BikeFacilities.BuildCost(BikeFacility.Path, 10.5f), Is.EqualTo(315));
            Assert.That(BikeFacilities.BuildCost(BikeFacility.None, 100f), Is.Zero);
            Assert.That(BikeFacilities.WeeklyUpkeep(BikeFacility.Path, 100f), Is.EqualTo(200));
            Assert.That(BikeFacilities.WeeklyUpkeep(BikeFacility.Lane, 100f), Is.EqualTo(100));
            Assert.That(BikeFacilities.WeeklyUpkeep(BikeFacility.None, 100f), Is.Zero);
        }

        [Test]
        public void NetworkStatsGiveCoverageRiskAndUpkeep()
        {
            var stats = new BikeNetworkStats();
            Assert.That(stats.Coverage, Is.Zero);
            Assert.That(stats.AverageRisk, Is.EqualTo(BikeFacilities.MixedRisk));

            stats.Add(BikeFacility.None, 500f);
            stats.Add(BikeFacility.Lane, 300f);
            stats.Add(BikeFacility.Path, 200f);

            Assert.That(stats.TotalLength, Is.EqualTo(1000f));
            Assert.That(stats.LengthOf(BikeFacility.Lane), Is.EqualTo(300f));
            Assert.That(stats.Coverage, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(stats.AverageRisk, Is.EqualTo(((500f * 3f) + 300f + (200f * 0.3f)) / 1000f).Within(1e-4f));
            Assert.That(stats.WeeklyUpkeep, Is.EqualTo(300 + 400));
            Assert.Throws<ArgumentOutOfRangeException>(() => stats.Add(BikeFacility.Lane, -1f));
        }

        [Test]
        public void ARentalNeedsABikeAndAReturnNeedsADock()
        {
            var share = new BikeShare();
            var a = share.AddStation(new float3(0f, 0f, 0f), 2, 1);
            var b = share.AddStation(new float3(100f, 0f, 0f), 1, 1);

            Assert.That(share.Rent(a), Is.True);
            Assert.That(share.Rent(a), Is.False, "leeg");
            Assert.That(share.InUse, Is.EqualTo(1));
            Assert.That(share.TryReturn(b, 100f, out var fare), Is.False, "vol");
            Assert.That(fare, Is.Zero);
            Assert.That(share.TryReturn(a, 100f, out fare), Is.True);
            Assert.That(fare, Is.EqualTo(BikeShare.UnlockFee + BikeShare.FarePerBlock));
            Assert.That(share.TryReturn(a, 100f, out _), Is.False, "er is niets verhuurd");
            Assert.That(share.TotalRentals, Is.EqualTo(1));
            Assert.That(share.TotalBikes, Is.EqualTo(2));
        }

        [Test]
        public void FareGrowsPerStartedBlock()
        {
            Assert.That(BikeShare.Fare(0f), Is.EqualTo(BikeShare.UnlockFee));
            Assert.That(BikeShare.Fare(1f), Is.EqualTo(BikeShare.UnlockFee + 1));
            Assert.That(BikeShare.Fare(BikeShare.BlockSeconds), Is.EqualTo(BikeShare.UnlockFee + 1));
            Assert.That(BikeShare.Fare(BikeShare.BlockSeconds + 1f), Is.EqualTo(BikeShare.UnlockFee + 2));
            Assert.That(BikeShare.Fare(-3f), Is.EqualTo(BikeShare.UnlockFee));
        }

        [Test]
        public void StationsAreValidatedAddedAndRemoved()
        {
            var share = new BikeShare();

            Assert.Throws<ArgumentOutOfRangeException>(() => share.AddStation(float3.zero, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => share.AddStation(float3.zero, BikeShare.MaxCapacity + 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => share.AddStation(float3.zero, 5, 6));
            Assert.Throws<ArgumentOutOfRangeException>(() => share.AddStation(float3.zero, 5, -1));
            var id = share.AddStation(float3.zero, 5, 3);
            Assert.That(share.Get(id).FreeDocks, Is.EqualTo(2));
            Assert.Throws<ArgumentException>(() => share.Get(99));
            Assert.That(share.RemoveStation(99), Is.False);
            Assert.That(share.RemoveStation(id), Is.True);
            Assert.That(share.Stations, Is.Empty);
        }

        [Test]
        public void NearestStationRespectsWalkingDistanceAndAvailability()
        {
            var share = new BikeShare();
            var empty = share.AddStation(new float3(10f, 0f, 0f), 4, 0);
            share.AddStation(new float3(300f, 0f, 0f), 4, 4);
            var near = share.AddStation(new float3(50f, 0f, 0f), 4, 2);

            Assert.That(share.FindNearest(float3.zero, needDock: false), Is.EqualTo(near));
            Assert.That(share.FindNearest(float3.zero, needDock: true), Is.EqualTo(empty));
            Assert.That(share.FindNearest(new float3(0f, 0f, 380f), needDock: false), Is.EqualTo(-1));
            Assert.That(share.FindNearest(float3.zero, needDock: true, maxDistance: 5f), Is.EqualTo(-1));
        }

        [Test]
        public void ATripNeedsAStationWithABikeAndOneWithADock()
        {
            var share = new BikeShare();
            var from = share.AddStation(new float3(0f, 0f, 0f), 4, 3);
            var to = share.AddStation(new float3(1000f, 0f, 0f), 4, 0);

            var trip = share.Plan(new float3(0f, 0f, 30f), new float3(1000f, 0f, -40f));

            Assert.That(trip.Found, Is.True);
            Assert.That(trip.FromStation, Is.EqualTo(from));
            Assert.That(trip.ToStation, Is.EqualTo(to));
            Assert.That(trip.WalkIn, Is.EqualTo(30f).Within(1e-3f));
            Assert.That(trip.Ride, Is.EqualTo(1000f).Within(1e-3f));
            Assert.That(trip.WalkOut, Is.EqualTo(40f).Within(1e-3f));
            Assert.That(trip.Seconds, Is.EqualTo((70f / PathNetwork.WalkSpeed) + (1000f / PathNetwork.BikeSpeed)).Within(1e-2f));
            Assert.That(share.Plan(new float3(0f, 0f, 900f), new float3(1000f, 0f, 0f)).Found, Is.False, "geen punt bij het begin");
            Assert.That(share.Plan(new float3(0f, 0f, 0f), new float3(10f, 0f, 0f)).Found, Is.False, "begin en eind zijn hetzelfde punt");
        }

        [Test]
        public void RebalancingEvensOutStationsAndCostsPerBike()
        {
            var share = new BikeShare();
            var full = share.AddStation(float3.zero, 10, 10);
            var empty = share.AddStation(new float3(500f, 0f, 0f), 10, 0);

            var moved = share.Rebalance();

            Assert.That(moved, Is.EqualTo(5));
            Assert.That(share.Get(full).Bikes, Is.EqualTo(5));
            Assert.That(share.Get(empty).Bikes, Is.EqualTo(5));
            Assert.That(share.PendingRebalanceCost, Is.EqualTo(5 * BikeShare.RebalanceCostPerBike));
            Assert.That(share.Rebalance(), Is.Zero, "al in balans");
            Assert.That(new BikeShare().Rebalance(), Is.Zero, "geen punten");
        }

        [Test]
        public void SettlingBooksRevenueAndCosts()
        {
            var share = new BikeShare();
            var a = share.AddStation(float3.zero, 4, 2);
            share.AddStation(new float3(100f, 0f, 0f), 4, 0);
            share.Rent(a);
            share.TryReturn(a, 1200f, out var fare);
            share.Rebalance();
            var ledger = new Ledger(1000);
            var cost = (2 * BikeShare.StationUpkeepPerWeek) + (2 * BikeShare.BikeUpkeepPerWeek) + share.PendingRebalanceCost;

            var result = share.Settle(0, ledger);

            Assert.That(result, Is.EqualTo(fare - cost));
            Assert.That(ledger.Income(0, LedgerCategory.Trade), Is.EqualTo(fare));
            Assert.That(ledger.Expenses(0, LedgerCategory.Upkeep), Is.EqualTo(cost));
            Assert.That(share.PendingRevenue, Is.Zero);
            Assert.That(share.PendingRebalanceCost, Is.Zero);
            Assert.Throws<ArgumentNullException>(() => share.Settle(0, null));
        }

        [Test]
        public void TheStateSurvivesASaveRoundTrip()
        {
            var share = new BikeShare();
            var a = share.AddStation(new float3(1f, 2f, 3f), 8, 6);
            share.AddStation(new float3(100f, 0f, 0f), 8, 1);
            share.Rent(a);
            share.Rent(a);
            share.TryReturn(a, 700f, out _);
            share.Rebalance();
            var bytes = SaveBytes.Write(new SaveMetadata(), new BikeShareSection(share));

            var loaded = new BikeShare();
            SaveBytes.Read(bytes, new BikeShareSection(loaded));

            Assert.That(loaded.Stations, Has.Count.EqualTo(2));
            Assert.That(loaded.Get(a).Position, Is.EqualTo(new float3(1f, 2f, 3f)));
            Assert.That(loaded.InUse, Is.EqualTo(share.InUse));
            Assert.That(loaded.PendingRevenue, Is.EqualTo(share.PendingRevenue));
            Assert.That(loaded.TotalRentals, Is.EqualTo(2));
            Assert.That(loaded.NextId, Is.EqualTo(share.NextId));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new BikeShareSection(loaded)), Is.EqualTo(bytes), "deterministisch opgeslagen");
            Assert.That(new BikeShareSection(loaded).Id, Is.EqualTo("BIKE"));
        }

        [Test]
        public void ACorruptSaveIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new BikeShareSection(null));
            var section = new BikeShareSection(new BikeShare());

            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, -1, 4, 1)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 1, 4, 9)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 1, 0, 0)), 1));
        }

        private static void Corrupt(SaveWriter w, int count, int capacity, int bikes)
        {
            w.Write(5);
            w.Write(0);
            w.Write(0L);
            w.Write(0L);
            w.Write(0L);
            w.Write(count);
            if (count > 0)
            {
                w.Write(1);
                w.Write(0f);
                w.Write(0f);
                w.Write(0f);
                w.Write(capacity);
                w.Write(bikes);
            }
        }
    }
}
