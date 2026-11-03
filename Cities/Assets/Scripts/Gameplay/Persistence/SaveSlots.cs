using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Cities.Core.Persistence;

namespace Cities.Gameplay.Persistence
{
    /// <summary>
    /// Saveslots bovenop <see cref="SaveGameService"/>: handmatige slots, één quicksave en een draaiend aantal
    /// autosaves (<c>Autosave 1</c> tot en met <c>Autosave N</c>, steeds de oudste wordt overschreven). Elke save
    /// krijgt een miniatuur naast het bestand.
    /// </summary>
    public sealed class SaveSlots
    {
        public const string QuicksaveName = "Quicksave";
        public const string AutosavePrefix = "Autosave ";
        public const int DefaultAutosaveCount = 3;
        public const int MaxAutosaveCount = 10;

        private readonly SaveGameService _service;
        private readonly SaveStorage _storage;
        private int _autosaveCount = DefaultAutosaveCount;

        public SaveSlots(SaveGameService service, SaveStorage storage)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        /// <summary>Hoeveel autosaves er rouleren, tussen 1 en 10.</summary>
        public int AutosaveCount
        {
            get => _autosaveCount;
            set => _autosaveCount = Math.Max(1, Math.Min(MaxAutosaveCount, value));
        }

        public bool HasQuicksave => _storage.Exists(QuicksaveName);

        public static string AutosaveName(int index) => AutosavePrefix + index.ToString(CultureInfo.InvariantCulture);

        public static SaveSlotKind KindOf(string name)
        {
            if (name == QuicksaveName)
            {
                return SaveSlotKind.Quicksave;
            }

            return IsAutosaveName(name) ? SaveSlotKind.Autosave : SaveSlotKind.Manual;
        }

        /// <summary>Handmatige slots mogen geen naam van een autosave of quicksave krijgen.</summary>
        public static bool IsReservedName(string name) => KindOf(name) != SaveSlotKind.Manual;

        /// <summary>Alle saves, nieuwste eerst; onleesbare bestanden staan er ook in, zonder metadata.</summary>
        public IReadOnlyList<SaveSlotInfo> List()
        {
            var names = _storage.List();
            var slots = new List<SaveSlotInfo>(names.Count);
            foreach (var name in names)
            {
                _service.TryReadMetadata(name, out var metadata);
                slots.Add(new SaveSlotInfo(name, KindOf(name), metadata, File.Exists(_storage.ThumbnailPathFor(name))));
            }

            return slots;
        }

        public SaveMetadata SaveManual(string name, byte[] thumbnail = null)
        {
            if (IsReservedName(name))
            {
                throw new ArgumentException($"De naam '{name}' is voor automatisch opslaan gereserveerd.", nameof(name));
            }

            return Write(name, thumbnail);
        }

        public SaveMetadata Quicksave(byte[] thumbnail = null) => Write(QuicksaveName, thumbnail);

        public SaveLoadResult LoadQuicksave() => _service.Load(QuicksaveName);

        public SaveLoadResult Load(string name) => _service.Load(name);

        /// <summary>Schrijft de volgende autosave en geeft de gebruikte naam terug.</summary>
        public string Autosave(byte[] thumbnail = null)
        {
            var name = NextAutosaveName();
            Write(name, thumbnail);
            return name;
        }

        /// <summary>Eerste vrije autosaveslot; als alle bezet zijn het slot met de oudste save (onleesbaar telt als oudste).</summary>
        public string NextAutosaveName()
        {
            string oldest = null;
            var oldestTime = DateTime.MaxValue;
            for (var i = 1; i <= _autosaveCount; i++)
            {
                var name = AutosaveName(i);
                if (!_storage.Exists(name))
                {
                    return name;
                }

                var time = _service.TryReadMetadata(name, out var metadata) ? metadata.SavedAtUtc : DateTime.MinValue;
                if (oldest == null || time < oldestTime)
                {
                    oldest = name;
                    oldestTime = time;
                }
            }

            return oldest;
        }

        public void Delete(string name) => _storage.Delete(name);

        public bool TryReadThumbnail(string name, out byte[] png) => _storage.TryReadThumbnail(name, out png);

        private SaveMetadata Write(string name, byte[] thumbnail)
        {
            var metadata = _service.Save(name);
            _storage.WriteThumbnail(name, thumbnail);
            return metadata;
        }

        private static bool IsAutosaveName(string name)
        {
            if (name == null || !name.StartsWith(AutosavePrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var number = name.Substring(AutosavePrefix.Length);
            return int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                && index >= 1
                && index <= MaxAutosaveCount
                && number == index.ToString(CultureInfo.InvariantCulture);
        }
    }
}
