using System;
using Cities.Simulation.Scheduling;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Diagnostics
{
    public class SystemTimingsTests
    {
        [Test]
        public void AveragesAndMaximaCoverTheWindow()
        {
            var timings = new SystemTimings(windowSize: 3);

            timings.Record("Verkeer", 1.0);
            timings.Record("Verkeer", 2.0);
            timings.Record("Verkeer", 6.0);
            var full = timings.Get("Verkeer");
            timings.Record("Verkeer", 1.0);
            var rolled = timings.Get("Verkeer");

            Assert.That(full.AverageMs, Is.EqualTo(3.0).Within(1e-9));
            Assert.That(full.MaxMs, Is.EqualTo(6.0));
            Assert.That(rolled.Samples, Is.EqualTo(3));
            Assert.That(rolled.AverageMs, Is.EqualTo(3.0).Within(1e-9), "1,0 viel uit het venster, 1,0 kwam erbij");
            Assert.That(rolled.LastMs, Is.EqualTo(1.0));
        }

        [Test]
        public void TickIsKeptApartFromTheSystems()
        {
            var timings = new SystemTimings();

            timings.Record("Burgers", 0.5);
            timings.Record(SystemTimings.TickName, 0.8);
            timings.Record("Verkeer", 0.3);
            timings.Record("Burgers", -2.0);

            Assert.That(timings.Systems, Is.EqualTo(new[] { "Burgers", "Verkeer" }));
            Assert.That(timings.Tick.LastMs, Is.EqualTo(0.8));
            Assert.That(timings.Get("Burgers").LastMs, Is.Zero, "Negatieve tijden worden 0");
            Assert.That(timings.Get("Onbekend").Samples, Is.Zero);

            timings.Clear();
            Assert.That(timings.Systems, Is.Empty);
            Assert.That(timings.Tick.Samples, Is.Zero);
        }

        [Test]
        public void ArgumentsAreChecked()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SystemTimings(0));
            Assert.Throws<ArgumentNullException>(() => new SystemTimings().Record(null, 1.0));
            Assert.That(new SystemTimings(7).WindowSize, Is.EqualTo(7));
        }
    }
}
