using System;
using System.Collections.Generic;

namespace Cities.Core.Diagnostics
{
    public enum LogLevel : byte
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warning = 3,
        Error = 4,

        /// <summary>Niets loggen.</summary>
        None = 5,
    }

    /// <summary>
    /// Het logniveau per categorie (meestal een systeemnaam), met een standaardniveau voor de rest.
    /// Alleen voor de main thread. Elke wijziging verhoogt <see cref="Version"/>, zodat loggers hun niveau kunnen
    /// cachen en een uitgeschakeld niveau vrijwel niets kost.
    /// </summary>
    public static class LogLevels
    {
        public const string CommandLineOption = "-loglevel";

        private static readonly Dictionary<string, LogLevel> Overrides = new Dictionary<string, LogLevel>(StringComparer.OrdinalIgnoreCase);
        private static LogLevel _default = LogLevel.Info;

        public static int Version { get; private set; }

        public static LogLevel Default
        {
            get => _default;
            set
            {
                _default = value;
                Version++;
            }
        }

        public static LogLevel Get(string category) =>
            category != null && Overrides.TryGetValue(category, out var level) ? level : _default;

        public static void Set(string category, LogLevel level)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                throw new ArgumentException("Categorie ontbreekt.", nameof(category));
            }

            Overrides[category.Trim()] = level;
            Version++;
        }

        public static void Clear(string category)
        {
            if (category != null && Overrides.Remove(category))
            {
                Version++;
            }
        }

        /// <summary>Alle categorieën terug naar het standaardniveau, en het standaardniveau terug naar Info.</summary>
        public static void Reset()
        {
            Overrides.Clear();
            _default = LogLevel.Info;
            Version++;
        }

        /// <summary>
        /// Past een specificatie toe als <c>Traffic=Debug,Water=Warning,*=Info</c>; <c>*</c> is het standaardniveau.
        /// Onbekende niveaus of lege delen komen in <paramref name="errors"/>, de rest wordt toegepast.
        /// </summary>
        public static void Apply(string specification, ICollection<string> errors)
        {
            if (string.IsNullOrWhiteSpace(specification))
            {
                return;
            }

            foreach (var part in specification.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split('=');
                if (pair.Length != 2 || string.IsNullOrWhiteSpace(pair[0]) ||
                    !Enum.TryParse(pair[1].Trim(), true, out LogLevel level) || !Enum.IsDefined(typeof(LogLevel), level))
                {
                    errors.Add($"Ongeldig logniveau '{part.Trim()}'; verwacht bijvoorbeeld 'Traffic=Debug'.");
                    continue;
                }

                var category = pair[0].Trim();
                if (category == "*")
                {
                    Default = level;
                }
                else
                {
                    Set(category, level);
                }
            }
        }

        /// <summary>Zoekt <c>-loglevel &lt;specificatie&gt;</c> in de opstartargumenten en past die toe.</summary>
        public static void ApplyCommandLine(string[] arguments, ICollection<string> errors)
        {
            if (arguments == null)
            {
                return;
            }

            for (var i = 0; i < arguments.Length - 1; i++)
            {
                if (string.Equals(arguments[i], CommandLineOption, StringComparison.OrdinalIgnoreCase))
                {
                    Apply(arguments[i + 1], errors);
                }
            }
        }
    }
}
