using System;
using System.IO.Compression;
using Cities.Core.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Persistence
{
    public class SaveTimingsTests
    {
        [Test]
        public void WithinBudgetHasNoViolations()
        {
            var timings = new SaveTimings();
            timings.RecordSave(TimeSpan.FromSeconds(0.2), 5 * 1024 * 1024);
            timings.RecordLoad(TimeSpan.FromSeconds(2));

            Assert.That(timings.Violations(10_000), Is.Empty);
            Assert.That(timings.LastSaveSeconds, Is.EqualTo(0.2).Within(1e-9));
            Assert.That(timings.LastLoadSeconds, Is.EqualTo(2).Within(1e-9));
        }

        [Test]
        public void ReportsEveryExceededLimit()
        {
            var timings = new SaveTimings();
            timings.RecordSave(TimeSpan.FromSeconds(5), 100L * 1024 * 1024);
            timings.RecordLoad(TimeSpan.FromSeconds(60));

            var violations = timings.Violations(10_000);

            Assert.That(violations, Has.Length.EqualTo(3));
            Assert.That(violations[0], Does.StartWith("Opslaan"));
            Assert.That(violations[1], Does.StartWith("Laden"));
            Assert.That(violations[2], Does.StartWith("De save"));
        }

        [Test]
        public void BiggerCitiesGetBiggerBudgets()
        {
            var timings = new SaveTimings();
            timings.RecordSave(TimeSpan.FromSeconds(2.5), 100L * 1024 * 1024);
            timings.RecordLoad(TimeSpan.FromSeconds(40));

            Assert.That(timings.Violations(10_000), Is.Not.Empty);
            Assert.That(timings.Violations(1_000_000), Is.Empty);
        }

        [Test]
        public void ParallelCompressionKeepsSectionOrderAndContent()
        {
            var sections = new[]
            {
                new TestSection("AAAA", TestSection.Pattern(300000)),
                new TestSection("BBBB", new byte[] { 1, 2, 3 }),
                new TestSection("CCCC", TestSection.Pattern(150000)),
                new TestSection("DDDD", TestSection.Pattern(500000)),
            };
            var metadata = new SaveMetadata { CityName = "Parallel", SavedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };

            var first = SaveBytes.Write(metadata, sections);
            var second = SaveBytes.Write(metadata, sections);
            var loaded = new[]
            {
                new TestSection("AAAA", null), new TestSection("BBBB", null), new TestSection("CCCC", null), new TestSection("DDDD", null),
            };
            SaveBytes.Read(first, loaded);

            Assert.That(second, Is.EqualTo(first), "Zelfde invoer geeft byte-voor-byte dezelfde save");
            for (var i = 0; i < sections.Length; i++)
            {
                Assert.That(loaded[i].Applied, Is.EqualTo(sections[i].Payload));
            }
        }

        [Test]
        public void UncompressedWriteStillWorks()
        {
            var section = new TestSection("AAAA", TestSection.Pattern(2000));
            using (var stream = new System.IO.MemoryStream())
            {
                SaveFile.Write(stream, new SaveMetadata { SavedAtUtc = DateTime.UtcNow }, new ISaveSection[] { section, new TestSection("BBBB", new byte[3]) }, CompressionLevel.NoCompression);
                Assert.That(stream.Length, Is.GreaterThan(2000));
            }
        }
    }
}
