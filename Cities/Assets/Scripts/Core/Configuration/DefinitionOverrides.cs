using System.Collections.Generic;
using System.IO;

namespace Cities.Core.Configuration
{
    /// <summary>
    /// Leest JSON-overrides voor definities uit een map: één bestand per definitie, met het ID als bestandsnaam
    /// (<c>vehicle.city-bus.json</c>). Zo zijn waarden in een gebouwde versie aan te passen zonder code of editor.
    /// </summary>
    /// <remarks>
    /// De inhoud gebruikt dezelfde veldnamen als de asset, bijvoorbeeld
    /// <c>{ "_spec": { "MaxSpeedKmh": 100 } }</c>; zie <see cref="DefinitionCatalog.CreateDatabase"/>.
    /// </remarks>
    public static class DefinitionOverrides
    {
        public const string FileExtension = ".json";

        /// <summary>Alle overrides in een map, op ID. Een ontbrekende map geeft een lege set.</summary>
        public static Dictionary<string, string> ReadFolder(string folder, ICollection<string> errors)
        {
            var overrides = new Dictionary<string, string>(System.StringComparer.Ordinal);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return overrides;
            }

            var files = Directory.GetFiles(folder, "*" + FileExtension, SearchOption.TopDirectoryOnly);
            System.Array.Sort(files, System.StringComparer.Ordinal);
            foreach (var file in files)
            {
                var id = Path.GetFileNameWithoutExtension(file);
                if (!DefinitionId.IsValid(id))
                {
                    errors.Add($"Override '{Path.GetFileName(file)}': bestandsnaam is geen geldig definitie-ID.");
                    continue;
                }

                var json = File.ReadAllText(file);
                if (string.IsNullOrWhiteSpace(json))
                {
                    errors.Add($"Override '{Path.GetFileName(file)}': bestand is leeg.");
                    continue;
                }

                overrides.Add(id, json);
            }

            return overrides;
        }
    }
}
