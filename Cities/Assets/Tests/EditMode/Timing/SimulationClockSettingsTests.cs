using System;
using Cities.Simulation.Timing;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Timing
{
    public class SimulationClockSettingsTests
    {
        [Test]
        public void DefaultsAreValid()
        {
            Assert.That(SimulationClockSettings.Default.FindError(), Is.Null);
            Assert.DoesNotThrow(() => SimulationClockSettings.Validate(SimulationClockSettings.Default));
        }

        [Test]
        public void TickSecondsIsInverseOfRate()
        {
            Assert.That(SimulationClockSettings.Default.TickSeconds, Is.EqualTo(0.1).Within(1e-12));
        }

        [TestCase(nameof(SimulationClockSettings.TicksPerSecond))]
        [TestCase(nameof(SimulationClockSettings.MaxTicksPerUpdate))]
        [TestCase(nameof(SimulationClockSettings.MaxFrameSeconds))]
        [TestCase(nameof(SimulationClockSettings.TicksPerDay))]
        [TestCase(nameof(SimulationClockSettings.StartYear))]
        [TestCase(nameof(SimulationClockSettings.StartMonth))]
        [TestCase(nameof(SimulationClockSettings.StartDay))]
        public void ZeroValueIsReportedByName(string field)
        {
            var settings = SimulationClockSettings.Default;
            switch (field)
            {
                case nameof(SimulationClockSettings.TicksPerSecond): settings.TicksPerSecond = 0; break;
                case nameof(SimulationClockSettings.MaxTicksPerUpdate): settings.MaxTicksPerUpdate = 0; break;
                case nameof(SimulationClockSettings.MaxFrameSeconds): settings.MaxFrameSeconds = 0f; break;
                case nameof(SimulationClockSettings.TicksPerDay): settings.TicksPerDay = 0; break;
                case nameof(SimulationClockSettings.StartYear): settings.StartYear = 0; break;
                case nameof(SimulationClockSettings.StartMonth): settings.StartMonth = 0; break;
                default: settings.StartDay = 0; break;
            }

            Assert.That(settings.FindError(), Does.StartWith(field));
            var error = Assert.Throws<ArgumentException>(() => SimulationClockSettings.Validate(settings));
            Assert.That(error.ParamName, Is.EqualTo("settings"));
        }

        [TestCase(9001, 1, 1)]
        [TestCase(2000, 13, 1)]
        [TestCase(2001, 2, 29)]
        public void OutOfRangeDatesAreInvalid(int year, int month, int day)
        {
            var settings = SimulationClockSettings.Default;
            settings.StartYear = year;
            settings.StartMonth = month;
            settings.StartDay = day;

            Assert.That(settings.FindError(), Is.Not.Null);
        }

        [Test]
        public void LeapDayIsValidInLeapYear()
        {
            var settings = SimulationClockSettings.Default;
            settings.StartYear = 2024;
            settings.StartMonth = 2;
            settings.StartDay = 29;

            Assert.That(settings.FindError(), Is.Null);
        }
    }
}
