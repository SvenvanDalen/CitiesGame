using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cities.Core.Diagnostics;
using UnityEngine;

namespace Cities.Core.Configuration
{
    /// <summary>
    /// De data-map van E21-09: gebouwen, beleid, wegtypes en voertuigen (dus ook kosten) aanpassen met tekstbestanden,
    /// zonder nieuwe build. In <c>persistentDataPath/Data</c> zet je per definitie een <c>&lt;id&gt;.json</c> met alleen
    /// de velden die je wilt wijzigen; <see cref="ExportDefaults"/> schrijft alle huidige waarden als voorbeeld naar
    /// <c>Data/Defaults</c>. Overrides werken op een kopie; de assets blijven ongewijzigd.
    /// </summary>
    public static class GameDataFolder
    {
        public const string FolderName = "Data";
        public const string DefaultsFolderName = "Defaults";
        public const string ReadmeFileName = "LEESMIJ.txt";

        private static readonly SystemLog Log = new SystemLog("Data");
        private static readonly Dictionary<DefinitionCatalog, DefinitionDatabase> Cache = new Dictionary<DefinitionCatalog, DefinitionDatabase>();

        /// <summary>De map met overrides, onder de gegeven gebruikersmap (in het spel <c>Application.persistentDataPath</c>).</summary>
        public static string FolderUnder(string persistentDataPath) => Path.Combine(persistentDataPath, FolderName);

        /// <summary>
        /// De definities van <paramref name="catalog"/> met de overrides uit de data-map, één keer per catalogus opgebouwd.
        /// Problemen met bestanden worden gelogd en genegeerd; het spel start altijd met de standaardwaarden als terugval.
        /// </summary>
        public static DefinitionDatabase Load(DefinitionCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (Cache.TryGetValue(catalog, out var cached))
            {
                return cached;
            }

            var errors = new List<string>();
            var folder = FolderUnder(Application.persistentDataPath);
            var database = catalog.CreateDatabase(DefinitionOverrides.ReadFolder(folder, errors), errors);
            foreach (var error in errors)
            {
                Log.Warning("Data-map: {0}", error);
            }

            foreach (var error in database.Errors)
            {
                Log.Warning("Data-map: ongeldige waarde: {0}", error);
            }

            Cache[catalog] = database;
            return database;
        }

        /// <summary>Vergeet de opgebouwde databases, zodat de volgende <see cref="Load"/> de map opnieuw leest.</summary>
        public static void Reload() => Cache.Clear();

        /// <summary>
        /// Maakt de map met een uitleg en schrijft de standaardwaarden van elke definitie als JSON naar <c>Defaults</c>,
        /// zodat je een bestand kunt kopiëren naar de map erboven en aanpassen. Geeft het aantal geschreven bestanden.
        /// </summary>
        public static int ExportDefaults(DefinitionCatalog catalog, string folder)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var defaults = Path.Combine(folder, DefaultsFolderName);
            Directory.CreateDirectory(defaults);
            File.WriteAllText(Path.Combine(folder, ReadmeFileName), Readme(), new UTF8Encoding(false));
            var written = 0;
            foreach (var definition in catalog.Definitions)
            {
                if (definition == null || !DefinitionId.IsValid(definition.Id))
                {
                    continue;
                }

                File.WriteAllText(Path.Combine(defaults, definition.Id + DefinitionOverrides.FileExtension), JsonUtility.ToJson(definition, true), new UTF8Encoding(false));
                written++;
            }

            return written;
        }

        /// <summary>De uitleg die naast de overrides komt te staan.</summary>
        public static string Readme() =>
            "Hier pas je spelgegevens aan zonder nieuwe versie van het spel.\r\n\r\n" +
            "1. Kopieer een bestand uit de map Defaults naar deze map (bijvoorbeeld building.huis-laag.json).\r\n" +
            "2. Verwijder wat je niet wilt wijzigen; laat alleen de velden staan die anders moeten.\r\n" +
            "3. Wijzig de waarden (bijvoorbeeld ConstructionCost) en start het spel opnieuw.\r\n\r\n" +
            "De bestandsnaam is het ID van de definitie. Het ID zelf kun je niet wijzigen. Fouten in een bestand staan in het logboek " +
            "en dat bestand wordt dan overgeslagen; het spel gebruikt dan de standaardwaarden.\r\n";
    }
}
