using System;
using System.IO;
using Cities.Core.Diagnostics;
using Cities.Core.Persistence;
using Cities.Gameplay.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Persistence
{
    public class SaveVerifierTests
    {
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
        public void GoodSaveListsItsSections()
        {
            var bytes = SaveBytes.Write(Metadata(), new TestSection("CLCK", new byte[] { 1, 2 }), new TestSection("WMAP", TestSection.Pattern(500)));

            var result = Check(bytes);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Problem, Is.Empty);
            Assert.That(result.Name, Is.EqualTo("Test"));
            Assert.That(result.Sections, Is.EqualTo(new[] { "META", "CLCK", "WMAP" }));
            Assert.That(_log.Lines, Is.Empty);
        }

        [Test]
        public void FlippedByteFailsTheChecksumAndIsLogged()
        {
            var bytes = SaveBytes.Write(Metadata(), new TestSection("CLCK", new byte[] { 1, 2, 3 }));
            bytes[bytes.Length - 1] ^= 0x01;

            var result = Check(bytes);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Problem, Does.Contain("controlegetal"));
            Assert.That(result.Sections, Is.Empty);
            Assert.That(_log.Lines.Exists(l => l.Level == LogLevel.Warning && l.Message.Contains("Test")), Is.True);
        }

        [Test]
        public void TruncatedAndForeignFilesAreBroken()
        {
            var bytes = SaveBytes.Write(Metadata(), new TestSection("CLCK", new byte[] { 1, 2, 3 }));

            Assert.That(Check(bytes.AsSpan(0, bytes.Length - 4).ToArray()).IsValid, Is.False);
            Assert.That(Check(new byte[] { 1, 2, 3 }).Problem, Is.Not.Empty);
            Assert.That(Check(System.Text.Encoding.ASCII.GetBytes("geen save")).Problem, Does.Contain("geen savebestand"));
        }

        [Test]
        public void TrailingGarbageIsBroken()
        {
            var bytes = SaveBytes.Write(Metadata(), new TestSection("CLCK", new byte[] { 1 }));
            var longer = new byte[bytes.Length + 3];
            Array.Copy(bytes, longer, bytes.Length);

            Assert.That(Check(longer).Problem, Does.Contain("Onverwachte"));
        }

        [Test]
        public void SaveWithoutMetadataFirstIsBroken()
        {
            using (var stream = new MemoryStream())
            {
                var writer = new BinaryWriter(stream);
                writer.Write(SaveFile.Magic);
                writer.Write(SaveFile.FormatVersion);
                writer.Write((ushort)0);
                writer.Write(1);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("CLCK"));
                writer.Write(1);
                writer.Write((byte)0);
                writer.Write(0);
                writer.Write(0);
                writer.Write(Crc32.Compute(Array.Empty<byte>()));
                writer.Flush();

                Assert.That(Check(stream.ToArray()).Problem, Does.Contain("META"));
            }
        }

        [Test]
        public void StorageChecksOneAndAllSavesAndMarksBrokenOnes()
        {
            var service = new SaveGameService(_storage, new ISaveSection[] { new TestSection("CLCK", new byte[] { 1 }) }, m => { });
            service.Save("Goed");
            File.WriteAllText(_storage.PathFor("Kapot"), "dit is geen save");
            File.SetLastWriteTimeUtc(_storage.PathFor("Goed"), DateTime.UtcNow.AddHours(-1));

            var all = service.VerifyAll();

            Assert.That(all, Has.Count.EqualTo(2));
            Assert.That(all[0].Name, Is.EqualTo("Kapot"));
            Assert.That(all[0].IsValid, Is.False);
            Assert.That(all[1].IsValid, Is.True);
            Assert.That(service.Verify("Goed").IsValid, Is.True);
            Assert.That(SaveVerifier.CheckAll(new SaveStorage(Path.Combine(_directory, "bestaat-niet"))), Is.Empty);
        }

        [Test]
        public void MissingFileIsReportedAsUnreadable()
        {
            var result = SaveVerifier.Check(_storage, "Bestaat niet");

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Problem, Does.Contain("niet te lezen"));
        }

        [Test]
        public void FailedLoadIsLoggedAsError()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_storage.PathFor("Kapot"), "dit is geen save");
            var service = new SaveGameService(_storage, new ISaveSection[] { new TestSection("CLCK", null) }, m => { });

            Assert.Throws<SaveFormatException>(() => service.Load("Kapot"));
            Assert.Throws<FileNotFoundException>(() => service.Load("Bestaat niet"));

            Assert.That(_log.Lines.FindAll(l => l.Level == LogLevel.Error), Has.Count.EqualTo(2));
        }

        [Test]
        public void ArgumentsAreChecked()
        {
            Assert.Throws<ArgumentNullException>(() => SaveVerifier.Check("x", null));
            Assert.Throws<ArgumentNullException>(() => SaveVerifier.Check(null, "x"));
            Assert.Throws<ArgumentNullException>(() => SaveVerifier.CheckAll(null));
            Assert.Throws<ArgumentNullException>(() => SaveFile.ReadSectionIds(null));
        }

        private static SaveIntegrity Check(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return SaveVerifier.Check("Test", stream);
            }
        }

        private static SaveMetadata Metadata() => new SaveMetadata
        {
            CityName = "Havenstad",
            GameVersion = "0.1.0",
            SavedAtUtc = new DateTime(2026, 10, 29, 21, 0, 0, DateTimeKind.Utc),
        };
    }
}
