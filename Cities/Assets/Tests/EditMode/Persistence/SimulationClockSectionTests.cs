using System;
using Cities.Core.Diagnostics;
using Cities.Core.Persistence;
using Cities.Simulation.Commands;
using Cities.Simulation.Timing;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Persistence
{
    public class SimulationClockSectionTests
    {
        [Test]
        public void TickSpeedAndDateRoundTrip()
        {
            var clock = new SimulationClock(SimulationClockSettings.Default);
            clock.SetSpeed(SimulationSpeed.Fastest);
            for (var i = 0; i < 4567; i++)
            {
                clock.Step();
            }

            var bytes = SaveBytes.Write(new SaveMetadata(), new SimulationClockSection(clock, new CommandBus()));
            var loaded = new SimulationClock(SimulationClockSettings.Default);
            var commands = new CommandBus();
            commands.RegisterHandler(new NoOpHandler());
            commands.Enqueue(new NoOp());
            Assert.That(commands.PendingCount, Is.EqualTo(1));
            SaveBytes.Read(bytes, new SimulationClockSection(loaded, commands));

            Assert.That(loaded.Tick, Is.EqualTo(4567));
            Assert.That(loaded.Speed, Is.EqualTo(SimulationSpeed.Fastest));
            Assert.That(loaded.Calendar.Date, Is.EqualTo(clock.Calendar.Date));
            Assert.That(commands.PendingCount, Is.Zero, "Commands van de oude stad zijn weg");
        }

        [Test]
        public void DifferentCalendarSettingsAreReported()
        {
            var clock = new SimulationClock(SimulationClockSettings.Default);
            var bytes = SaveBytes.Write(new SaveMetadata(), new SimulationClockSection(clock, new CommandBus()));
            var settings = SimulationClockSettings.Default;
            settings.TicksPerDay = 50;

            using (var log = new RecordingLogSink())
            {
                SaveBytes.Read(bytes, new SimulationClockSection(new SimulationClock(settings), new CommandBus()));

                Assert.That(log.Lines.Exists(l => l.Level == LogLevel.Warning && l.Message.Contains("kalender")), Is.True);
            }
        }

        [Test]
        public void InvalidContentIsRejected()
        {
            var section = new SimulationClockSection(new SimulationClock(SimulationClockSettings.Default), new CommandBus());

            Assert.Throws<SaveFormatException>(() => section.Read(Content(-1, 1), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(Content(10, 9), 1));
            Assert.Throws<ArgumentNullException>(() => new SimulationClockSection(null, new CommandBus()));
            Assert.Throws<ArgumentNullException>(() => new SimulationClockSection(new SimulationClock(SimulationClockSettings.Default), null));
        }

        private static SaveReader Content(long tick, byte speed) => SaveBytes.ReaderFor(w =>
        {
            w.Write(tick);
            w.Write(speed);
            w.Write(100);
            w.Write(2000);
            w.Write(1);
            w.Write(1);
        });

        private readonly struct NoOp : ISimulationCommand
        {
        }

        private sealed class NoOpHandler : ICommandHandler<NoOp>
        {
            public CommandValidation Validate(in NoOp command) => CommandValidation.Valid();

            public void Execute(in NoOp command)
            {
                // Niets te doen; de test kijkt alleen of het command uit de wachtrij verdwijnt.
            }
        }
    }
}
