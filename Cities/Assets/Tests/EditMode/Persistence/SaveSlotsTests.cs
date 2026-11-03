using System;
using System.IO;
using Cities.Core.Persistence;
using Cities.Gameplay.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Persistence
{
    public class SaveSlotsTests
    {
        private string _directory;
        private SaveStorage _storage;
        private DateTime _now;
        private TestSection _section;
        private SaveSlots _slots;
        private RecordingLogSink _log;

        [SetUp]
        public void SetUp()
        {
            _log = new RecordingLogSink();
            _directory = SaveBytes.TempDirectory();
            _storage = new SaveStorage(_directory);
            _now = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
            _section = new TestSection("CLCK", new byte[] { 1, 2, 3 });
            var service = new SaveGameService(_storage, new ISaveSection[] { _section }, m => m.Tick = 5, null, () => _now);
            _slots = new SaveSlots(service, _storage);
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
        public void ClassifiesNames()
        {
            Assert.That(SaveSlots.KindOf("Quicksave"), Is.EqualTo(SaveSlotKind.Quicksave));
            Assert.That(SaveSlots.KindOf("Autosave 2"), Is.EqualTo(SaveSlotKind.Autosave));
            Assert.That(SaveSlots.KindOf("Autosave 10"), Is.EqualTo(SaveSlotKind.Autosave));
            Assert.That(SaveSlots.KindOf("Autosave 02"), Is.EqualTo(SaveSlotKind.Manual));
            Assert.That(SaveSlots.KindOf("Autosave 0"), Is.EqualTo(SaveSlotKind.Manual));
            Assert.That(SaveSlots.KindOf("Autosave 11"), Is.EqualTo(SaveSlotKind.Manual));
            Assert.That(SaveSlots.KindOf("Autosave x"), Is.EqualTo(SaveSlotKind.Manual));
            Assert.That(SaveSlots.KindOf("Havenstad"), Is.EqualTo(SaveSlotKind.Manual));
            Assert.That(SaveSlots.KindOf(null), Is.EqualTo(SaveSlotKind.Manual));
            Assert.That(SaveSlots.IsReservedName("Quicksave"), Is.True);
            Assert.That(SaveSlots.IsReservedName("Havenstad"), Is.False);
            Assert.That(SaveSlots.AutosaveName(3), Is.EqualTo("Autosave 3"));
        }

        [Test]
        public void AutosavesRotateAndOverwriteTheOldest()
        {
            _slots.AutosaveCount = 3;
            var used = new System.Collections.Generic.List<string>();
            for (var i = 0; i < 5; i++)
            {
                used.Add(_slots.Autosave());
                _now = _now.AddMinutes(10);
            }

            Assert.That(used, Is.EqualTo(new[] { "Autosave 1", "Autosave 2", "Autosave 3", "Autosave 1", "Autosave 2" }));
            Assert.That(_slots.NextAutosaveName(), Is.EqualTo("Autosave 3"));
            Assert.That(_storage.List().Count, Is.EqualTo(3));
        }

        [Test]
        public void AUnreadableAutosaveIsReplacedFirst()
        {
            _slots.AutosaveCount = 2;
            _slots.Autosave();
            _now = _now.AddMinutes(1);
            _slots.Autosave();
            File.WriteAllText(_storage.PathFor("Autosave 2"), "kapot");

            Assert.That(_slots.NextAutosaveName(), Is.EqualTo("Autosave 2"));
        }

        [Test]
        public void AutosaveCountIsClamped()
        {
            _slots.AutosaveCount = 0;
            Assert.That(_slots.AutosaveCount, Is.EqualTo(1));
            _slots.AutosaveCount = 99;
            Assert.That(_slots.AutosaveCount, Is.EqualTo(SaveSlots.MaxAutosaveCount));
        }

        [Test]
        public void QuicksaveOverwritesAndLoads()
        {
            Assert.That(_slots.HasQuicksave, Is.False);

            _slots.Quicksave(new byte[] { 7 });
            _section.Payload = new byte[] { 9 };
            _now = _now.AddMinutes(1);
            var metadata = _slots.Quicksave();
            var result = _slots.LoadQuicksave();

            Assert.That(_slots.HasQuicksave, Is.True);
            Assert.That(result.Metadata.SavedAtUtc, Is.EqualTo(metadata.SavedAtUtc));
            Assert.That(_section.Applied, Is.EqualTo(new byte[] { 9 }));
            Assert.That(_slots.TryReadThumbnail("Quicksave", out _), Is.False, "Zonder miniatuur verdwijnt de oude");
        }

        [Test]
        public void ManualSlotsKeepTheirThumbnailAndRejectReservedNames()
        {
            _slots.SaveManual("Havenstad", new byte[] { 1, 2 });

            Assert.That(_slots.TryReadThumbnail("Havenstad", out var png), Is.True);
            Assert.That(png, Is.EqualTo(new byte[] { 1, 2 }));
            Assert.Throws<ArgumentException>(() => _slots.SaveManual("Quicksave"));
            Assert.Throws<ArgumentException>(() => _slots.SaveManual("Autosave 1"));
            Assert.That(_slots.Load("Havenstad").Metadata.Tick, Is.EqualTo(5));
        }

        [Test]
        public void ListShowsKindMetadataThumbnailAndBrokenFiles()
        {
            _slots.SaveManual("Havenstad", new byte[] { 1 });
            _now = _now.AddMinutes(1);
            _slots.Autosave();
            _slots.Quicksave();
            File.WriteAllText(_storage.PathFor("Kapot"), "geen save");
            File.SetLastWriteTimeUtc(_storage.PathFor("Havenstad"), DateTime.UtcNow.AddHours(-2));
            File.SetLastWriteTimeUtc(_storage.PathFor("Kapot"), DateTime.UtcNow.AddHours(-1));

            var list = _slots.List();
            var byName = new System.Collections.Generic.Dictionary<string, SaveSlotInfo>();
            foreach (var slot in list)
            {
                byName[slot.Name] = slot;
            }

            Assert.That(list.Count, Is.EqualTo(4));
            Assert.That(byName["Havenstad"].Kind, Is.EqualTo(SaveSlotKind.Manual));
            Assert.That(byName["Havenstad"].HasThumbnail, Is.True);
            Assert.That(byName["Havenstad"].IsReadable, Is.True);
            Assert.That(byName["Autosave 1"].Kind, Is.EqualTo(SaveSlotKind.Autosave));
            Assert.That(byName["Autosave 1"].HasThumbnail, Is.False);
            Assert.That(byName["Quicksave"].Kind, Is.EqualTo(SaveSlotKind.Quicksave));
            Assert.That(byName["Kapot"].IsReadable, Is.False);
            Assert.That(byName["Kapot"].Metadata, Is.Null);
        }

        [Test]
        public void DeleteRemovesSaveAndThumbnail()
        {
            _slots.SaveManual("Havenstad", new byte[] { 1 });

            _slots.Delete("Havenstad");

            Assert.That(_slots.List(), Is.Empty);
            Assert.That(File.Exists(_storage.ThumbnailPathFor("Havenstad")), Is.False);
        }

        [Test]
        public void ArgumentsAreChecked()
        {
            var service = new SaveGameService(_storage, Array.Empty<ISaveSection>(), _ => { });
            Assert.Throws<ArgumentNullException>(() => new SaveSlots(null, _storage));
            Assert.Throws<ArgumentNullException>(() => new SaveSlots(service, null));
            Assert.Throws<ArgumentNullException>(() => new SaveSlotInfo(null, SaveSlotKind.Manual, null, false));
        }
    }
}
