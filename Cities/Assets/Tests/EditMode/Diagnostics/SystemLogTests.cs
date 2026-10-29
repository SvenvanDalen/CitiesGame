using System;
using System.Collections.Generic;
using Cities.Core.Diagnostics;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Diagnostics
{
    public class SystemLogTests
    {
        private sealed class RecordingSink : ILogSink
        {
            public List<(LogLevel Level, string Category, string Message)> Lines { get; } = new List<(LogLevel, string, string)>();

            public void Write(LogLevel level, string category, string message) => Lines.Add((level, category, message));
        }

        /// <summary>Telt hoe vaak het als argument wordt opgemaakt, om te bewijzen dat uitgeschakelde niveaus niets opmaken.</summary>
        private sealed class CountingArgument
        {
            public int Formatted { get; private set; }

            public override string ToString()
            {
                Formatted++;
                return "arg";
            }
        }

        private ILogSink _previousSink;
        private RecordingSink _sink;

        [SetUp]
        public void SetUp()
        {
            LogLevels.Reset();
            _previousSink = SystemLog.Sink;
            _sink = new RecordingSink();
            SystemLog.Sink = _sink;
        }

        [TearDown]
        public void TearDown()
        {
            SystemLog.Sink = _previousSink;
            LogLevels.Reset();
        }

        [Test]
        public void DefaultLevelIsInfo()
        {
            var log = new SystemLog("Traffic");

            log.Debug("verborgen");
            log.Info("zichtbaar");
            log.Warning("let op");
            log.Error("fout");

            Assert.That(_sink.Lines.ConvertAll(l => l.Message), Is.EqualTo(new[] { "zichtbaar", "let op", "fout" }));
            Assert.That(_sink.Lines[0].Category, Is.EqualTo("Traffic"));
        }

        [Test]
        public void CategoryLevelOverridesDefaultAndIsPickedUpLive()
        {
            var traffic = new SystemLog("Traffic");
            var water = new SystemLog("Water");
            traffic.Debug("eerst niet");

            LogLevels.Set("traffic", LogLevel.Trace);
            traffic.Trace("nu wel");
            water.Debug("water niet");

            Assert.That(_sink.Lines.ConvertAll(l => l.Message), Is.EqualTo(new[] { "nu wel" }), "Categorieën zijn niet hoofdlettergevoelig");
            Assert.That(traffic.Level, Is.EqualTo(LogLevel.Trace));
            Assert.That(water.Level, Is.EqualTo(LogLevel.Info));
        }

        [Test]
        public void DisabledLevelDoesNotFormat()
        {
            var log = new SystemLog("Traffic");
            var argument = new CountingArgument();

            log.Debug("waarde {0}", argument);
            log.Debug("waarden {0} {1}", argument, argument);
            log.Trace("waarde {0}", argument);

            Assert.That(argument.Formatted, Is.EqualTo(0));
            Assert.That(_sink.Lines, Is.Empty);
        }

        [Test]
        public void EnabledLevelFormatsInvariantly()
        {
            var log = new SystemLog("Economy");
            LogLevels.Default = LogLevel.Trace;

            log.Info("Budget {0}", 1234.5);
            log.Info("{0} van {1}", 3, 4);
            log.Debug("{0}", "a");
            log.Debug("{0}-{1}", "a", "b");
            log.Trace("{0}", 1);
            log.Warning("{0}", 2);

            Assert.That(_sink.Lines.ConvertAll(l => l.Message), Is.EqualTo(new[] { "Budget 1234.5", "3 van 4", "a", "a-b", "1", "2" }));
        }

        [Test]
        public void NoneSilencesEverything()
        {
            var log = new SystemLog("Traffic");
            LogLevels.Set("Traffic", LogLevel.None);

            log.Error("stil");
            log.Error(new InvalidOperationException("x"), "stil");

            Assert.That(_sink.Lines, Is.Empty);
            Assert.That(log.IsEnabled(LogLevel.None), Is.False);
        }

        [Test]
        public void ExceptionIsIncluded()
        {
            new SystemLog("Traffic").Error(new InvalidOperationException("kapot"), "Route faalde");

            Assert.That(_sink.Lines[0].Level, Is.EqualTo(LogLevel.Error));
            Assert.That(_sink.Lines[0].Message, Does.StartWith("Route faalde: System.InvalidOperationException: kapot"));
        }

        [Test]
        public void ClearRestoresDefault()
        {
            var log = new SystemLog("Traffic");
            LogLevels.Set("Traffic", LogLevel.Error);

            LogLevels.Clear("Traffic");
            LogLevels.Clear("Onbekend");
            LogLevels.Clear(null);

            Assert.That(log.Level, Is.EqualTo(LogLevel.Info));
        }

        [Test]
        public void SpecificationSetsLevelsAndReportsMistakes()
        {
            var errors = new List<string>();

            LogLevels.Apply("Traffic=Debug; Water=warning,*=Error, kapot, Zones=Luid, =Info, Pad=7", errors);

            Assert.That(LogLevels.Get("Traffic"), Is.EqualTo(LogLevel.Debug));
            Assert.That(LogLevels.Get("Water"), Is.EqualTo(LogLevel.Warning));
            Assert.That(LogLevels.Default, Is.EqualTo(LogLevel.Error));
            Assert.That(LogLevels.Get("Zones"), Is.EqualTo(LogLevel.Error), "Ongeldig niveau valt terug op standaard");
            Assert.That(errors.Count, Is.EqualTo(4));
        }

        [Test]
        public void CommandLineOptionIsRecognised()
        {
            var errors = new List<string>();

            LogLevels.ApplyCommandLine(new[] { "Cities.exe", "-batchmode", "-LOGLEVEL", "Traffic=Trace" }, errors);
            LogLevels.ApplyCommandLine(new[] { "-loglevel" }, errors);
            LogLevels.ApplyCommandLine(null, errors);
            LogLevels.Apply("  ", errors);

            Assert.That(LogLevels.Get("Traffic"), Is.EqualTo(LogLevel.Trace));
            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void InvalidArgumentsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => _ = new SystemLog(" "));
            Assert.Throws<ArgumentException>(() => LogLevels.Set("", LogLevel.Debug));
            Assert.Throws<ArgumentNullException>(() => SystemLog.Sink = null);
        }
    }
}
