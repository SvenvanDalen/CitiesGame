using System;
using System.Collections.Generic;
using Cities.Core.Persistence;
using Unity.Mathematics;

namespace Cities.Simulation.Disasters
{
    /// <summary>De storm die nu over de stad trekt (E15-08), hooguit één tegelijk; bewaart hem voor de save.</summary>
    public sealed class StormField
    {
        public Storm Current { get; private set; }

        public int NextId { get; private set; } = 1;

        /// <summary>Laat een nieuwe storm opsteken; een lopende storm wordt vervangen.</summary>
        public Storm Start(float2 center, float radius, int strength, ulong seed)
        {
            Current = new Storm(NextId++, center, radius, strength, seed);
            return Current;
        }

        /// <summary>De storm trekt weg.</summary>
        public void Clear() => Current = null;

        internal void Restore(int nextId, Storm current)
        {
            NextId = nextId;
            Current = current;
        }
    }

    /// <summary>Sectie <c>STRM</c>: de lopende storm met leeftijd en uitgevallen lijnen.</summary>
    public sealed class StormSection : ISaveSection
    {
        public const string SectionId = "STRM";

        private const int MaxLines = 100_000;

        private readonly StormField _field;

        public StormSection(StormField field)
        {
            _field = field ?? throw new ArgumentNullException(nameof(field));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_field.NextId);
            var storm = _field.Current;
            writer.Write(storm != null);
            if (storm == null)
            {
                return;
            }

            writer.Write(storm.Id);
            writer.Write(storm.Center.x);
            writer.Write(storm.Center.y);
            writer.Write(storm.Radius);
            writer.Write(storm.Strength);
            writer.Write(unchecked((long)storm.Seed));
            writer.Write(storm.AgeDays);
            writer.Write(storm.LinesDown.Count);
            foreach (var id in storm.LinesDown)
            {
                writer.Write(id);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var nextId = reader.ReadInt32();
            if (nextId < 1)
            {
                throw reader.Corrupt($"onmogelijk volgnummer {nextId}");
            }

            Storm storm = null;
            if (reader.ReadBool())
            {
                storm = ReadStorm(reader, nextId);
            }

            reader.ExpectEnd();
            return () => _field.Restore(nextId, storm);
        }

        private static Storm ReadStorm(SaveReader reader, int nextId)
        {
            var id = reader.ReadInt32();
            var center = new float2(reader.ReadSingle(), reader.ReadSingle());
            var radius = reader.ReadSingle();
            var strength = reader.ReadInt32();
            var seed = unchecked((ulong)reader.ReadInt64());
            var age = reader.ReadInt32();
            var count = reader.ReadInt32();
            var valid = id >= 1 && id < nextId && radius > 0f && strength >= 1 && strength <= Storm.MaxStrength && age >= 0 && count >= 0 && count <= MaxLines;
            if (!valid)
            {
                throw reader.Corrupt($"onmogelijke storm {id}");
            }

            var lines = new List<int>(count);
            for (var i = 0; i < count; i++)
            {
                lines.Add(reader.ReadInt32());
            }

            var storm = new Storm(id, center, radius, strength, seed);
            storm.Restore(age, lines);
            return storm;
        }
    }
}
