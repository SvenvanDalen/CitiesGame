using System;
using System.Collections.Generic;
using Cities.Simulation.Commands;
using Cities.Simulation.Scheduling;
using Cities.Simulation.Timing;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Scheduling
{
    public class SimulationRunnerTests
    {
        private struct MarkCommand : ISimulationCommand
        {
        }

        private sealed class MarkHandler : ICommandHandler<MarkCommand>
        {
            private readonly List<string> _log;

            public MarkHandler(List<string> log) => _log = log;

            public CommandValidation Validate(in MarkCommand command) => CommandValidation.Valid();

            public void Execute(in MarkCommand command) => _log.Add("command");
        }

        /// <summary>Legt vast in welke volgorde de runner plant en wacht.</summary>
        private sealed class RecordingScheduler : ITickScheduler
        {
            private readonly List<string> _log;

            public RecordingScheduler(List<string> log) => _log = log;

            public List<SimulationTick> Ticks { get; } = new List<SimulationTick>();

            public void ScheduleTick(in SimulationTick tick)
            {
                Ticks.Add(tick);
                _log.Add($"tick {tick.Tick}");
            }

            public void ExecuteOnMainThread(Action work)
            {
                _log.Add("complete");
                work();
            }
        }

        private List<string> _log;
        private SimulationClock _clock;
        private CommandBus _bus;
        private RecordingScheduler _scheduler;
        private SimulationRunner _runner;
        private List<CommandRecord> _records;

        [SetUp]
        public void SetUp()
        {
            _log = new List<string>();
            var settings = SimulationClockSettings.Default;
            settings.MaxTicksPerUpdate = 100;
            settings.MaxFrameSeconds = 10f;
            _clock = new SimulationClock(settings);
            _bus = new CommandBus();
            _bus.RegisterHandler(new MarkHandler(_log));
            _records = new List<CommandRecord>();
            _bus.CommandProcessed += _records.Add;
            _scheduler = new RecordingScheduler(_log);
            _runner = new SimulationRunner(_clock, _bus, _scheduler);
        }

        [TearDown]
        public void TearDown() => _runner.Dispose();

        [Test]
        public void EveryTickIsScheduledWithoutWaiting()
        {
            var ticks = _runner.Update(0.3);

            Assert.That(ticks, Is.EqualTo(3));
            Assert.That(_log, Is.EqualTo(new[] { "tick 1", "tick 2", "tick 3" }));
            Assert.That(_runner.ScheduledTicks, Is.EqualTo(3));
        }

        [Test]
        public void TickCarriesFixedStepAndCalendar()
        {
            _clock.Restore(99, SimulationSpeed.Normal);

            _runner.Update(0.1);

            var tick = _scheduler.Ticks[0];
            Assert.That(tick.Tick, Is.EqualTo(100));
            Assert.That(tick.StepSeconds, Is.EqualTo(0.1f).Within(1e-6f));
            Assert.That(tick.Date, Is.EqualTo(new GameDate(2000, 1, 2)));
            Assert.That(tick.TimeOfDay, Is.EqualTo(0f));
        }

        [Test]
        public void CommandsRunBeforeSystemsAfterWaitingForJobs()
        {
            _runner.Update(0.1);
            _bus.Enqueue(new MarkCommand());

            _runner.Update(0.1);

            Assert.That(_log, Is.EqualTo(new[] { "tick 1", "complete", "command", "tick 2" }));
            Assert.That(_records[0].Tick, Is.EqualTo(1), "Laatste tick vóór het command");
        }

        [Test]
        public void CommandsRunImmediatelyWhilePaused()
        {
            _runner.Update(0.1);
            _clock.SetSpeed(SimulationSpeed.Paused);
            _bus.Enqueue(new MarkCommand());

            var ticks = _runner.Update(0.1);

            Assert.That(ticks, Is.EqualTo(0));
            Assert.That(_log, Is.EqualTo(new[] { "tick 1", "complete", "command" }));
            Assert.That(_records[0].Tick, Is.EqualTo(1), "Zelfde nummer als bij uitvoeren aan het begin van tick 2");
        }

        [Test]
        public void FramesWithoutCommandsNeverWait()
        {
            for (var i = 0; i < 20; i++)
            {
                _runner.Update(0.016);
            }

            Assert.That(_log, Has.None.EqualTo("complete"));
        }

        [Test]
        public void DisposeStopsListeningToClock()
        {
            _runner.Dispose();

            _clock.Advance(0.5);

            Assert.That(_scheduler.Ticks, Is.Empty);
        }

        [Test]
        public void RequiresAllParts()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new SimulationRunner(null, _bus, _scheduler));
            Assert.Throws<ArgumentNullException>(() => _ = new SimulationRunner(_clock, null, _scheduler));
            Assert.Throws<ArgumentNullException>(() => _ = new SimulationRunner(_clock, _bus, null));
        }
    }
}
