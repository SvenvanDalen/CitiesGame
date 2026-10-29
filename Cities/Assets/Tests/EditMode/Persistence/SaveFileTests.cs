using System;
using System.IO;
using System.IO.Compression;
using Cities.Core.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Persistence
{
    public class SaveFileTests
    {
        private static readonly DateTime SavedAt = new DateTime(2026, 10, 29, 20, 15, 0, DateTimeKind.Utc);

        [Test]
        public void SectionsAndMetadataRoundTrip()
        {
            var large = new TestSection("WMAP", TestSection.Pattern(200000));
            var small = new TestSection("CLCK", new byte[] { 1, 2, 3 });

            var bytes = SaveBytes.Write(Metadata(), large, small);
            var loadedLarge = new TestSection("WMAP", null);
            var loadedSmall = new TestSection("CLCK", null);
            var result = SaveBytes.Read(bytes, loadedLarge, loadedSmall);

            Assert.That(loadedLarge.Applied, Is.EqualTo(large.Payload));
            Assert.That(loadedSmall.Applied, Is.EqualTo(small.Payload));
            Assert.That(result.MissingSections, Is.Empty);
            Assert.That(result.SkippedSections, Is.Empty);
            AssertMetadata(result.Metadata);
            Assert.That(bytes.Length, Is.LessThan(large.Payload.Length / 4), "Deflate comprimeert de grote sectie");
        }

        [Test]
        public void MetadataCanBeReadAlone()
        {
            var bytes = SaveBytes.Write(Metadata(), new TestSection("WMAP", TestSection.Pattern(1000)));

            using (var stream = new MemoryStream(bytes))
            {
                AssertMetadata(SaveFile.ReadMetadata(stream));
            }
        }

        [Test]
        public void UncompressedSavesAreReadable()
        {
            var section = new TestSection("WMAP", TestSection.Pattern(5000));
            byte[] bytes;
            using (var stream = new MemoryStream())
            {
                SaveFile.Write(stream, Metadata(), new ISaveSection[] { section }, CompressionLevel.NoCompression);
                bytes = stream.ToArray();
            }

            var loaded = new TestSection("WMAP", null);
            SaveBytes.Read(bytes, loaded);

            Assert.That(bytes.Length, Is.GreaterThan(5000));
            Assert.That(loaded.Applied, Is.EqualTo(section.Payload));
        }

        [Test]
        public void UnknownSectionsAreSkippedAndMissingOnesReported()
        {
            var bytes = SaveBytes.Write(Metadata(), new TestSection("OLD1", new byte[] { 9 }), new TestSection("CLCK", new byte[] { 1 }));
            var clock = new TestSection("CLCK", null);
            var roads = new TestSection("ROAD", null);

            var result = SaveBytes.Read(bytes, clock, roads);

            Assert.That(result.SkippedSections, Is.EqualTo(new[] { "OLD1" }));
            Assert.That(result.MissingSections, Is.EqualTo(new[] { "ROAD" }));
            Assert.That(clock.Applied, Is.EqualTo(new byte[] { 1 }));
            Assert.That(roads.Applied, Is.Null);
        }

        [Test]
        public void OlderSectionVersionIsPassedToTheReader()
        {
            var bytes = SaveBytes.Write(Metadata(), new TestSection("CLCK", new byte[] { 1 }, version: 1));
            var newer = new TestSection("CLCK", null, version: 3);

            SaveBytes.Read(bytes, newer);

            Assert.That(newer.ReadVersion, Is.EqualTo(1));
        }

        [Test]
        public void NewerSectionVersionIsRejected()
        {
            var bytes = SaveBytes.Write(Metadata(), new TestSection("CLCK", new byte[] { 1 }, version: 2));

            var error = Assert.Throws<SaveFormatException>(() => SaveBytes.Read(bytes, new TestSection("CLCK", null, version: 1)));
            Assert.That(error.Message, Does.Contain("versie 2"));
        }

        [Test]
        public void CorruptByteIsDetectedAndNothingIsApplied()
        {
            var first = new TestSection("AAAA", new byte[] { 1, 2, 3 });
            var second = new TestSection("BBBB", TestSection.Pattern(1000));
            var bytes = SaveBytes.Write(Metadata(), first, second);
            bytes[bytes.Length - 10] ^= 0x5A;

            var loadedFirst = new TestSection("AAAA", null);
            Assert.Throws<SaveFormatException>(() => SaveBytes.Read(bytes, loadedFirst, new TestSection("BBBB", null)));
            Assert.That(loadedFirst.Applied, Is.Null, "De goede eerste sectie is niet toegepast");
        }

        [Test]
        public void CorruptUncompressedSectionFailsTheChecksum()
        {
            var bytes = SaveBytes.Write(Metadata(), new TestSection("CLCK", new byte[] { 1, 2, 3 }));
            bytes[bytes.Length - 1] ^= 0x01;

            var error = Assert.Throws<SaveFormatException>(() => SaveBytes.Read(bytes, new TestSection("CLCK", null)));
            Assert.That(error.Message, Does.Contain("controlegetal"));
        }

        [TestCase(3)]
        [TestCase(10)]
        [TestCase(40)]
        public void TruncatedFilesAreRejected(int keep)
        {
            var bytes = SaveBytes.Write(Metadata(), new TestSection("CLCK", TestSection.Pattern(500)));

            Assert.Throws<SaveFormatException>(() => SaveBytes.Read(bytes.AsSpan(0, Math.Min(keep, bytes.Length)).ToArray(), new TestSection("CLCK", null)));
        }

        [Test]
        public void SectionCutOffAtTheEndIsRejected()
        {
            var bytes = SaveBytes.Write(Metadata(), new TestSection("CLCK", TestSection.Pattern(500)));

            Assert.Throws<SaveFormatException>(() => SaveBytes.Read(bytes.AsSpan(0, bytes.Length - 5).ToArray(), new TestSection("CLCK", null)));
        }

        [Test]
        public void OtherFilesAndNewerFormatsAreRejected()
        {
            var png = new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0, 0, 0, 0, 0, 0, 0, 0 };
            Assert.That(Assert.Throws<SaveFormatException>(() => SaveBytes.Read(png)).Message, Does.Contain("geen savebestand"));

            var bytes = SaveBytes.Write(Metadata());
            bytes[4] = 2;
            Assert.That(Assert.Throws<SaveFormatException>(() => SaveBytes.Read(bytes)).Message, Does.Contain("Werk het spel bij"));

            bytes[4] = 0;
            Assert.Throws<SaveFormatException>(() => SaveBytes.Read(bytes));
        }

        [Test]
        public void TrailingDataIsRejected()
        {
            var bytes = SaveBytes.Write(Metadata());
            var longer = new byte[bytes.Length + 3];
            bytes.CopyTo(longer, 0);

            Assert.Throws<SaveFormatException>(() => SaveBytes.Read(longer));
        }

        [Test]
        public void InvalidSectionCountIsRejected()
        {
            var bytes = SaveBytes.Write(Metadata());
            BitConverter.GetBytes(0).CopyTo(bytes, 8);

            Assert.Throws<SaveFormatException>(() => SaveBytes.Read(bytes));
        }

        [Test]
        public void DuplicateSectionInTheFileIsRejected()
        {
            var bytes = SaveBytes.Write(Metadata(), new TestSection("AAAA", new byte[] { 1 }), new TestSection("BBBB", new byte[] { 2 }));
            var idOfSecond = IndexOf(bytes, "BBBB");
            bytes[idOfSecond] = (byte)'A';
            bytes[idOfSecond + 1] = (byte)'A';
            bytes[idOfSecond + 2] = (byte)'A';
            bytes[idOfSecond + 3] = (byte)'A';

            Assert.That(Assert.Throws<SaveFormatException>(() => SaveBytes.Read(bytes, new TestSection("AAAA", null))).Message, Does.Contain("twee keer"));
        }

        [TestCase("abcd")]
        [TestCase("TOOLONG")]
        [TestCase("AB")]
        [TestCase("META")]
        [TestCase("A-BC")]
        [TestCase(null)]
        public void InvalidOrReservedIdsAreRefused(string id)
        {
            Assert.Throws<ArgumentException>(() => SaveBytes.Write(Metadata(), new TestSection(id, new byte[0])));
        }

        [Test]
        public void ArgumentsAreChecked()
        {
            Assert.Throws<ArgumentException>(() => SaveBytes.Write(Metadata(), new TestSection("AAAA", new byte[0]), new TestSection("AAAA", new byte[0])));
            Assert.Throws<ArgumentException>(() => SaveBytes.Write(Metadata(), new TestSection("AAAA", new byte[0], version: 0)));
            Assert.Throws<ArgumentException>(() => SaveBytes.Write(Metadata(), new ISaveSection[] { null }));
            Assert.Throws<ArgumentNullException>(() => SaveFile.Write(null, Metadata(), Array.Empty<ISaveSection>()));
            Assert.Throws<ArgumentNullException>(() => SaveFile.Write(new MemoryStream(), null, Array.Empty<ISaveSection>()));
            Assert.Throws<ArgumentNullException>(() => SaveFile.Write(new MemoryStream(), Metadata(), null));
            Assert.Throws<ArgumentNullException>(() => SaveFile.Read(null, Array.Empty<ISaveSection>()));
            Assert.Throws<ArgumentNullException>(() => SaveFile.ReadMetadata(null));
        }

        [Test]
        public void ReaderMustReturnAnApplyAction()
        {
            var bytes = SaveBytes.Write(Metadata(), new TestSection("AAAA", new byte[] { 1 }));

            Assert.Throws<InvalidOperationException>(() => SaveBytes.Read(bytes, new NullSection()));
        }

        // Een sectiekop is: id (4), versie (4), opslag (1), lengte (4), opgeslagen lengte (4), CRC (4).
        [TestCase(8, 7, "onbekende opslag", TestName = "Onbekende opslag")]
        [TestCase(8, 1, "Sectie CLCK", TestName = "Ruwe inhoud als Deflate gemarkeerd")]
        [TestCase(9, 0xFF, "ongeldige lengte", TestName = "Negatieve lengte")]
        public void DamagedSectionHeaderIsRejected(int offset, int value, string message)
        {
            var bytes = SaveBytes.Write(Metadata(), new TestSection("CLCK", new byte[] { 1, 2, 3 }));
            var header = IndexOf(bytes, "CLCK");
            bytes[header + offset] = (byte)value;
            if (offset == 9)
            {
                bytes[header + 12] = 0xFF;
            }

            var error = Assert.Throws<SaveFormatException>(() => SaveBytes.Read(bytes, new TestSection("CLCK", null)));
            Assert.That(error.Message, Does.Contain(message));
        }

        [Test]
        public void CompressedSectionMarkedAsRawIsRejected()
        {
            var bytes = SaveBytes.Write(Metadata(), new TestSection("WMAP", TestSection.Pattern(5000)));
            bytes[IndexOf(bytes, "WMAP") + 8] = 0;

            Assert.Throws<SaveFormatException>(() => SaveBytes.Read(bytes, new TestSection("WMAP", null)));
        }

        [Test]
        public void UnknownMetadataVersionIsRejected()
        {
            var bytes = SaveBytes.Write(Metadata());
            bytes[IndexOf(bytes, "META") + 4] = 2;

            Assert.Throws<SaveFormatException>(() => SaveBytes.Read(bytes));
        }

        [Test]
        public void SaveMustStartWithMetadata()
        {
            var bytes = SaveBytes.Write(Metadata());
            var meta = IndexOf(bytes, "META");
            bytes[meta] = (byte)'X';

            Assert.That(Assert.Throws<SaveFormatException>(() => SaveBytes.Read(bytes)).Message, Does.Contain("begint met"));
        }

        [Test]
        public void SectionIdRules()
        {
            Assert.That(SaveFile.IsValidId("WMAP"), Is.True);
            Assert.That(SaveFile.IsValidId("R2D2"), Is.True);
            Assert.That(SaveFile.IsValidId("wmap"), Is.False);
        }

        private static SaveMetadata Metadata() => new SaveMetadata
        {
            CityName = "Havenstad",
            GameVersion = "0.1.0",
            SavedAtUtc = SavedAt,
            Tick = 123456,
            Year = 2003,
            Month = 7,
            Day = 14,
            PlayTimeSeconds = 5400.5,
        };

        private static void AssertMetadata(SaveMetadata metadata)
        {
            Assert.That(metadata.CityName, Is.EqualTo("Havenstad"));
            Assert.That(metadata.GameVersion, Is.EqualTo("0.1.0"));
            Assert.That(metadata.SavedAtUtc, Is.EqualTo(SavedAt));
            Assert.That(metadata.SavedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(metadata.Tick, Is.EqualTo(123456));
            Assert.That((metadata.Year, metadata.Month, metadata.Day), Is.EqualTo((2003, 7, 14)));
            Assert.That(metadata.PlayTimeSeconds, Is.EqualTo(5400.5));
        }

        private static int IndexOf(byte[] bytes, string ascii)
        {
            var pattern = System.Text.Encoding.ASCII.GetBytes(ascii);
            for (var i = 0; i + pattern.Length <= bytes.Length; i++)
            {
                if (bytes.AsSpan(i, pattern.Length).SequenceEqual(pattern))
                {
                    return i;
                }
            }

            throw new InvalidOperationException(ascii + " niet gevonden");
        }

        private sealed class NullSection : ISaveSection
        {
            public string Id => "AAAA";

            public int Version => 1;

            public void Write(SaveWriter writer)
            {
                // Schrijft niets; alleen lezen wordt getest.
            }

            public Action Read(SaveReader reader, int version) => null;
        }
    }
}
