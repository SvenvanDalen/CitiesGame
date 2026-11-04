using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Simulation.Economy;
using Cities.Simulation.Freight;
using Cities.Simulation.Outside;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Outside
{
    public class OutsideConnectionsTests
    {
        private const float Size = 4000f;

        [Test]
        public void ConnectionsSitOnTheMapEdge()
        {
            var outside = new OutsideConnections(Size);

            var north = outside.Add(OutsideKind.Highway, MapEdge.North, 500f);
            var east = outside.Add(OutsideKind.Rail, MapEdge.East, 700f);
            var south = outside.Add(OutsideKind.Harbour, MapEdge.South, 900f);
            var west = outside.Add(OutsideKind.Airport, MapEdge.West, 1100f);

            Assert.That(north.PositionOn(Size).z, Is.EqualTo(Size));
            Assert.That(north.PositionOn(Size).x, Is.EqualTo(500f));
            Assert.That(east.PositionOn(Size).x, Is.EqualTo(Size));
            Assert.That(east.PositionOn(Size).z, Is.EqualTo(700f));
            Assert.That(south.PositionOn(Size).z, Is.Zero);
            Assert.That(west.PositionOn(Size).x, Is.Zero);
            Assert.That(west.PositionOn(Size).z, Is.EqualTo(1100f));
            Assert.That(outside.Get(east.Id), Is.SameAs(east));
            Assert.Throws<KeyNotFoundException>(() => outside.Get(99));
        }

        [Test]
        public void InvalidPlacementsAreRejected()
        {
            var outside = new OutsideConnections(Size);
            outside.Add(OutsideKind.Highway, MapEdge.North, 500f);

            Assert.Throws<ArgumentOutOfRangeException>(() => new OutsideConnections(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => outside.Add(OutsideKind.Highway, MapEdge.North, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => outside.Add(OutsideKind.Highway, MapEdge.North, Size + 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => outside.Add((OutsideKind)9, MapEdge.North, 1000f));
            Assert.Throws<ArgumentOutOfRangeException>(() => outside.Add(OutsideKind.Highway, (MapEdge)9, 1000f));
            Assert.Throws<InvalidOperationException>(() => outside.Add(OutsideKind.Rail, MapEdge.North, 550f), "te dicht");
            Assert.DoesNotThrow(() => outside.Add(OutsideKind.Rail, MapEdge.East, 550f));
            Assert.Throws<ArgumentOutOfRangeException>(() => OutsideProfile.For((OutsideKind)9));
        }

        [Test]
        public void ThereIsALimitPerKind()
        {
            var outside = new OutsideConnections(Size);
            for (var i = 0; i < OutsideConnections.MaxPerKind; i++)
            {
                outside.Add(OutsideKind.Highway, MapEdge.South, i * 200f);
            }

            Assert.Throws<InvalidOperationException>(() => outside.Add(OutsideKind.Highway, MapEdge.North, 0f));
            Assert.That(outside.Count(OutsideKind.Highway), Is.EqualTo(OutsideConnections.MaxPerKind));
            Assert.That(outside.Remove(1), Is.True);
            Assert.That(outside.Remove(1), Is.False);
            Assert.DoesNotThrow(() => outside.Add(OutsideKind.Highway, MapEdge.North, 0f));
        }

        [Test]
        public void BuildingUpgradingAndUpkeepCostMoney()
        {
            var outside = new OutsideConnections(Size);
            var ledger = new Ledger(2_500_000);

            var road = outside.TryBuild(ledger, 0, OutsideKind.Highway, MapEdge.North, 100f);
            var airport = outside.TryBuild(ledger, 0, OutsideKind.Airport, MapEdge.West, 100f);

            Assert.That(road, Is.Not.Null);
            Assert.That(airport, Is.Null, "geen geld");
            Assert.That(outside.Count(OutsideKind.Airport), Is.Zero);
            Assert.That(ledger.Balance, Is.EqualTo(500_000));
            Assert.That(outside.TryUpgrade(ledger, 0, road.Id), Is.False, "upgrade kost 2 x bouwprijs");
            ledger.Earn(0, LedgerCategory.Other, 10_000_000);
            Assert.That(outside.TryUpgrade(ledger, 0, road.Id), Is.True);
            Assert.That(outside.TryUpgrade(ledger, 0, road.Id), Is.True);
            Assert.That(outside.TryUpgrade(ledger, 0, road.Id), Is.False, "maximaal niveau");
            Assert.That(road.Level, Is.EqualTo(OutsideConnection.MaxLevel));
            Assert.That(road.VehiclesPerHour, Is.EqualTo(3 * 3600));

            Assert.That(outside.SettleUpkeep(ledger, 1), Is.EqualTo(3 * 20_000));
            Assert.That(ledger.Expenses(1, LedgerCategory.Upkeep), Is.EqualTo(60_000));
            outside.SetOpen(road.Id, false);
            Assert.That(outside.UpkeepPerWeek, Is.Zero);
            Assert.Throws<ArgumentNullException>(() => outside.TryBuild(null, 0, OutsideKind.Rail, MapEdge.East, 0f));
            Assert.Throws<ArgumentNullException>(() => outside.TryUpgrade(null, 0, 1));
            Assert.Throws<ArgumentNullException>(() => outside.SettleUpkeep(null, 0));
        }

        [Test]
        public void TrafficFollowsTheDayAndIsCappedByCapacity()
        {
            var outside = new OutsideConnections(Size);
            var a = outside.Add(OutsideKind.Highway, MapEdge.North, 100f);
            var b = outside.Add(OutsideKind.Highway, MapEdge.South, 100f);
            outside.Add(OutsideKind.Rail, MapEdge.East, 100f);
            outside.Get(b.Id).Level = 3;

            var rush = outside.HourlyTraffic(8, 100_000);
            var night = outside.HourlyTraffic(3, 100_000);

            Assert.That(rush.Select(f => f.ConnectionId).ToArray(), Is.EqualTo(new[] { a.Id, b.Id }));
            Assert.That(rush[1].Vehicles, Is.EqualTo(3 * rush[0].Vehicles).Within(1));
            Assert.That(rush.Sum(f => f.Vehicles), Is.GreaterThan(night.Sum(f => f.Vehicles)));
            Assert.That(outside.HourlyTraffic(8 + 24, 100_000).Sum(f => f.Vehicles), Is.EqualTo(rush.Sum(f => f.Vehicles)));
            Assert.That(outside.HourlyTraffic(-16, 100_000).Sum(f => f.Vehicles), Is.EqualTo(rush.Sum(f => f.Vehicles)));

            var jam = outside.HourlyTraffic(8, 100_000_000);
            Assert.That(jam[0].Vehicles, Is.EqualTo(3600));
            Assert.That(jam[1].Vehicles, Is.EqualTo(3 * 3600));
            Assert.That(outside.HourlyTraffic(8, -5).All(f => f.Vehicles == 0), Is.True);
        }

        [Test]
        public void ClosedOrMissingRoadsLetNoTrafficIn()
        {
            var outside = new OutsideConnections(Size);
            Assert.That(outside.HourlyTraffic(8, 1000), Is.Empty);
            var road = outside.Add(OutsideKind.Highway, MapEdge.North, 100f);
            outside.SetOpen(road.Id, false);
            Assert.That(outside.HourlyTraffic(8, 1000), Is.Empty);
            Assert.That(outside.TotalVehiclesPerHour, Is.Zero);
        }

        [Test]
        public void TicksSpreadTheHourlyVehiclesWithoutRemainder()
        {
            var total = 0;
            for (var tick = 0; tick < 60; tick++)
            {
                total += OutsideConnections.VehiclesInTick(1234, tick, 60);
            }

            Assert.That(total, Is.EqualTo(1234));
            Assert.That(OutsideConnections.VehiclesInTick(0, 0, 60), Is.Zero);
            Assert.That(OutsideConnections.VehiclesInTick(10, 60, 60), Is.Zero);
            Assert.That(OutsideConnections.VehiclesInTick(10, -1, 60), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => OutsideConnections.VehiclesInTick(10, 0, 0));
        }

        [Test]
        public void VisitorsAndFreightCapacityAddUpOverOpenConnections()
        {
            var outside = new OutsideConnections(Size);
            outside.Add(OutsideKind.Rail, MapEdge.East, 100f);
            var airport = outside.Add(OutsideKind.Airport, MapEdge.West, 100f);

            Assert.That(outside.VisitorsPerDay, Is.EqualTo(8000 + 12000));
            Assert.That(outside.FreightTonnesPerDay, Is.EqualTo(6300f));
            outside.SetOpen(airport.Id, false);
            Assert.That(outside.VisitorsPerDay, Is.EqualTo(8000));
            Assert.That(airport.PassengersPerDay, Is.Zero);
            Assert.That(airport.TonnesPerDay, Is.Zero);
        }

        [Test]
        public void FreightConnectionsBecomeOutsideNodes()
        {
            var outside = new OutsideConnections(Size);
            outside.Add(OutsideKind.Highway, MapEdge.North, 100f);
            var closed = outside.Add(OutsideKind.Rail, MapEdge.East, 200f);
            outside.Add(OutsideKind.Harbour, MapEdge.South, 300f);
            outside.SetOpen(closed.Id, false);
            var network = new FreightNetwork();

            var nodes = outside.AttachFreight(network);

            Assert.That(nodes.Count, Is.EqualTo(2));
            Assert.That(network.Get(nodes[0]).Kind, Is.EqualTo(FreightNodeKind.Outside));
            Assert.That(network.Get(nodes[0]).Position.z, Is.EqualTo(Size));
            Assert.That(network.Get(nodes[1]).Position.z, Is.Zero);
            Assert.Throws<ArgumentNullException>(() => outside.AttachFreight(null));
        }

        [Test]
        public void TradeIsLimitedByTheOpenConnections()
        {
            var outside = new OutsideConnections(Size);
            var surplus = new Dictionary<string, float> { ["staal"] = 20_000f, ["graan"] = 10_000f, ["olie"] = -50_000f, ["hout"] = 0f };

            var closed = OutsideTrade.Limit(outside, surplus);
            Assert.That(closed.Exported, Is.Zero);
            Assert.That(closed.Imported, Is.Zero);
            Assert.That(closed.BlockedExports, Is.EqualTo(30_000f));
            Assert.That(closed.BlockedImports, Is.EqualTo(50_000f));

            outside.Add(OutsideKind.Harbour, MapEdge.South, 100f);
            var open = OutsideTrade.Limit(outside, surplus);
            Assert.That(open.Exported, Is.EqualTo(30_000f).Within(0.01f));
            Assert.That(open.Imported, Is.EqualTo(50_000f).Within(0.01f));

            outside.Add(OutsideKind.Highway, MapEdge.North, 100f);
            outside.Get(1).IsOpen = false;
            var small = OutsideTrade.Limit(outside, surplus);
            Assert.That(small.Exported, Is.EqualTo(4000f * 7).Within(0.01f));
            Assert.That(small.Surplus["staal"], Is.EqualTo(2f * small.Surplus["graan"]).Within(0.01f));
            Assert.That(small.Surplus["olie"], Is.EqualTo(-4000f * 7).Within(0.01f));
            Assert.That(small.Surplus["hout"], Is.Zero);
            Assert.That(small.Surplus.Keys.ToArray(), Is.EqualTo(new[] { "graan", "hout", "olie", "staal" }));
            Assert.Throws<ArgumentNullException>(() => OutsideTrade.Limit(null, surplus));
            Assert.Throws<ArgumentNullException>(() => OutsideTrade.Limit(outside, null));
        }

        [Test]
        public void SettlingBooksOnlyWhatPassedTheBorder()
        {
            var outside = new OutsideConnections(Size);
            outside.Add(OutsideKind.Highway, MapEdge.North, 100f);
            var trade = new Trade();
            trade.SetPrices("staal", 12, 8);
            var ledger = new Ledger(0);
            var surplus = new Dictionary<string, float> { ["staal"] = 56_000f };

            var result = OutsideTrade.Settle(0, outside, trade, ledger, surplus);

            Assert.That(result.Flow.Exported, Is.EqualTo(28_000f).Within(0.01f));
            Assert.That(result.Trade.Exports, Is.EqualTo(28_000 * 8));
            Assert.That(ledger.Income(0, LedgerCategory.Trade), Is.EqualTo(28_000 * 8));
            Assert.Throws<ArgumentNullException>(() => OutsideTrade.Settle(0, outside, null, ledger, surplus));
        }

        [Test]
        public void SaveRoundTripKeepsConnectionsLevelsAndIds()
        {
            var outside = new OutsideConnections(Size);
            outside.Add(OutsideKind.Highway, MapEdge.North, 100f);
            var rail = outside.Add(OutsideKind.Rail, MapEdge.East, 250f);
            rail.Level = 2;
            rail.IsOpen = false;
            outside.Remove(1);
            var bytes = SaveBytes.Write(new SaveMetadata(), new OutsideSection(outside));
            var loaded = new OutsideConnections(Size);
            loaded.Add(OutsideKind.Airport, MapEdge.West, 10f);

            SaveBytes.Read(bytes, new OutsideSection(loaded));

            var c = loaded.Connections.Single();
            Assert.That(c.Kind, Is.EqualTo(OutsideKind.Rail));
            Assert.That(c.Edge, Is.EqualTo(MapEdge.East));
            Assert.That(c.Offset, Is.EqualTo(250f));
            Assert.That(c.Level, Is.EqualTo(2));
            Assert.That(c.IsOpen, Is.False);
            Assert.That(loaded.NextId, Is.EqualTo(outside.NextId));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new OutsideSection(loaded)), Is.EqualTo(bytes));
            Assert.That(new OutsideSection(outside).Id, Is.EqualTo("OUTC"));
            Assert.Throws<ArgumentNullException>(() => new OutsideSection(null));
        }

        [Test]
        public void ACorruptSaveIsRejectedAndChangesNothing()
        {
            var outside = new OutsideConnections(Size);
            outside.Add(OutsideKind.Highway, MapEdge.North, 100f);
            var section = new OutsideSection(outside);

            Assert.Throws<SaveFormatException>(() => section.Read(Save(Size + 1f, 2, 0, null), 1), "andere kaartmaat");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(Size, 2, -1, null), 1), "negatief aantal");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(Size, 2, 1, w => Connection(w, 2, 0, 0, 0f, 1)), 1), "volgnummer te laag");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(Size, 5, 2, w => { Connection(w, 1, 0, 0, 0f, 1); Connection(w, 1, 0, 1, 0f, 1); }), 1), "dubbel ID");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(Size, 5, 1, w => Connection(w, 1, 9, 0, 0f, 1)), 1), "soort");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(Size, 5, 1, w => Connection(w, 1, 0, 9, 0f, 1)), 1), "rand");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(Size, 5, 1, w => Connection(w, 1, 0, 0, Size + 5f, 1)), 1), "offset");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(Size, 5, 1, w => Connection(w, 1, 0, 0, 0f, 4)), 1), "niveau");
            Assert.Throws<SaveFormatException>(() => section.Read(Save(Size, 20, 9, w =>
            {
                for (var i = 1; i <= 9; i++)
                {
                    Connection(w, i, 0, 0, i * 10f, 1);
                }
            }), 1), "te veel van een soort");
            Assert.That(outside.Connections.Count, Is.EqualTo(1));
        }

        private static SaveReader Save(float size, int nextId, int count, Action<SaveWriter> body) => SaveBytes.ReaderFor(w =>
        {
            w.Write(size);
            w.Write(nextId);
            w.Write(count);
            body?.Invoke(w);
        });

        private static void Connection(SaveWriter w, int id, byte kind, byte edge, float offset, int level)
        {
            w.Write(id);
            w.Write(kind);
            w.Write(edge);
            w.Write(offset);
            w.Write(level);
            w.Write(true);
        }
    }
}
