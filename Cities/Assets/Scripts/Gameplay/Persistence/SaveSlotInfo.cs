using System;
using Cities.Core.Persistence;

namespace Cities.Gameplay.Persistence
{
    /// <summary>Eén regel in de lijst met saves: naam, soort, metadata en of er een miniatuur is.</summary>
    public sealed class SaveSlotInfo
    {
        public SaveSlotInfo(string name, SaveSlotKind kind, SaveMetadata metadata, bool hasThumbnail)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Kind = kind;
            Metadata = metadata;
            HasThumbnail = hasThumbnail;
        }

        public string Name { get; }

        public SaveSlotKind Kind { get; }

        /// <summary>Null als het bestand niet te lezen is; zo'n slot blijft zichtbaar zodat het te verwijderen is.</summary>
        public SaveMetadata Metadata { get; }

        public bool HasThumbnail { get; }

        public bool IsReadable => Metadata != null;
    }
}
