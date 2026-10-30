using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Cities.Core.Randomness;

namespace Cities.Core.Persistence
{
    /// <summary>Checksum van één sectie.</summary>
    public readonly struct SectionChecksum
    {
        public SectionChecksum(string id, ulong hash, long length)
        {
            Id = id;
            Hash = hash;
            Length = length;
        }

        public string Id { get; }

        public ulong Hash { get; }

        public long Length { get; }

        public override string ToString() => $"{Id} {Hash:X16} ({Length} bytes)";
    }

    /// <summary>
    /// Vingerafdruk van de wereldstatus via dezelfde secties als de save. Twee runs met dezelfde seed en commands horen
    /// per tick dezelfde checksums te geven; de eerste afwijkende sectie wijst het systeem aan dat uiteenloopt.
    /// </summary>
    public static class StateChecksum
    {
        /// <summary>Schrijft elke sectie en hasht de bytes; de volgorde van <paramref name="sections"/> blijft behouden.</summary>
        public static IReadOnlyList<SectionChecksum> Compute(IEnumerable<ISaveSection> sections)
        {
            if (sections == null)
            {
                throw new ArgumentNullException(nameof(sections));
            }

            return sections.Select(Compute).ToList();
        }

        public static SectionChecksum Compute(ISaveSection section)
        {
            if (section == null)
            {
                throw new ArgumentNullException(nameof(section));
            }

            var writer = new SaveWriter(4096);
            section.Write(writer);
            var bytes = writer.ToSegment();
            return new SectionChecksum(section.Id, Hash(bytes), bytes.Count);
        }

        /// <summary>Eén waarde voor alle secties samen.</summary>
        public static ulong Combine(IEnumerable<SectionChecksum> checksums)
        {
            if (checksums == null)
            {
                throw new ArgumentNullException(nameof(checksums));
            }

            return checksums.Aggregate(0UL, (total, checksum) => SeedHash.Combine(total, SeedHash.Of(checksum.Id), checksum.Hash));
        }

        /// <summary>Id van de eerste sectie die verschilt (ontbrekend telt als verschil), of <c>null</c> als alles gelijk is.</summary>
        public static string FirstDifference(IReadOnlyList<SectionChecksum> expected, IReadOnlyList<SectionChecksum> actual)
        {
            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            if (actual == null)
            {
                throw new ArgumentNullException(nameof(actual));
            }

            var count = Math.Max(expected.Count, actual.Count);
            for (var i = 0; i < count; i++)
            {
                if (i >= expected.Count)
                {
                    return actual[i].Id;
                }

                if (i >= actual.Count || expected[i].Id != actual[i].Id || expected[i].Hash != actual[i].Hash || expected[i].Length != actual[i].Length)
                {
                    return expected[i].Id;
                }
            }

            return null;
        }

        /// <summary>Per 8 bytes gemengd met SplitMix64, de rest per byte; snel genoeg voor de volledige kaart.</summary>
        internal static ulong Hash(ReadOnlySpan<byte> data)
        {
            var hash = SeedHash.Mix((ulong)data.Length);
            var words = MemoryMarshal.Cast<byte, ulong>(data);
            foreach (var word in words)
            {
                hash = SeedHash.Mix(hash ^ word);
            }

            for (var i = words.Length * sizeof(ulong); i < data.Length; i++)
            {
                hash = SeedHash.Mix(hash ^ data[i]);
            }

            return hash;
        }
    }
}
