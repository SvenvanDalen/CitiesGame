using System;
using Cities.Core.Persistence;
using Cities.Simulation.Economy;
using Cities.Tests.EditMode.Persistence;
using Cities.World.Grid;
using Cities.World.Tiles;
using NUnit.Framework;

namespace Cities.Tests.EditMode.World
{
    public class TileUnlocksTests
    {
        private static readonly WorldDimensions Dimensions = new WorldDimensions(3, 3, 16, 8f);

        private static TileUnlocks Create() => new TileUnlocks(Dimensions, new TileCoord(1, 1), 1000, 500);

        [Test]
        public void OnlyTheStartTileIsUnlocked()
        {
            var unlocks = Create();

            Assert.That(unlocks.UnlockedCount, Is.EqualTo(1));
            Assert.That(unlocks.IsUnlocked(new TileCoord(1, 1)), Is.True);
            Assert.That(unlocks.IsUnlocked(new TileCoord(0, 1)), Is.False);
            Assert.That(unlocks.IsUnlocked(new TileCoord(-1, 1)), Is.False);
        }

        [Test]
        public void BuildingIsBlockedOutsideUnlockedTiles()
        {
            var unlocks = Create();

            Assert.That(unlocks.BuildReason(new CellCoord(20, 20)), Is.Null);
            Assert.That(unlocks.BuildReason(new CellCoord(2, 2)), Is.EqualTo(TileUnlocks.Locked));
            Assert.That(unlocks.BuildReason(new CellCoord(-1, 2)), Is.EqualTo(TileUnlocks.OutsideMap));
            Assert.That(unlocks.IsUnlocked(new CellCoord(20, 20)), Is.True);
        }

        [Test]
        public void BuyingCostsMoneyAndRaisesThePrice()
        {
            var unlocks = Create();
            var ledger = new Ledger(2500);
            TileCoord? raised = null;
            unlocks.Unlocked += tile => raised = tile;

            Assert.That(unlocks.TryBuy(new TileCoord(1, 0), ledger, 0), Is.True);
            Assert.That(ledger.Balance, Is.EqualTo(1500));
            Assert.That(raised, Is.EqualTo(new TileCoord(1, 0)));
            Assert.That(unlocks.NextPrice, Is.EqualTo(2000));
            Assert.That(unlocks.TryBuy(new TileCoord(0, 0), ledger, 0), Is.False, "2000 is meer dan het saldo");
            Assert.That(ledger.Expenses(0), Is.EqualTo(1000));
        }

        [Test]
        public void BuyingFailsWithoutMoneyOrAdjacency()
        {
            var unlocks = Create();

            Assert.That(unlocks.TryBuy(new TileCoord(1, 0), new Ledger(999), 0), Is.False);
            Assert.That(unlocks.TryBuy(new TileCoord(0, 0), new Ledger(99999), 0), Is.False);
            Assert.That(unlocks.UnlockReason(new TileCoord(0, 0)), Is.EqualTo(TileUnlocks.NotAdjacent));
            Assert.That(unlocks.UnlockReason(new TileCoord(1, 1)), Is.EqualTo(TileUnlocks.AlreadyUnlocked));
            Assert.That(unlocks.UnlockReason(new TileCoord(3, 1)), Is.EqualTo(TileUnlocks.OutsideMap));
            Assert.That(unlocks.UnlockedCount, Is.EqualTo(1));
            Assert.Throws<ArgumentNullException>(() => unlocks.TryBuy(new TileCoord(1, 0), null, 0));
        }

        [Test]
        public void PopulationUnlocksForFreeAboveTheThreshold()
        {
            var unlocks = Create();

            Assert.That(unlocks.TryUnlockByPopulation(new TileCoord(2, 1), 499), Is.False);
            Assert.That(unlocks.TryUnlockByPopulation(new TileCoord(2, 1), 500), Is.True);
            Assert.That(unlocks.NextPopulation, Is.EqualTo(1000));
            Assert.That(unlocks.TryUnlockByPopulation(new TileCoord(0, 0), 5000), Is.False, "niet aangrenzend");
        }

        [Test]
        public void ConstructorRejectsBadArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TileUnlocks(Dimensions, new TileCoord(3, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TileUnlocks(Dimensions, new TileCoord(0, 0), -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TileUnlocks(Dimensions, new TileCoord(0, 0), 1, -1));
        }

        [Test]
        public void SectionRoundTrips()
        {
            var unlocks = Create();
            unlocks.TryUnlockByPopulation(new TileCoord(2, 1), 500);
            var bytes = SaveBytes.Write(new SaveMetadata(), new TileUnlockSection(unlocks));

            var loaded = Create();
            SaveBytes.Read(bytes, new TileUnlockSection(loaded));

            Assert.That(loaded.UnlockedCount, Is.EqualTo(2));
            Assert.That(loaded.IsUnlocked(new TileCoord(2, 1)), Is.True);
            Assert.That(loaded.IsUnlocked(new TileCoord(0, 0)), Is.False);
        }

        [Test]
        public void SectionRejectsOtherMapSizeAndEmptyState()
        {
            var section = new TileUnlockSection(Create());

            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => { w.Write(2); w.Write(2); }), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w =>
            {
                w.Write(3);
                w.Write(3);
                for (var i = 0; i < 9; i++)
                {
                    w.Write(false);
                }
            }), 1));
        }
    }
}
