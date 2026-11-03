using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Gameplay.Cameras;
using Cities.Gameplay.Cheats;
using Cities.Simulation.Economy;
using Cities.Simulation.Timing;
using Cities.World.Grid;
using Cities.World.Terrain;
using Cities.World.Tiles;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Cheats
{
    public class CheatConsoleTests
    {
        private static readonly WorldDimensions Dimensions = new WorldDimensions(3, 3, 16, 8f);

        private CheatConsole _console;
        private CheatContext _context;
        private FakeSpawner _spawner;
        private FakeTeleporter _teleporter;

        [SetUp]
        public void SetUp()
        {
            _spawner = new FakeSpawner();
            _teleporter = new FakeTeleporter();
            _context = new CheatContext
            {
                Ledger = new Ledger(1000),
                Unlocks = new TileUnlocks(Dimensions, new TileCoord(1, 1), 1000, 500),
                Clock = new SimulationClock(SimulationClockSettings.Default),
                Spawner = _spawner,
                Teleporter = _teleporter,
            };
            _console = new CheatConsole();
            StandardCheats.Register(_console, _context);
        }

        [Test]
        public void TokenizeSplitsOnWhitespaceAndKeepsQuotedText()
        {
            Assert.That(CheatConsole.Tokenize("  geld   50 "), Is.EqualTo(new[] { "geld", "50" }));
            Assert.That(CheatConsole.Tokenize("spawn \"grote bus\" 1 2"), Is.EqualTo(new[] { "spawn", "grote bus", "1", "2" }));
            Assert.That(CheatConsole.Tokenize("a \"\" b"), Is.EqualTo(new[] { "a", string.Empty, "b" }));
            Assert.That(CheatConsole.Tokenize("   "), Is.Empty);
            Assert.That(CheatConsole.Tokenize(null), Is.Empty);
        }

        [Test]
        public void EmptyLineDoesNothing()
        {
            var result = _console.Execute("  ");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Is.Empty);
        }

        [Test]
        public void UnknownCommandIsReported()
        {
            var result = _console.Execute("vlieg");

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("vlieg"));
        }

        [Test]
        public void CommandNamesIgnoreCase()
        {
            Assert.That(_console.Execute("GELD 10").Success, Is.True);
            Assert.That(_context.Ledger.Balance, Is.EqualTo(1010));
        }

        [Test]
        public void HelpListsEveryCommandAndExplainsOne()
        {
            var all = _console.Execute("help");
            var one = _console.Execute("help geld");

            Assert.That(all.Success, Is.True);
            foreach (var name in new[] { "geld", "ontgrendel", "tijd", "spawn", "teleport", "help" })
            {
                Assert.That(all.Message, Does.Contain(name));
            }

            Assert.That(one.Message, Does.StartWith("geld <bedrag>"));
            Assert.That(_console.Execute("help nietbestaand").Success, Is.False);
        }

        [Test]
        public void CheatsCanBeSwitchedOffButHelpStillWorks()
        {
            _console.CheatsEnabled = false;

            Assert.That(_console.Execute("geld 10").Success, Is.False);
            Assert.That(_context.Ledger.Balance, Is.EqualTo(1000));
            Assert.That(_console.Execute("help").Success, Is.True);
        }

        [Test]
        public void CompleteReturnsMatchingNames()
        {
            Assert.That(_console.Complete("t"), Is.EquivalentTo(new[] { "teleport", "tijd" }));
            Assert.That(_console.Complete("G"), Is.EquivalentTo(new[] { "geld" }));
            Assert.That(_console.Complete(null).Count, Is.EqualTo(_console.Names.Count()));
        }

        [Test]
        public void RegisterRejectsBadOrDuplicateNames()
        {
            ConsoleHandler handler = _ => ConsoleResult.Ok(string.Empty);

            Assert.Throws<ArgumentException>(() => _console.Register("twee woorden", null, null, handler));
            Assert.Throws<ArgumentException>(() => _console.Register(" ", null, null, handler));
            Assert.Throws<ArgumentNullException>(() => _console.Register("nieuw", null, null, null));
            Assert.Throws<InvalidOperationException>(() => _console.Register("GELD", null, null, handler));
        }

        [Test]
        public void ExceptionsFromHandlersBecomeErrors()
        {
            _console.Register("stuk", null, null, _ => throw new InvalidOperationException("kapot"));

            var result = _console.Execute("stuk");

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.EqualTo("kapot"));
        }

        [Test]
        public void MoneyAddsAndSubtractsAndRecordsInTheLedger()
        {
            var added = _console.Execute("geld 5000");
            var taken = _console.Execute("geld -2500");

            Assert.That(added.Success && taken.Success, Is.True);
            Assert.That(_context.Ledger.Balance, Is.EqualTo(3500));
            Assert.That(_context.Ledger.Income(0, LedgerCategory.Other), Is.EqualTo(5000));
            Assert.That(_context.Ledger.Expenses(0, LedgerCategory.Other), Is.EqualTo(2500));
            Assert.That(added.Message, Does.Contain("6,000"));
        }

        [Test]
        public void MoneyCanGoNegative()
        {
            _console.Execute("geld -5000");

            Assert.That(_context.Ledger.Balance, Is.EqualTo(-4000));
        }

        [TestCase("geld")]
        [TestCase("geld abc")]
        [TestCase("geld 0")]
        [TestCase("geld 1 2")]
        public void MoneyRejectsBadArguments(string line)
        {
            Assert.That(_console.Execute(line).Success, Is.False);
            Assert.That(_context.Ledger.Balance, Is.EqualTo(1000));
        }

        [Test]
        public void UnlockOpensASingleTileForFree()
        {
            var result = _console.Execute("ontgrendel 0 0");

            Assert.That(result.Success, Is.True);
            Assert.That(_context.Unlocks.IsUnlocked(new TileCoord(0, 0)), Is.True, "ook zonder buurtegel");
            Assert.That(_context.Ledger.Balance, Is.EqualTo(1000));
        }

        [Test]
        public void UnlockReportsAlreadyOpenOutsideMapAndBadInput()
        {
            Assert.That(_console.Execute("ontgrendel 1 1").Message, Is.EqualTo(TileUnlocks.AlreadyUnlocked));
            Assert.That(_console.Execute("ontgrendel 9 9").Message, Is.EqualTo(TileUnlocks.OutsideMap));
            Assert.That(_console.Execute("ontgrendel x y").Success, Is.False);
            Assert.That(_console.Execute("ontgrendel 1").Success, Is.False);
        }

        [Test]
        public void UnlockAllOpensEveryTileAndRaisesTheEvent()
        {
            var raised = 0;
            _context.Unlocks.Unlocked += _ => raised++;

            var result = _console.Execute("ONTGRENDEL alles");

            Assert.That(result.Message, Does.StartWith("8 "));
            Assert.That(_context.Unlocks.UnlockedCount, Is.EqualTo(9));
            Assert.That(raised, Is.EqualTo(8));
            Assert.That(_console.Execute("ontgrendel alles").Message, Does.StartWith("0 "));
        }

        [Test]
        public void TimeSetsTheSpeed()
        {
            var expected = new Dictionary<string, SimulationSpeed>
            {
                { "pauze", SimulationSpeed.Paused },
                { "1", SimulationSpeed.Normal },
                { "2", SimulationSpeed.Fast },
                { "3", SimulationSpeed.Fastest },
            };

            foreach (var pair in expected)
            {
                Assert.That(_console.Execute("tijd " + pair.Key).Success, Is.True, pair.Key);
                Assert.That(_context.Clock.Speed, Is.EqualTo(pair.Value), pair.Key);
            }
        }

        [Test]
        public void TimeSkipsWholeDays()
        {
            var startTick = _context.Clock.Tick;

            var result = _console.Execute("tijd +3");

            Assert.That(result.Success, Is.True);
            Assert.That(_context.Clock.Tick - startTick, Is.EqualTo(3L * _context.Clock.Calendar.TicksPerDay));
            Assert.That(_context.Clock.Calendar.DaysElapsed, Is.EqualTo(3));
        }

        [TestCase("tijd")]
        [TestCase("tijd snel")]
        [TestCase("tijd +0")]
        [TestCase("tijd +31")]
        [TestCase("tijd +x")]
        [TestCase("tijd 1 2")]
        public void TimeRejectsBadArguments(string line)
        {
            var startTick = _context.Clock.Tick;

            Assert.That(_console.Execute(line).Success, Is.False);
            Assert.That(_context.Clock.Tick, Is.EqualTo(startTick));
        }

        [Test]
        public void SpawnPassesKindAndPositionToTheService()
        {
            var result = _console.Execute("spawn bus 120.5 -30");

            Assert.That(result.Success, Is.True);
            Assert.That(_spawner.Calls, Is.EqualTo(new[] { "bus@120.5,-30" }));
        }

        [Test]
        public void SpawnShowsTheServiceErrorAndTheKindsOnBadInput()
        {
            _spawner.Error = "Onbekende soort";

            Assert.That(_console.Execute("spawn drol 1 2").Message, Is.EqualTo("Onbekende soort"));
            var usage = _console.Execute("spawn bus 1");
            Assert.That(usage.Success, Is.False);
            Assert.That(usage.Message, Does.Contain("bus, auto"));
        }

        [Test]
        public void TeleportMovesTheView()
        {
            Assert.That(_console.Execute("teleport 10 20").Success, Is.True);
            Assert.That(_teleporter.Calls, Is.EqualTo(new[] { "10,20" }));

            _teleporter.Error = "Buiten de kaart";
            Assert.That(_console.Execute("teleport 10 20").Message, Is.EqualTo("Buiten de kaart"));
            Assert.That(_console.Execute("teleport 10").Success, Is.False);
        }

        [Test]
        public void MissingPartsGiveAMessageInsteadOfCrashing()
        {
            var empty = new CheatConsole();
            StandardCheats.Register(empty, new CheatContext());

            foreach (var line in new[] { "geld 1", "ontgrendel alles", "tijd 1", "spawn bus 1 2", "teleport 1 2" })
            {
                var result = empty.Execute(line);
                Assert.That(result.Success, Is.False, line);
                Assert.That(result.Message, Does.StartWith("Niet beschikbaar"), line);
            }
        }

        [Test]
        public void RegisterRequiresConsoleAndContext()
        {
            Assert.Throws<ArgumentNullException>(() => StandardCheats.Register(null, new CheatContext()));
            Assert.Throws<ArgumentNullException>(() => StandardCheats.Register(new CheatConsole(), null));
        }

        [Test]
        public void ForceUnlockIgnoresPositionsOutsideTheMap()
        {
            Assert.That(_context.Unlocks.ForceUnlock(new TileCoord(-1, 0)), Is.False);
            Assert.That(_context.Unlocks.ForceUnlock(new TileCoord(3, 0)), Is.False);
        }

        [Test]
        public void CameraTeleporterPutsThePivotOnTheTerrain()
        {
            var controller = new CameraController(
                CameraSettings.Default,
                new CameraRig(new float3(100f, 0f, 100f), 0f, 45f, 300f),
                float2.zero,
                new float2(10_000f, 10_000f),
                new FlatTerrain(12f));
            var teleporter = new CameraTeleporter(controller);

            var result = teleporter.TeleportTo(2500f, 3500f);

            Assert.That(result, Is.Null);
            Assert.That(controller.Current.Pivot.x, Is.EqualTo(2500f).Within(0.01f));
            Assert.That(controller.Current.Pivot.z, Is.EqualTo(3500f).Within(0.01f));
            Assert.That(controller.Current.Pivot.y, Is.EqualTo(12f).Within(0.01f));
            Assert.That(controller.Current, Is.EqualTo(controller.Target));
        }

        [Test]
        public void CameraTeleporterRejectsNonFinitePositions()
        {
            var controller = new CameraController(
                CameraSettings.Default,
                new CameraRig(new float3(100f, 0f, 100f), 0f, 45f, 300f),
                float2.zero,
                new float2(10_000f, 10_000f));
            var teleporter = new CameraTeleporter(controller);

            Assert.That(teleporter.TeleportTo(float.NaN, 1f), Is.Not.Null);
            Assert.That(teleporter.TeleportTo(1f, float.PositiveInfinity), Is.Not.Null);
            Assert.Throws<ArgumentNullException>(() => _ = new CameraTeleporter(null));
        }

        private sealed class FakeSpawner : ISpawnService
        {
            public List<string> Calls { get; } = new List<string>();

            public string Error { get; set; }

            public IReadOnlyList<string> Kinds { get; } = new[] { "bus", "auto" };

            public string Spawn(string kind, float x, float z)
            {
                Calls.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}@{1},{2}", kind, x, z));
                return Error;
            }
        }

        private sealed class FakeTeleporter : IViewTeleporter
        {
            public List<string> Calls { get; } = new List<string>();

            public string Error { get; set; }

            public string TeleportTo(float x, float z)
            {
                Calls.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1}", x, z));
                return Error;
            }
        }
    }

    public class ConsoleHistoryTests
    {
        [Test]
        public void WalksBackAndForthThroughEntries()
        {
            var history = new ConsoleHistory();
            history.Add("een");
            history.Add("twee");

            Assert.That(history.Previous(), Is.EqualTo("twee"));
            Assert.That(history.Previous(), Is.EqualTo("een"));
            Assert.That(history.Previous(), Is.EqualTo("een"), "blijft bij de oudste");
            Assert.That(history.Next(), Is.EqualTo("twee"));
            Assert.That(history.Next(), Is.EqualTo(string.Empty), "voorbij de nieuwste");
        }

        [Test]
        public void IgnoresBlankAndRepeatedLinesAndResetsTheCursor()
        {
            var history = new ConsoleHistory();
            history.Add("a");
            history.Add("a");
            history.Add("  ");
            history.Add(null);

            Assert.That(history.Count, Is.EqualTo(1));
            history.Previous();
            history.Add(string.Empty);
            Assert.That(history.Previous(), Is.EqualTo("a"));
        }

        [Test]
        public void EmptyHistoryReturnsNull()
        {
            var history = new ConsoleHistory();

            Assert.That(history.Previous(), Is.Null);
            Assert.That(history.Next(), Is.Null);
        }

        [Test]
        public void DropsTheOldestBeyondCapacity()
        {
            var history = new ConsoleHistory(2);
            history.Add("1");
            history.Add("2");
            history.Add("3");

            Assert.That(history.Count, Is.EqualTo(2));
            history.Previous();
            Assert.That(history.Previous(), Is.EqualTo("2"));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ConsoleHistory(0));
        }
    }
}
