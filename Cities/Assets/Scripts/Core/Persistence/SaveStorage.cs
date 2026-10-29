using System;
using System.Collections.Generic;
using System.IO;

namespace Cities.Core.Persistence
{
    /// <summary>
    /// Saves als bestanden in één map (in het spel <c>Application.persistentDataPath/Saves</c>). Schrijven gaat naar een
    /// tijdelijk bestand dat pas na succes het oude vervangt; een crash of volle schijf laat de vorige save heel.
    /// </summary>
    public sealed class SaveStorage
    {
        public const int MaxNameLength = 64;

        private const string TemporarySuffix = ".tmp";

        public SaveStorage(string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("Map is leeg.", nameof(directory));
            }

            Directory = directory;
        }

        public string Directory { get; }

        public string PathFor(string name) => Path.Combine(Directory, ValidateName(name) + SaveFile.Extension);

        public bool Exists(string name) => File.Exists(PathFor(name));

        /// <summary>Schrijft een save onder <paramref name="name"/>; <paramref name="write"/> schrijft de inhoud.</summary>
        public void Write(string name, Action<Stream> write)
        {
            if (write is null)
            {
                throw new ArgumentNullException(nameof(write));
            }

            var path = PathFor(name);
            System.IO.Directory.CreateDirectory(Directory);
            var temporary = path + TemporarySuffix;
            try
            {
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
                {
                    write(stream);
                    stream.Flush(flushToDisk: true);
                }

                if (File.Exists(path))
                {
                    File.Replace(temporary, path, null);
                }
                else
                {
                    File.Move(temporary, path);
                }
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        public Stream OpenRead(string name) =>
            new FileStream(PathFor(name), FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);

        public void Delete(string name)
        {
            var path = PathFor(name);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        /// <summary>Namen van alle saves, nieuwste eerst.</summary>
        public IReadOnlyList<string> List()
        {
            if (!System.IO.Directory.Exists(Directory))
            {
                return Array.Empty<string>();
            }

            var files = new DirectoryInfo(Directory).GetFiles("*" + SaveFile.Extension);
            Array.Sort(files, (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            var names = new List<string>(files.Length);
            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file.Name);
                if (IsValidName(name))
                {
                    names.Add(name);
                }
            }

            return names;
        }

        /// <summary>Letters, cijfers, spaties, '-', '_' en haakjes; geen spatie aan begin of eind, maximaal 64 tekens.</summary>
        public static bool IsValidName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength || name[0] == ' ' || name[name.Length - 1] == ' ')
            {
                return false;
            }

            foreach (var c in name)
            {
                var valid = char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_' || c == '(' || c == ')';
                if (!valid)
                {
                    return false;
                }
            }

            return true;
        }

        private static string ValidateName(string name) =>
            IsValidName(name) ? name : throw new ArgumentException($"Ongeldige naam voor een save: '{name}'.", nameof(name));
    }
}
