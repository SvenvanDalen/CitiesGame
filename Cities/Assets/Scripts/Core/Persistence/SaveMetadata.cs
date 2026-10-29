using System;

namespace Cities.Core.Persistence
{
    /// <summary>
    /// Gegevens voor het laadscherm en de save-slots, vooraan en ongecomprimeerd in het bestand zodat ze zonder de hele
    /// wereld te lezen zijn.
    /// </summary>
    public sealed class SaveMetadata
    {
        public const string SectionId = "META";
        public const int Version = 1;

        public string CityName { get; set; } = string.Empty;

        /// <summary>Versie van het spel dat de save schreef (<c>Application.version</c>).</summary>
        public string GameVersion { get; set; } = string.Empty;

        public DateTime SavedAtUtc { get; set; }

        public long Tick { get; set; }

        public int Year { get; set; }

        public int Month { get; set; }

        public int Day { get; set; }

        /// <summary>Echte speeltijd in seconden over alle sessies.</summary>
        public double PlayTimeSeconds { get; set; }

        internal void Write(SaveWriter writer)
        {
            writer.Write(CityName);
            writer.Write(GameVersion);
            writer.Write(SavedAtUtc.ToUniversalTime().Ticks);
            writer.Write(Tick);
            writer.Write(Year);
            writer.Write(Month);
            writer.Write(Day);
            writer.Write(PlayTimeSeconds);
        }

        internal static SaveMetadata Read(SaveReader reader, int version)
        {
            if (version != Version)
            {
                throw reader.Corrupt($"onbekende versie {version}");
            }

            var metadata = new SaveMetadata { CityName = reader.ReadString() };
            metadata.GameVersion = reader.ReadString();
            var ticks = reader.ReadInt64();
            if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
            {
                throw reader.Corrupt("ongeldige datum");
            }

            metadata.SavedAtUtc = new DateTime(ticks, DateTimeKind.Utc);
            metadata.Tick = reader.ReadInt64();
            metadata.Year = reader.ReadInt32();
            metadata.Month = reader.ReadInt32();
            metadata.Day = reader.ReadInt32();
            metadata.PlayTimeSeconds = reader.ReadDouble();
            reader.ExpectEnd();
            return metadata;
        }
    }
}
