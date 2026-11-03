using System;
using System.Collections.Generic;
using System.IO;
using Cities.Core.Diagnostics;

namespace Cities.Core.Persistence
{
    /// <summary>Uitkomst van een controle van één save, zonder iets in het spel toe te passen.</summary>
    public sealed class SaveIntegrity
    {
        public SaveIntegrity(string name, bool isValid, string problem, IReadOnlyList<string> sections)
        {
            Name = name;
            IsValid = isValid;
            Problem = problem;
            Sections = sections;
        }

        public string Name { get; }

        public bool IsValid { get; }

        /// <summary>Wat er mis is (Nederlands, voor het scherm en het log); leeg als de save goed is.</summary>
        public string Problem { get; }

        /// <summary>Id's van de secties die zijn gecontroleerd, metadata eerst.</summary>
        public IReadOnlyList<string> Sections { get; }
    }

    /// <summary>
    /// Herkent kapotte saves aan de checksum van elke sectie, de lengtes en de structuur van het bestand, zodat de
    /// lijst van saves ze kan markeren en het laden niet halverwege mislukt. Elk gevonden probleem komt in het log.
    /// </summary>
    public static class SaveVerifier
    {
        private static readonly SystemLog Log = new SystemLog("Save");

        public static SaveIntegrity Check(string name, Stream input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            try
            {
                var sections = SaveFile.ReadSectionIds(input);
                return new SaveIntegrity(name, true, string.Empty, sections);
            }
            catch (SaveFormatException exception)
            {
                Log.Warning("Save {0} is beschadigd.", $"{name}: {exception.Message}");
                return new SaveIntegrity(name, false, exception.Message, Array.Empty<string>());
            }
        }

        /// <summary>Controleert één save in de opslag; een bestand dat niet te lezen is telt als beschadigd.</summary>
        public static SaveIntegrity Check(SaveStorage storage, string name)
        {
            if (storage is null)
            {
                throw new ArgumentNullException(nameof(storage));
            }

            try
            {
                using (var stream = storage.OpenRead(name))
                {
                    return Check(name, stream);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Log.Warning("Save {0} is niet te lezen.", $"{name}: {exception.Message}");
                return new SaveIntegrity(name, false, "Het bestand is niet te lezen.", Array.Empty<string>());
            }
        }

        /// <summary>Controleert alle saves, nieuwste eerst.</summary>
        public static IReadOnlyList<SaveIntegrity> CheckAll(SaveStorage storage)
        {
            if (storage is null)
            {
                throw new ArgumentNullException(nameof(storage));
            }

            var results = new List<SaveIntegrity>();
            foreach (var name in storage.List())
            {
                results.Add(Check(storage, name));
            }

            return results;
        }
    }
}
