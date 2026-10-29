using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Cities.Core.Persistence
{
    /// <summary>
    /// Het savebestand: een kop, de metadata en daarna één blok per sectie met id, versie, lengtes, CRC-32 en de
    /// (met Deflate gecomprimeerde) inhoud. Laden leest en controleert eerst alles en past pas daarna toe, zodat een
    /// kapotte save het lopende spel nooit half overschrijft. Indeling: <c>docs/decisions/0003-save-formaat.md</c>.
    /// </summary>
    public static class SaveFile
    {
        /// <summary>"CTYS" in ASCII.</summary>
        public static readonly byte[] Magic = { (byte)'C', (byte)'T', (byte)'Y', (byte)'S' };

        /// <summary>Versie van de bestandsindeling (kop en sectieblokken), los van de versies van de secties.</summary>
        public const ushort FormatVersion = 1;

        public const string Extension = ".ctysave";

        /// <summary>Bovengrens per sectie (ongecomprimeerd) tegen onzinnige lengtes in een kapot bestand.</summary>
        public const int MaxSectionBytes = 1 << 30;

        private const byte StoredRaw = 0;
        private const byte StoredDeflate = 1;
        private const int MinBytesToCompress = 64;

        public static void Write(Stream output, SaveMetadata metadata, IReadOnlyList<ISaveSection> sections, CompressionLevel level = CompressionLevel.Fastest)
        {
            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (metadata is null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            ValidateSections(sections);
            using (var writer = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true))
            {
                writer.Write(Magic);
                writer.Write(FormatVersion);
                writer.Write((ushort)0);
                writer.Write(sections.Count + 1);

                var meta = new SaveWriter(256);
                metadata.Write(meta);
                WriteBlock(writer, SaveMetadata.SectionId, SaveMetadata.Version, meta.ToSegment(), CompressionLevel.NoCompression);

                foreach (var section in sections)
                {
                    var content = new SaveWriter(4096);
                    section.Write(content);
                    WriteBlock(writer, section.Id, section.Version, content.ToSegment(), level);
                }
            }
        }

        /// <summary>
        /// Leest een save en past de secties toe in de volgorde van <paramref name="sections"/>. Onbekende secties
        /// worden overgeslagen; secties die in de save ontbreken worden gemeld en niet aangeraakt.
        /// </summary>
        public static SaveLoadResult Read(Stream input, IReadOnlyList<ISaveSection> sections)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            ValidateSections(sections);
            var byId = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < sections.Count; i++)
            {
                byId.Add(sections[i].Id, i);
            }

            var apply = new Action[sections.Count];
            var skipped = new List<string>();
            SaveMetadata metadata;
            using (var reader = new BinaryReader(input, Encoding.ASCII, leaveOpen: true))
            {
                var count = ReadHeader(reader);
                metadata = ReadMetadataBlock(reader);
                for (var block = 1; block < count; block++)
                {
                    var entry = ReadBlock(reader);
                    if (!byId.TryGetValue(entry.Id, out var index))
                    {
                        skipped.Add(entry.Id);
                        continue;
                    }

                    if (apply[index] != null)
                    {
                        throw new SaveFormatException($"Sectie {entry.Id} staat twee keer in de save.");
                    }

                    var section = sections[index];
                    if (entry.Version > section.Version || entry.Version < 1)
                    {
                        throw new SaveFormatException(
                            $"Sectie {entry.Id} heeft versie {entry.Version}; deze versie van het spel leest tot en met {section.Version}.");
                    }

                    apply[index] = section.Read(new SaveReader(entry.Data, entry.Id), entry.Version)
                        ?? throw new InvalidOperationException($"Sectie {entry.Id} gaf geen toepassing terug.");
                }

                if (input.CanSeek && input.Position != input.Length)
                {
                    throw new SaveFormatException("Onverwachte gegevens na de laatste sectie.");
                }
            }

            var missing = new List<string>();
            for (var i = 0; i < sections.Count; i++)
            {
                if (apply[i] is null)
                {
                    missing.Add(sections[i].Id);
                }
                else
                {
                    apply[i]();
                }
            }

            return new SaveLoadResult(metadata, missing, skipped);
        }

        /// <summary>Leest alleen de kop en de metadata, voor lijsten van saves.</summary>
        public static SaveMetadata ReadMetadata(Stream input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            using (var reader = new BinaryReader(input, Encoding.ASCII, leaveOpen: true))
            {
                ReadHeader(reader);
                return ReadMetadataBlock(reader);
            }
        }

        /// <summary>Of een sectie-id geldig is: precies vier ASCII-letters of -cijfers.</summary>
        public static bool IsValidId(string id)
        {
            if (id is null || id.Length != 4)
            {
                return false;
            }

            foreach (var c in id)
            {
                var valid = (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
                if (!valid)
                {
                    return false;
                }
            }

            return true;
        }

        private static void ValidateSections(IReadOnlyList<ISaveSection> sections)
        {
            if (sections is null)
            {
                throw new ArgumentNullException(nameof(sections));
            }

            var ids = new HashSet<string>(StringComparer.Ordinal) { SaveMetadata.SectionId };
            foreach (var section in sections)
            {
                if (section is null)
                {
                    throw new ArgumentException("Lege sectie in de lijst.", nameof(sections));
                }

                if (!IsValidId(section.Id))
                {
                    throw new ArgumentException($"Ongeldige sectie-id '{section.Id}': vier hoofdletters of cijfers.", nameof(sections));
                }

                if (!ids.Add(section.Id))
                {
                    throw new ArgumentException($"Sectie-id '{section.Id}' komt meer dan eens voor of is gereserveerd.", nameof(sections));
                }

                if (section.Version < 1)
                {
                    throw new ArgumentException($"Sectie {section.Id} heeft versie {section.Version}; versies beginnen bij 1.", nameof(sections));
                }
            }
        }

        private static void WriteBlock(BinaryWriter writer, string id, int version, ArraySegment<byte> raw, CompressionLevel level)
        {
            if (raw.Count > MaxSectionBytes)
            {
                throw new InvalidOperationException($"Sectie {id} is {raw.Count} bytes; het maximum is {MaxSectionBytes}.");
            }

            var stored = raw;
            var storage = StoredRaw;
            if (level != CompressionLevel.NoCompression && raw.Count >= MinBytesToCompress)
            {
                var compressed = Compress(raw, level);
                if (compressed.Count < raw.Count)
                {
                    stored = compressed;
                    storage = StoredDeflate;
                }
            }

            writer.Write(Encoding.ASCII.GetBytes(id));
            writer.Write(version);
            writer.Write(storage);
            writer.Write(raw.Count);
            writer.Write(stored.Count);
            writer.Write(Crc32.Compute(raw.AsSpan()));
            writer.Write(stored.Array, stored.Offset, stored.Count);
        }

        private static ArraySegment<byte> Compress(ArraySegment<byte> raw, CompressionLevel level)
        {
            var output = new MemoryStream(raw.Count / 2 + 64);
            using (var deflate = new DeflateStream(output, level, leaveOpen: true))
            {
                deflate.Write(raw.Array, raw.Offset, raw.Count);
            }

            return new ArraySegment<byte>(output.GetBuffer(), 0, (int)output.Length);
        }

        private static int ReadHeader(BinaryReader reader)
        {
            var magic = ReadExactly(reader, Magic.Length);
            if (!magic.AsSpan().SequenceEqual(Magic))
            {
                throw new SaveFormatException("Dit is geen savebestand van Cities.");
            }

            var version = ReadOrThrow(reader, r => r.ReadUInt16());
            if (version > FormatVersion)
            {
                throw new SaveFormatException($"De save gebruikt bestandsindeling {version}; dit spel kent tot en met {FormatVersion}. Werk het spel bij.");
            }

            if (version == 0)
            {
                throw new SaveFormatException("Ongeldige versie van de bestandsindeling.");
            }

            ReadOrThrow(reader, r => r.ReadUInt16());
            var count = ReadOrThrow(reader, r => r.ReadInt32());
            if (count < 1 || count > 4096)
            {
                throw new SaveFormatException($"Ongeldig aantal secties ({count}).");
            }

            return count;
        }

        private static SaveMetadata ReadMetadataBlock(BinaryReader reader)
        {
            var entry = ReadBlock(reader);
            if (entry.Id != SaveMetadata.SectionId)
            {
                throw new SaveFormatException($"De save begint met sectie {entry.Id} in plaats van {SaveMetadata.SectionId}.");
            }

            return SaveMetadata.Read(new SaveReader(entry.Data, entry.Id), entry.Version);
        }

        private static Block ReadBlock(BinaryReader reader)
        {
            var id = Encoding.ASCII.GetString(ReadExactly(reader, 4));
            if (!IsValidId(id))
            {
                throw new SaveFormatException("Ongeldige sectiekop; de save is beschadigd.");
            }

            var version = ReadOrThrow(reader, r => r.ReadInt32());
            var storage = ReadOrThrow(reader, r => r.ReadByte());
            var rawLength = ReadOrThrow(reader, r => r.ReadInt32());
            var storedLength = ReadOrThrow(reader, r => r.ReadInt32());
            var crc = ReadOrThrow(reader, r => r.ReadUInt32());
            if (rawLength < 0 || rawLength > MaxSectionBytes || storedLength < 0 || storedLength > MaxSectionBytes)
            {
                throw new SaveFormatException($"Sectie {id} heeft een ongeldige lengte.");
            }

            var stored = ReadExactly(reader, storedLength);
            byte[] data;
            switch (storage)
            {
                case StoredRaw when storedLength == rawLength:
                    data = stored;
                    break;
                case StoredDeflate:
                    data = Decompress(stored, rawLength, id);
                    break;
                default:
                    throw new SaveFormatException($"Sectie {id} gebruikt een onbekende opslag ({storage}).");
            }

            if (Crc32.Compute(data) != crc)
            {
                throw new SaveFormatException($"Sectie {id} is beschadigd (controlegetal klopt niet).");
            }

            return new Block(id, version, data);
        }

        private static byte[] Decompress(byte[] stored, int rawLength, string id)
        {
            var data = new byte[rawLength];
            try
            {
                using (var deflate = new DeflateStream(new MemoryStream(stored), CompressionMode.Decompress))
                {
                    var read = 0;
                    while (read < rawLength)
                    {
                        var n = deflate.Read(data, read, rawLength - read);
                        if (n == 0)
                        {
                            throw new SaveFormatException($"Sectie {id} is korter dan opgegeven.");
                        }

                        read += n;
                    }

                    if (deflate.ReadByte() != -1)
                    {
                        throw new SaveFormatException($"Sectie {id} is langer dan opgegeven.");
                    }
                }
            }
            catch (InvalidDataException exception)
            {
                throw new SaveFormatException($"Sectie {id} is beschadigd (compressie).", exception);
            }

            return data;
        }

        private static byte[] ReadExactly(BinaryReader reader, int count)
        {
            var bytes = reader.ReadBytes(count);
            if (bytes.Length != count)
            {
                throw new SaveFormatException("De save is afgebroken (bestand te kort).");
            }

            return bytes;
        }

        private static T ReadOrThrow<T>(BinaryReader reader, Func<BinaryReader, T> read)
        {
            try
            {
                return read(reader);
            }
            catch (EndOfStreamException exception)
            {
                throw new SaveFormatException("De save is afgebroken (bestand te kort).", exception);
            }
        }

        private readonly struct Block
        {
            public Block(string id, int version, byte[] data)
            {
                Id = id;
                Version = version;
                Data = data;
            }

            public string Id { get; }

            public int Version { get; }

            public byte[] Data { get; }
        }
    }

    /// <summary>Uitkomst van <see cref="SaveFile.Read"/>.</summary>
    public sealed class SaveLoadResult
    {
        public SaveLoadResult(SaveMetadata metadata, IReadOnlyList<string> missingSections, IReadOnlyList<string> skippedSections)
        {
            Metadata = metadata;
            MissingSections = missingSections;
            SkippedSections = skippedSections;
        }

        public SaveMetadata Metadata { get; }

        /// <summary>Secties van het spel die niet in de save staan (bijvoorbeeld een systeem dat nieuwer is dan de save).</summary>
        public IReadOnlyList<string> MissingSections { get; }

        /// <summary>Secties in de save die dit spel niet kent; ze worden genegeerd.</summary>
        public IReadOnlyList<string> SkippedSections { get; }
    }
}
