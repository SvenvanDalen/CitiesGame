using System;
using System.Collections.Generic;
using Cities.Simulation.Timing;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Timing
{
    public class SimulationClockTests
    {
        private static SimulationClock CreateClock()
        {
            var settings = SimulationClockSettings.Default;
            settings.MaxTicksPerUpdate = 100;
            settings.MaxFrameSeconds = 10f;
            return new SimulationClock(settings);
        }

        [Test]
        public void NormalSpeedRunsTicksPerSecond()
        {
            var clock = CreateClock();

            var ticks = clock.Advance(1.0);

            Assert.That(ticks, Is.EqualTo(10));
            Assert.That(clock.Tick, Is.EqualTo(10));
        }

        [TestCase(SimulationSpeed.Normal, 10)]
        [TestCase(SimulationSpeed.Fast, 20)]
        [TestCase(SimulationSpeed.Fastest, 30)]
        [TestCase(SimulationSpeed.Paused, 0)]
        public void SpeedMultipliesTicks(SimulationSpeed speed, int expected)
        {
            var clock = CreateClock();
            clock.SetSpeed(speed);

            clock.Advance(1.0);

            Assert.That(clock.Tick, Is.EqualTo(expected));
        }

        [Test]
        public void FrameRateDoesNotChangeTickRate()
        {
            var at30 = CreateClock();
            var at144 = CreateClock();

            for (var i = 0; i < 30 * 60; i++)
            {
                at30.Advance(1.0 / 30);
            }

            for (var i = 0; i < 144 * 60; i++)
            {
                at144.Advance(1.0 / 144);
            }

            Assert.That(at30.Tick, Is.EqualTo(600));
            Assert.That(at144.Tick, Is.InRange(599, 600));
        }

        [Test]
        public void TicksAreCappedPerUpdateAndExcessIsDropped()
        {
            var settings = SimulationClockSettings.Default;
            settings.MaxTicksPerUpdate = 4;
            settings.MaxFrameSeconds = 10f;
            var clock = new SimulationClock(settings);

            var ticks = clock.Advance(1.0);

            Assert.That(ticks, Is.EqualTo(4));
            Assert.That(clock.DroppedTicks, Is.EqualTo(6));
            Assert.That(clock.Advance(0.0), Is.EqualTo(0), "Overgeslagen ticks worden niet later ingehaald");
        }

        [Test]
        public void LongFramesAreClamped()
        {
            var settings = SimulationClockSettings.Default;
            settings.MaxTicksPerUpdate = 100;
            settings.MaxFrameSeconds = 0.25f;
            var clock = new SimulationClock(settings);

            clock.Advance(5.0);

            Assert.That(clock.Tick, Is.EqualTo(2));
        }

        [Test]
        public void TogglePauseRestoresPreviousSpeed()
        {
            var clock = CreateClock();
            clock.SetSpeed(SimulationSpeed.Fastest);

            clock.TogglePause();
            Assert.That(clock.Speed, Is.EqualTo(SimulationSpeed.Paused));

            clock.TogglePause();
            Assert.That(clock.Speed, Is.EqualTo(SimulationSpeed.Fastest));
        }

        [Test]
        public void StepRunsOneTickWhilePaused()
        {
            var clock = CreateClock();
            clock.SetSpeed(SimulationSpeed.Paused);
            var raised = new List<long>();
            clock.Ticked += raised.Add;

            clock.Step();

            Assert.That(raised, Is.EqualTo(new[] { 1L }));
        }

        [Test]
        public void SpeedChangedOnlyFiresOnChange()
        {
            var clock = CreateClock();
            var changes = new List<SimulationSpeed>();
            clock.SpeedChanged += changes.Add;

            clock.SetSpeed(SimulationSpeed.Normal);
            clock.SetSpeed(SimulationSpeed.Fast);
            clock.SetSpeed(SimulationSpeed.Fast);

            Assert.That(changes, Is.EqualTo(new[] { SimulationSpeed.Fast }));
        }

        [Test]
        public void InterpolationFactorIsFractionOfNextTick()
        {
            var clock = CreateClock();

            clock.Advance(0.25);

            Assert.That(clock.Tick, Is.EqualTo(2));
            Assert.That(clock.InterpolationFactor, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void RestoreSetsTickAndDateWithoutEvents()
        {
            var clock = CreateClock();
            var raised = 0;
            clock.Ticked += _ => raised++;
            clock.Calendar.DayStarted += _ => raised++;

            clock.Restore(100 * 31, SimulationSpeed.Fast);

            Assert.That(raised, Is.EqualTo(0));
            Assert.That(clock.Tick, Is.EqualTo(3100));
            Assert.That(clock.Speed, Is.EqualTo(SimulationSpeed.Fast));
            Assert.That(clock.Calendar.Date, Is.EqualTo(new GameDate(2000, 2, 1)));
        }

        [Test]
        public void InvalidSettingsAreRejected()
        {
            var settings = SimulationClockSettings.Default;
            settings.StartMonth = 2;
            settings.StartDay = 30;

            Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationClock(settings));
        }

        [Test]
        public void SameInputGivesSameTickSequence()
        {
            var frames = new[] { 0.016, 0.033, 0.1, 0.007, 0.25, 0.05 };
            var a = CreateClock();
            var b = CreateClock();

            foreach (var frame in frames)
            {
                Assert.That(a.Advance(frame), Is.EqualTo(b.Advance(frame)));
            }

            Assert.That(a.Tick, Is.EqualTo(b.Tick));
        }
    }
}
