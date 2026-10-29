using System;
using System.IO;

namespace Cities.Core.Settings
{
    /// <summary>Bewaart instellingen van de speler als tekst onder een naam, bijvoorbeeld <c>controls</c>.</summary>
    public interface ISettingsStore
    {
        bool TryRead(string name, out string contents);

        void Write(string name, string contents);

        void Delete(string name);
    }

    /// <summary>
    /// Eén JSON-bestand per naam in een map (in het spel <c>Application.persistentDataPath/Settings</c>). Schrijven gaat
    /// via een tijdelijk bestand dat daarna het oude vervangt, zodat een crash tijdens het opslaan geen half bestand
    /// achterlaat.
    /// </summary>
    public sealed class FileSettingsStore : ISettingsStore
    {
        private const string Extension = ".json";

        public FileSettingsStore(string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("Map is leeg.", nameof(directory));
            }

            Directory = directory;
        }

        public string Directory { get; }

        public string PathFor(string name) => Path.Combine(Directory, ValidateName(name) + Extension);

        public bool TryRead(string name, out string contents)
        {
            var path = PathFor(name);
            if (!File.Exists(path))
            {
                contents = null;
                return false;
            }

            contents = File.ReadAllText(path);
            return true;
        }

        public void Write(string name, string contents)
        {
            var path = PathFor(name);
            System.IO.Directory.CreateDirectory(Directory);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, contents ?? string.Empty);
            if (File.Exists(path))
            {
                File.Replace(temporary, path, null);
            }
            else
            {
                File.Move(temporary, path);
            }
        }

        public void Delete(string name)
        {
            var path = PathFor(name);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        /// <summary>Alleen kleine letters, cijfers, '-' en '_', zodat een naam nooit buiten de map kan wijzen.</summary>
        public static string ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Naam is leeg.", nameof(name));
            }

            foreach (var c in name)
            {
                var valid = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
                if (!valid)
                {
                    throw new ArgumentException($"Ongeldig teken '{c}' in instellingsnaam '{name}'.", nameof(name));
                }
            }

            return name;
        }
    }
}
