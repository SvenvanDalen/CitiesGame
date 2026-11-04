using System;
using System.Collections.Generic;
using Cities.Core.Persistence;
using Unity.Mathematics;

namespace Cities.Simulation.Disasters
{
    /// <summary>Sectie <c>SINK</c>: de zinkgaten met hun leeftijd en wat ze al hebben opgeslokt.</summary>
    public sealed class SinkholeSection : ISaveSection
    {
        public const string SectionId = "SINK";

        private const int MaxLost = 100_000;

        private readonly SinkholeField _field;

        public SinkholeSection(SinkholeField field)
        {
            _field = field ?? throw new ArgumentNullException(nameof(field));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_field.NextId);
            writer.Write(_field.Holes.Count);
            foreach (var hole in _field.Holes)
            {
                writer.Write(hole.Id);
                writer.Write(hole.Center.x);
                writer.Write(hole.Center.y);
                writer.Write(hole.MaxRadius);
                writer.Write(hole.GrowthPerDay);
                writer.Write(hole.AgeDays);
                WriteIds(writer, hole.LostBuildings);
                WriteIds(writer, hole.LostSegments);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var nextId = reader.ReadInt32();
            var count = reader.ReadInt32();
            if (count < 0 || count > SinkholeField.MaxHoles || nextId < 1)
            {
                throw reader.Corrupt($"onmogelijke zinkgatstatus ({count} gaten)");
            }

            var holes = new List<Sinkhole>(count);
            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadInt32();
                var center = new float2(reader.ReadSingle(), reader.ReadSingle());
                var maxRadius = reader.ReadSingle();
                var growth = reader.ReadSingle();
                var age = reader.ReadInt32();
                if (id < 1 || id >= nextId || !(maxRadius > 0f) || !(growth > 0f) || age < 0)
                {
                    throw reader.Corrupt($"onmogelijk zinkgat {id}");
                }

                var buildings = ReadIds(reader);
                var segments = ReadIds(reader);
                holes.Add(SinkholeField.Create(id, center, maxRadius, growth, age, buildings, segments));
            }

            reader.ExpectEnd();
            return () => _field.Restore(nextId, holes);
        }

        private static void WriteIds(SaveWriter writer, IReadOnlyList<int> ids)
        {
            writer.Write(ids.Count);
            foreach (var id in ids)
            {
                writer.Write(id);
            }
        }

        private static List<int> ReadIds(SaveReader reader)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > MaxLost)
            {
                throw reader.Corrupt($"onmogelijk aantal verloren objecten ({count})");
            }

            var ids = new List<int>(count);
            for (var i = 0; i < count; i++)
            {
                ids.Add(reader.ReadInt32());
            }

            return ids;
        }
    }
}
