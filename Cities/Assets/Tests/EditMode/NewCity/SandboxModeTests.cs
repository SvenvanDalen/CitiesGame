using System;
using System.Collections.Generic;
using Cities.Gameplay.NewCity;
using Cities.Simulation.Economy;
using Cities.Simulation.Milestones;
using Cities.World.Grid;
using Cities.World.Tiles;
using NUnit.Framework;

namespace Cities.Tests.EditMode.NewCity
{
    public class SandboxModeTests
    {
        private static readonly WorldDimensions Dimensions = new WorldDimensions(3, 3, 16, 8f);

        private static TileUnlocks Tiles() => new TileUnlocks(Dimensions, new TileCoord(1, 1), 1000, 500);

        [Test]
        public void TheSandboxPresetHasUnlimitedMoneyAndEverythingUnlocked()
        {
            var sandbox = DifficultySettings.Sandbox;

            Assert.That(sandbox.IsSandbox, Is.True);
            Assert.That(sandbox.UnlimitedMoney, Is.True);
            Assert.That(sandbox.AllUnlocked, Is.True);
            Assert.That(sandbox.Cost(5000), Is.Zero);
            Assert.That(DifficultySettings.For(Difficulty.Normal).IsSandbox, Is.False);
            Assert.That(DifficultySettings.For(Difficulty.Normal, unlimitedMoney: true).IsSandbox, Is.False);
        }

        [Test]
        public void UnlimitedMoneyKeepsTheBalanceButStillBooksExpenses()
        {
            var ledger = new Ledger(100) { UnlimitedMoney = true };

            Assert.That(ledger.CanAfford(1_000_000), Is.True);
            Assert.That(ledger.TrySpend(0, LedgerCategory.Construction, 5000), Is.True);
            ledger.Force(0, LedgerCategory.Interest, 300);
            ledger.Earn(0, LedgerCategory.Taxes, 50);

            Assert.That(ledger.Balance, Is.EqualTo(150));
            Assert.That(ledger.Expenses(0), Is.EqualTo(5300));
            Assert.That(ledger.Expenses(0, LedgerCategory.Construction), Is.EqualTo(5000));
        }

        [Test]
        public void WithoutUnlimitedMoneyASpendBeyondTheBalanceStillFails()
        {
            var ledger = new Ledger(100);

            Assert.That(ledger.CanAfford(101), Is.False);
            Assert.That(ledger.TrySpend(0, LedgerCategory.Construction, 101), Is.False);
            Assert.That(ledger.Balance, Is.EqualTo(100));
        }

        [Test]
        public void UnlockAllOpensEveryTileAndRaisesTheEventForEachNewOne()
        {
            var tiles = Tiles();
            var raised = new List<TileCoord>();
            tiles.Unlocked += raised.Add;

            tiles.UnlockAll();
            tiles.UnlockAll();

            Assert.That(tiles.UnlockedCount, Is.EqualTo(Dimensions.TileCount));
            Assert.That(raised, Has.Count.EqualTo(Dimensions.TileCount - 1));
            Assert.That(tiles.IsUnlocked(new TileCoord(0, 0)), Is.True);
            Assert.That(tiles.IsUnlocked(new TileCoord(2, 2)), Is.True);
        }

        [Test]
        public void ApplyingTheSandboxMakesMoneyUnlimitedAndOpensTheMap()
        {
            var ledger = new Ledger(10);
            var tiles = Tiles();

            SandboxMode.Apply(DifficultySettings.Sandbox, ledger, tiles);

            Assert.That(ledger.UnlimitedMoney, Is.True);
            Assert.That(tiles.UnlockedCount, Is.EqualTo(Dimensions.TileCount));
        }

        [Test]
        public void ApplyingTheSandboxOpensEveryMilestoneUnlock()
        {
            var unlocks = new UnlockService(new MilestoneTracker());
            var released = new List<string>();
            unlocks.Unlocked += released.Add;

            var opened = SandboxMode.Apply(DifficultySettings.Sandbox, new Ledger(10), Tiles(), unlocks);

            Assert.That(unlocks.AllUnlocked, Is.True);
            Assert.That(unlocks.IsUnlocked("tool.airport"), Is.True);
            Assert.That(opened, Is.EqualTo(released));
            Assert.That(opened, Is.Not.Empty);
        }

        [Test]
        public void ANormalGameKeepsTheMilestoneUnlocksClosed()
        {
            var unlocks = new UnlockService(new MilestoneTracker());

            var opened = SandboxMode.Apply(DifficultySettings.For(Difficulty.Normal), new Ledger(10), Tiles(), unlocks);

            Assert.That(opened, Is.Empty);
            Assert.That(unlocks.IsUnlocked("tool.airport"), Is.False);
        }

        [Test]
        public void ApplyingANormalGameLeavesTheSystemsAlone()
        {
            var ledger = new Ledger(10);
            var tiles = Tiles();

            SandboxMode.Apply(DifficultySettings.For(Difficulty.Normal), ledger, tiles);

            Assert.That(ledger.UnlimitedMoney, Is.False);
            Assert.That(tiles.UnlockedCount, Is.EqualTo(1));
        }

        [Test]
        public void ApplyRejectsMissingSystems()
        {
            Assert.Throws<ArgumentNullException>(() => SandboxMode.Apply(DifficultySettings.Sandbox, null, Tiles()));
            Assert.Throws<ArgumentNullException>(() => SandboxMode.Apply(DifficultySettings.Sandbox, new Ledger(0), null));
        }
    }
}
