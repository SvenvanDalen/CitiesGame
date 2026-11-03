using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;

namespace Cities.Simulation.Resources
{
    /// <summary>Sectie <c>RSRC</c>: de afzettingen op de resource-laag en de winningsgebouwen (E14-01).</summary>
    public sealed class ResourceSection : ISaveSection
    {
        public const string SectionId = "RSRC";

        private readonly ResourceExtraction _extraction;

        public ResourceSection(ResourceExtraction extraction)
        {
            _extraction = extraction ?? throw new ArgumentNullException(nameof(extraction));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            var deposits = _extraction.Deposits;
            writer.Write(deposits.Count);
            foreach (var pair in deposits.All)
            {
                writer.Write(ResourceDeposits.KeyX(pair.Key));
                writer.Write(ResourceDeposits.KeyZ(pair.Key));
                writer.Write((int)pair.Value.Kind);
                writer.Write(pair.Value.Capacity);
                writer.Write(pair.Value.Remaining);
            }

            writer.Write(_extraction.NextId);
            var extractors = _extraction.Extractors.ToArray();
            writer.Write(extractors.Length);
            foreach (var e in extractors)
            {
                writer.Write(e.Id);
                writer.Write((int)e.Kind);
                writer.Write(e.RatePerCell);
                writer.Write(e.Depletes);
                writer.Write(e.Cells.Count);
                foreach (var (x, z) in e.Cells)
                {
                    writer.Write(x);
                    writer.Write(z);
                }
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var depositCount = reader.ReadInt32();
            if (depositCount < 0 || depositCount > ResourceDeposits.MaxDeposits)
            {
                throw reader.Corrupt($"onmogelijk aantal afzettingen: {depositCount}");
            }

            var deposits = new List<(long Key, Deposit Deposit)>();
            for (var i = 0; i < depositCount; i++)
            {
                var x = reader.ReadInt32();
                var z = reader.ReadInt32();
                var kind = ReadKind(reader);
                var capacity = reader.ReadSingle();
                var remaining = reader.ReadSingle();
                if (!(capacity > 0f) || !(remaining >= 0f && remaining <= capacity))
                {
                    throw reader.Corrupt("ongeldige hoeveelheid in een afzetting");
                }

                deposits.Add((ResourceDeposits.Key(x, z), ResourceDeposits.Create(kind, capacity, remaining)));
            }

            if (deposits.Select(d => d.Key).Distinct().Count() != depositCount)
            {
                throw reader.Corrupt("een cel heeft twee afzettingen");
            }

            var nextId = reader.ReadInt32();
            var extractorCount = reader.ReadInt32();
            if (extractorCount < 0 || extractorCount > ResourceExtraction.MaxExtractors)
            {
                throw reader.Corrupt($"onmogelijk aantal winningsgebouwen: {extractorCount}");
            }

            var extractors = new List<Extractor>();
            for (var i = 0; i < extractorCount; i++)
            {
                extractors.Add(ReadExtractor(reader, nextId));
            }

            reader.ExpectEnd();
            if (extractors.Select(e => e.Id).Distinct().Count() != extractorCount)
            {
                throw reader.Corrupt("dubbel winningsgebouw-ID");
            }

            return () =>
            {
                _extraction.Deposits.Clear();
                foreach (var (key, deposit) in deposits)
                {
                    _extraction.Deposits.Restore(key, deposit);
                }

                _extraction.Restore(extractors, nextId);
            };
        }

        private static ResourceKind ReadKind(SaveReader reader)
        {
            var kind = (ResourceKind)reader.ReadInt32();
            return ResourceKinds.IsValid(kind) ? kind : throw reader.Corrupt("onbekende soort");
        }

        private static Extractor ReadExtractor(SaveReader reader, int nextId)
        {
            var id = reader.ReadInt32();
            var kind = ReadKind(reader);
            var rate = reader.ReadSingle();
            var depletes = reader.ReadBool();
            var cellCount = reader.ReadInt32();
            if (id < 1 || id >= nextId || !(rate > 0f) || cellCount < 1 || cellCount > ResourceExtraction.MaxCells)
            {
                throw reader.Corrupt("ongeldig winningsgebouw");
            }

            var cells = new (int X, int Z)[cellCount];
            for (var c = 0; c < cellCount; c++)
            {
                cells[c] = (reader.ReadInt32(), reader.ReadInt32());
            }

            return ResourceExtraction.Create(id, kind, cells, rate, depletes);
        }
    }
}
