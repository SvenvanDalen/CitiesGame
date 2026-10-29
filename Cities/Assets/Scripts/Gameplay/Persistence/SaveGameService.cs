using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Cities.Core.Diagnostics;
using Cities.Core.Persistence;

namespace Cities.Gameplay.Persistence
{
    /// <summary>
    /// Slaat de stad op en laadt hem: verzamelt de secties van alle systemen, houdt de speeltijd bij en vult de
    /// metadata. Opslaan en laden gebeuren tussen twee ticks; <c>synchronize</c> wacht op lopende simulatiejobs.
    /// </summary>
    public sealed class SaveGameService
    {
        private static readonly SystemLog Log = new SystemLog("Save");

        private readonly SaveStorage _storage;
        private readonly IReadOnlyList<ISaveSection> _sections;
        private readonly Action<SaveMetadata> _describe;
        private readonly Action _synchronize;
        private readonly Func<DateTime> _utcNow;

        public SaveGameService(
            SaveStorage storage,
            IReadOnlyList<ISaveSection> sections,
            Action<SaveMetadata> describe,
            Action synchronize = null,
            Func<DateTime> utcNow = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _sections = sections ?? throw new ArgumentNullException(nameof(sections));
            _describe = describe ?? throw new ArgumentNullException(nameof(describe));
            _synchronize = synchronize ?? (() => { });
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        /// <summary>Naam van de stad in nieuwe saves.</summary>
        public string CityName { get; set; } = "Nieuwe stad";

        /// <summary>Wordt in de metadata gezet (in het spel <c>Application.version</c>).</summary>
        public string GameVersion { get; set; } = string.Empty;

        /// <summary>
        /// Fastest is de standaard: met de byte-vlakken van <see cref="SaveWriter.WriteShuffled{T}"/> is Optimal op de
        /// kaartlagen even klein maar een derde trager.
        /// </summary>
        public CompressionLevel Compression { get; set; } = CompressionLevel.Fastest;

        /// <summary>Speeltijd van deze stad in seconden, ook over eerdere sessies.</summary>
        public double PlayTimeSeconds { get; private set; }

        public IReadOnlyList<string> List() => _storage.List();

        public void AddPlayTime(double seconds)
        {
            if (seconds > 0d && !double.IsInfinity(seconds))
            {
                PlayTimeSeconds += seconds;
            }
        }

        public SaveMetadata Save(string name)
        {
            _synchronize();
            var metadata = new SaveMetadata
            {
                CityName = CityName,
                GameVersion = GameVersion,
                SavedAtUtc = _utcNow(),
                PlayTimeSeconds = PlayTimeSeconds,
            };
            _describe(metadata);
            _storage.Write(name, stream => SaveFile.Write(stream, metadata, _sections, Compression));
            Log.Info("Opgeslagen als {0}.", name);
            return metadata;
        }

        /// <summary>
        /// Laadt een save. Bij een <see cref="SaveFormatException"/> of <see cref="IOException"/> is de huidige stad
        /// ongewijzigd.
        /// </summary>
        public SaveLoadResult Load(string name)
        {
            _synchronize();
            SaveLoadResult result;
            using (var stream = _storage.OpenRead(name))
            {
                result = SaveFile.Read(stream, _sections);
            }

            CityName = result.Metadata.CityName;
            PlayTimeSeconds = result.Metadata.PlayTimeSeconds;
            if (result.MissingSections.Count > 0)
            {
                Log.Warning("Save {0} mist secties; die onderdelen blijven zoals ze waren.", $"{name}: {string.Join(", ", result.MissingSections)}");
            }

            if (result.SkippedSections.Count > 0)
            {
                Log.Info("Save {0} bevat onbekende secties; die worden genegeerd.", $"{name}: {string.Join(", ", result.SkippedSections)}");
            }

            Log.Info("Geladen: {0}.", name);
            return result;
        }

        /// <summary>Leest alleen de metadata; false als het bestand ontbreekt of geen geldige save is.</summary>
        public bool TryReadMetadata(string name, out SaveMetadata metadata)
        {
            try
            {
                using (var stream = _storage.OpenRead(name))
                {
                    metadata = SaveFile.ReadMetadata(stream);
                    return true;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is SaveFormatException || exception is UnauthorizedAccessException)
            {
                metadata = null;
                return false;
            }
        }
    }
}
