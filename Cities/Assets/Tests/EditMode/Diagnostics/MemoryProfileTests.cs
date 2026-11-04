using System;
using Cities.Core.Diagnostics;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Diagnostics
{
    public class MemoryProfileTests
    {
        private const long Mb = 1024 * 1024;

        [Test]
        public void MeasureListsLargestFirst()
        {
            var profile = new MemoryProfile();
            profile.Register("Wegen", () => 3 * Mb);
            profile.Register("Kaart", () => 10 * Mb);
            profile.Register("Agents", () => 3 * Mb);

            var usages = profile.Measure();

            Assert.That(usages[0].System, Is.EqualTo("Kaart"));
            Assert.That(usages[1].System, Is.EqualTo("Agents"));
            Assert.That(usages[2].System, Is.EqualTo("Wegen"));
            Assert.That(usages[0].Megabytes, Is.EqualTo(10.0));
            Assert.That(profile.TotalBytes(), Is.EqualTo(16 * Mb));
            Assert.That(profile.Count, Is.EqualTo(3));
        }

        [Test]
        public void RegisteringAgainReplacesAndUnregisterRemoves()
        {
            var profile = new MemoryProfile();
            profile.Register("A", () => 1);
            profile.Register("A", () => 2);

            Assert.That(profile.TotalBytes(), Is.EqualTo(2));
            Assert.That(profile.Unregister("A"), Is.True);
            Assert.That(profile.Unregister("A"), Is.False);
        }

        [Test]
        public void FailingReporterCountsAsZeroAndLogsAWarning()
        {
            using (var sink = new Persistence.RecordingLogSink())
            {
                var profile = new MemoryProfile();
                profile.Register("Stuk", () => throw new InvalidOperationException("kapot"));
                profile.Register("Goed", () => 5);

                Assert.That(profile.TotalBytes(), Is.EqualTo(5));
                Assert.That(sink.Lines, Has.Some.Matches<(LogLevel Level, string Message)>(line => line.Message.Contains("Stuk")));
            }
        }

        [Test]
        public void CheckComparesWithTheBudget()
        {
            var profile = new MemoryProfile();
            profile.Register("Kaart", () => 1_000 * Mb);
            Assert.That(profile.Check(10_000), Is.Null);

            profile.Register("Agents", () => 1_500 * Mb);
            Assert.That(profile.Check(10_000), Does.Contain("2500").Or.Contain("2 500").Or.Contain("2500,"));
            Assert.That(profile.Check(1_000_000), Is.Null);
        }

        [Test]
        public void DescribeHasOneLinePerSystem()
        {
            var profile = new MemoryProfile();
            profile.Register("Kaart", () => 2 * Mb);
            profile.Register("Wegen", () => Mb / 2);

            Assert.That(profile.Describe().Split('\n'), Has.Length.EqualTo(2));
            Assert.That(profile.Describe(), Does.StartWith("Kaart: "));
        }

        [Test]
        public void InvalidRegistrationsAreRejected()
        {
            var profile = new MemoryProfile();

            Assert.Throws<ArgumentException>(() => profile.Register(" ", () => 1));
            Assert.Throws<ArgumentNullException>(() => profile.Register("A", null));
        }
    }
}
