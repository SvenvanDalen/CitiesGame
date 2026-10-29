using System;
using System.IO;
using Cities.Core.Diagnostics;
using Cities.Core.Persistence;
using Cities.Gameplay.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Persistence
{
    public class SaveGameServiceTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 29, 21, 0, 0, DateTimeKind.Utc);

        private string _directory;
        private SaveStorage _storage;
        private RecordingLogSink _log;

        [SetUp]
        public void SetUp()
        {
            _directory = SaveBytes.TempDirectory();
            _storage = new SaveStorage(_directory);
            _log = new RecordingLogSink();
        }

        [TearDown]
        public void TearDown()
        {
            _log.Dispose();
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [Test]
        public void SaveAndLoadRoundTripWithMetadataAndPlayTime()
        {
            var section = new TestSection("CLCK", new byte[] { 4, 5 });
            var synchronized = 0;
            var service = Service(section, () => synchronized++);
            service.CityName = "Havenstad";
            service.AddPlayTime(90);
            service.AddPlayTime(-5);
            service.AddPlayTime(double.PositiveInfinity);

            var saved = service.Save("Slot 1");

            Assert.That(saved.SavedAtUtc, Is.EqualTo(Now));
            Assert.That(saved.Tick, Is.EqualTo(777));
            Assert.That(saved.PlayTimeSeconds, Is.EqualTo(90));
            Assert.That(service.List(), Is.EqualTo(new[] { "Slot 1" }));

            var loadedSection = new TestSection("CLCK", null);
            var other = Service(loadedSection, () => synchronized++);
            var result = other.Load("Slot 1");

            Assert.That(loadedSection.Applied, Is.EqualTo(new byte[] { 4, 5 }));
            Assert.That(other.CityName, Is.EqualTo("Havenstad"));
            Assert.That(other.PlayTimeSeconds, Is.EqualTo(90));
            Assert.That(result.Metadata.GameVersion, Is.EqualTo("0.1.0"));
            Assert.That(synchronized, Is.EqualTo(2), "Jobs afgerond voor opslaan en laden");
        }

        [Test]
        public void MissingAndUnknownSectionsAreLogged()
        {
            Service(new TestSection("OLD1", new byte[] { 1 })).Save("Oud");

            var result = Service(new TestSection("ROAD", null)).Load("Oud");

            Assert.That(result.MissingSections, Is.EqualTo(new[] { "ROAD" }));
            Assert.That(_log.Lines.Exists(l => l.Level == LogLevel.Warning && l.Message.Contains("ROAD")), Is.True);
            Assert.That(_log.Lines.Exists(l => l.Level == LogLevel.Info && l.Message.Contains("OLD1")), Is.True);
        }

        [Test]
        public void MetadataOfValidAndBrokenSaves()
        {
            var service = Service(new TestSection("CLCK", new byte[] { 1 }));
            service.Save("Goed");
            File.WriteAllText(_storage.PathFor("Kapot"), "dit is geen save");

            Assert.That(service.TryReadMetadata("Goed", out var metadata), Is.True);
            Assert.That(metadata.Tick, Is.EqualTo(777));
            Assert.That(service.TryReadMetadata("Kapot", out var broken), Is.False);
            Assert.That(broken, Is.Null);
            Assert.That(service.TryReadMetadata("Bestaat niet", out _), Is.False);
        }

        [Test]
        public void BrokenSaveLeavesTheCityAlone()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_storage.PathFor("Kapot"), "dit is geen save");
            var section = new TestSection("CLCK", null);
            var service = Service(section);
            service.CityName = "Huidige stad";

            Assert.Throws<SaveFormatException>(() => service.Load("Kapot"));
            Assert.That(section.Applied, Is.Null);
            Assert.That(service.CityName, Is.EqualTo("Huidige stad"));
        }

        [Test]
        public void ArgumentsAreChecked()
        {
            var sections = new ISaveSection[0];
            Assert.Throws<ArgumentNullException>(() => new SaveGameService(null, sections, _ => { }));
            Assert.Throws<ArgumentNullException>(() => new SaveGameService(_storage, null, _ => { }));
            Assert.Throws<ArgumentNullException>(() => new SaveGameService(_storage, sections, null));
            Assert.That(new SaveGameService(_storage, sections, _ => { }).Save("Standaard").SavedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
        }

        private SaveGameService Service(TestSection section, Action synchronize = null) =>
            new SaveGameService(_storage, new ISaveSection[] { section }, m => m.Tick = 777, synchronize, () => Now)
            {
                GameVersion = "0.1.0",
                Compression = System.IO.Compression.CompressionLevel.Fastest,
            };
    }
}
