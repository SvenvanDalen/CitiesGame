using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Cities.Gameplay.Cheats
{
    /// <summary>Uitkomst van één consoleregel: gelukt of niet, met een melding voor in het uitvoervenster.</summary>
    public readonly struct ConsoleResult
    {
        private ConsoleResult(bool success, string message)
        {
            Success = success;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }

        public string Message { get; }

        public static ConsoleResult Ok(string message) => new ConsoleResult(true, message);

        public static ConsoleResult Error(string message) => new ConsoleResult(false, message);
    }

    /// <summary>Voert een commando uit met de argumenten achter de naam.</summary>
    public delegate ConsoleResult ConsoleHandler(IReadOnlyList<string> args);

    /// <summary>
    /// De console voor ontwikkel- en testcheats (E24-03): leest regels als <c>geld 5000</c>, zoekt het commando op
    /// (hoofdletters maken niet uit) en voert het uit. Alle commando's behalve <c>help</c> zijn cheats en weigeren
    /// zolang <see cref="CheatsEnabled"/> uit staat. De console kent zelf geen spelsystemen; die registreert
    /// <see cref="StandardCheats"/>.
    /// </summary>
    public sealed class CheatConsole
    {
        public const string HelpName = "help";

        private readonly Dictionary<string, Entry> _commands = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        public CheatConsole()
        {
            Register(HelpName, "help [commando]", "Toont alle commando's of de uitleg van één commando.", Help, isCheat: false);
        }

        public bool CheatsEnabled { get; set; } = true;

        /// <summary>De namen van alle commando's, alfabetisch.</summary>
        public IEnumerable<string> Names => _commands.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

        public void Register(string name, string usage, string summary, ConsoleHandler handler, bool isCheat = true)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Any(char.IsWhiteSpace))
            {
                throw new ArgumentException("Een commandonaam bestaat uit één woord.", nameof(name));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (_commands.ContainsKey(name))
            {
                throw new InvalidOperationException($"Het commando '{name}' bestaat al.");
            }

            _commands.Add(name, new Entry(usage ?? name, summary ?? string.Empty, handler, isCheat));
        }

        /// <summary>Voert een regel uit; een lege regel geeft een geslaagd, leeg resultaat.</summary>
        public ConsoleResult Execute(string line)
        {
            var tokens = Tokenize(line);
            if (tokens.Count == 0)
            {
                return ConsoleResult.Ok(string.Empty);
            }

            if (!_commands.TryGetValue(tokens[0], out var entry))
            {
                return ConsoleResult.Error($"Onbekend commando '{tokens[0]}'. Typ help voor de lijst.");
            }

            if (entry.IsCheat && !CheatsEnabled)
            {
                return ConsoleResult.Error("Cheats staan uit.");
            }

            try
            {
                return entry.Handler(tokens.Skip(1).ToList());
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                return ConsoleResult.Error(exception.Message);
            }
        }

        /// <summary>Alle namen die met <paramref name="prefix"/> beginnen, voor aanvullen met Tab.</summary>
        public IReadOnlyList<string> Complete(string prefix) =>
            Names.Where(name => name.StartsWith(prefix ?? string.Empty, StringComparison.OrdinalIgnoreCase)).ToList();

        /// <summary>Splitst op witruimte; tekst tussen dubbele aanhalingstekens blijft één woord.</summary>
        public static IReadOnlyList<string> Tokenize(string line)
        {
            var tokens = new List<string>();
            if (string.IsNullOrWhiteSpace(line))
            {
                return tokens;
            }

            var current = new StringBuilder();
            var quoted = false;
            var started = false;
            foreach (var c in line)
            {
                if (c == '"')
                {
                    quoted = !quoted;
                    started = true;
                }
                else if (char.IsWhiteSpace(c) && !quoted)
                {
                    if (started)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                        started = false;
                    }
                }
                else
                {
                    current.Append(c);
                    started = true;
                }
            }

            if (started)
            {
                tokens.Add(current.ToString());
            }

            return tokens;
        }

        public static bool TryParseLong(string text, out long value) =>
            long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);

        public static bool TryParseInt(string text, out int value) =>
            int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);

        public static bool TryParseFloat(string text, out float value) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        private ConsoleResult Help(IReadOnlyList<string> args)
        {
            if (args.Count > 0)
            {
                return _commands.TryGetValue(args[0], out var entry)
                    ? ConsoleResult.Ok($"{entry.Usage}\n{entry.Summary}")
                    : ConsoleResult.Error($"Onbekend commando '{args[0]}'.");
            }

            var lines = Names.Select(name => $"{_commands[name].Usage} - {_commands[name].Summary}");
            return ConsoleResult.Ok(string.Join("\n", lines));
        }

        private sealed class Entry
        {
            public Entry(string usage, string summary, ConsoleHandler handler, bool isCheat)
            {
                Usage = usage;
                Summary = summary;
                Handler = handler;
                IsCheat = isCheat;
            }

            public string Usage { get; }

            public string Summary { get; }

            public ConsoleHandler Handler { get; }

            public bool IsCheat { get; }
        }
    }
}
