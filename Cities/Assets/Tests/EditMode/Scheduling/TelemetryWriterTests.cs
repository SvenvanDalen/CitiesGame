using System;
using System.IO;
using Cities.Simulation.Scheduling;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Scheduling
{
    public sealed class TelemetryWriterTests
    {
        private static SystemTimings Timings()
        {
            var timings = new SystemTimings();
            timings.Record(SystemTimings.TickName, 2d);
            timings.Record("Verkeer", 1.5d);
            return timings;
        }

        [Test]
        public void WritesHeaderFirst()
        {
            var text = new StringWriter();
            using (new TelemetryWriter(text, Timings()))
            {
            }

            Assert.That(text.ToString().TrimEnd(), Is.EqualTo(TelemetryWriter.Header));
        }

        [Test]
        public void WritesOnlyOnTheInterval()
        {
            using var writer = new TelemetryWriter(new StringWriter(), Timings(), 10);
            Assert.That(writer.OnTick(5), Is.False);
            Assert.That(writer.OnTick(10), Is.True);
            Assert.That(writer.OnTick(10), Is.False, "dezelfde tick niet dubbel");
            Assert.That(writer.Samples, Is.EqualTo(1));
        }

        [Test]
        public void WritesTickAndEverySystem()
        {
            var text = new StringWriter();
            using var writer = new TelemetryWriter(text, Timings(), 1);
            writer.OnTick(1);
            var lines = text.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Assert.That(lines, Does.Contain("1,Tick,ms,2.000,2.000,2.000"));
            Assert.That(lines, Does.Contain("1,Verkeer,ms,1.500,1.500,1.500"));
        }

        [Test]
        public void WritesCounters()
        {
            var text = new StringWriter();
            long citizens = 1200;
            using var writer = new TelemetryWriter(text, Timings(), 1);
            writer.AddCounter("Burgers", () => citizens);
            writer.OnTick(1);
            Assert.That(text.ToString(), Does.Contain("1,Burgers,count,1200,1200,1200"));
        }

        [Test]
        public void EscapesNamesWithCommas()
        {
            var text = new StringWriter();
            using var writer = new TelemetryWriter(text, Timings(), 1);
            writer.AddCounter("a,b\"c", () => 1);
            writer.OnTick(1);
            Assert.That(text.ToString(), Does.Contain("\"a,b\"\"c\",count"));
        }

        [Test]
        public void OwnedWriterIsDisposed()
        {
            var text = new StringWriter();
            new TelemetryWriter(text, Timings(), 1, true).Dispose();
            Assert.Throws<ObjectDisposedException>(() => text.Write("x"));
        }

        [Test]
        public void RejectsBadArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TelemetryWriter(new StringWriter(), Timings(), 0));
            Assert.Throws<ArgumentNullException>(() => new TelemetryWriter(null, Timings()));
            Assert.Throws<ArgumentNullException>(() => new TelemetryWriter(new StringWriter(), null));
            using var writer = new TelemetryWriter(new StringWriter(), Timings());
            Assert.Throws<ArgumentException>(() => writer.AddCounter(" ", () => 1));
            Assert.Throws<ArgumentNullException>(() => writer.AddCounter("x", null));
        }
    }
}
