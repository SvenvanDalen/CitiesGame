using System;
using System.Collections.Generic;
using Cities.Core.Persistence;

namespace Cities.Simulation.Disasters
{
    /// <summary>Sectie <c>DSET</c>: de rampinstellingen van de speler (E15-13), globaal en per ramptype.</summary>
    public sealed class DisasterSettingsSection : ISaveSection
    {
        public const string SectionId = "DSET";

        private const int MaxTypes = 1000;

        private readonly DisasterSettings _settings;

        public DisasterSettingsSection(DisasterSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_settings.RandomEnabled);
            var names = new List<string>(_settings.Customised);
            writer.Write(names.Count);
            foreach (var name in names)
            {
                writer.Write(name);
                writer.Write(_settings.IsEnabled(name));
                writer.Write(_settings.FrequencyFactor(name));
                writer.Write(_settings.DamageFactor(name));
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var random = reader.ReadBool();
            var count = reader.ReadInt32();
            if (count < 0 || count > MaxTypes)
            {
                throw reader.Corrupt($"onmogelijk aantal ramptypes {count}");
            }

            var types = new List<(string, bool, float, float)>(count);
            for (var i = 0; i < count; i++)
            {
                var name = reader.ReadString();
                var enabled = reader.ReadBool();
                var frequency = reader.ReadSingle();
                var damage = reader.ReadSingle();
                var valid = !string.IsNullOrWhiteSpace(name)
                    && frequency >= 0f && frequency <= DisasterSettings.MaxFactor
                    && damage >= 0f && damage <= DisasterSettings.MaxFactor;
                if (!valid)
                {
                    throw reader.Corrupt($"onmogelijke rampinstelling '{name}'");
                }

                types.Add((name, enabled, frequency, damage));
            }

            reader.ExpectEnd();
            return () => _settings.Restore(random, types);
        }
    }
}
