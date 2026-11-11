using System;
using Cities.Core.Persistence;

namespace Cities.Simulation.Disasters
{
    /// <summary>De hittegolf die nu loopt (E15-14), hooguit één tegelijk.</summary>
    public sealed class HeatwaveField
    {
        public Heatwave Current { get; private set; }

        public int NextId { get; private set; } = 1;

        public Heatwave Start(int strength, int durationDays, ulong seed)
        {
            Current = new Heatwave(NextId++, strength, durationDays, seed);
            return Current;
        }

        public void Clear() => Current = null;

        internal void Restore(int nextId, Heatwave current)
        {
            NextId = nextId;
            Current = current;
        }
    }

    /// <summary>Sectie <c>HEAT</c>: de lopende hittegolf met leeftijd.</summary>
    public sealed class HeatwaveSection : ISaveSection
    {
        public const string SectionId = "HEAT";

        private readonly HeatwaveField _field;

        public HeatwaveSection(HeatwaveField field)
        {
            _field = field ?? throw new ArgumentNullException(nameof(field));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_field.NextId);
            var wave = _field.Current;
            writer.Write(wave != null);
            if (wave == null)
            {
                return;
            }

            writer.Write(wave.Id);
            writer.Write(wave.Strength);
            writer.Write(wave.DurationDays);
            writer.Write(unchecked((long)wave.Seed));
            writer.Write(wave.AgeDays);
        }

        public Action Read(SaveReader reader, int version)
        {
            var nextId = reader.ReadInt32();
            if (nextId < 1)
            {
                throw reader.Corrupt($"onmogelijk volgnummer {nextId}");
            }

            Heatwave wave = null;
            if (reader.ReadBool())
            {
                var id = reader.ReadInt32();
                var strength = reader.ReadInt32();
                var duration = reader.ReadInt32();
                var seed = unchecked((ulong)reader.ReadInt64());
                var age = reader.ReadInt32();
                var valid = id >= 1 && id < nextId && strength >= 1 && strength <= Heatwave.MaxStrength && duration >= 1 && age >= 0;
                if (!valid)
                {
                    throw reader.Corrupt($"onmogelijke hittegolf {id}");
                }

                wave = new Heatwave(id, strength, duration, seed);
                wave.Restore(age);
            }

            reader.ExpectEnd();
            return () => _field.Restore(nextId, wave);
        }
    }
}
