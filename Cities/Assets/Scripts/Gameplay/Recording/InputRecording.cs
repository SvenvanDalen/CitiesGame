using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Cities.Gameplay.Recording
{
    public enum RecordedPhase
    {
        Started = 0,
        Performed = 1,
        Canceled = 2,
    }

    /// <summary>Eén invoeractie op een simulatietick, bijvoorbeeld <c>Game/Pause</c> of een camerabeweging.</summary>
    public readonly struct RecordedAction : IEquatable<RecordedAction>
    {
        public RecordedAction(long tick, string action, RecordedPhase phase, float x = 0f, float y = 0f)
        {
            Tick = tick;
            Action = action;
            Phase = phase;
            X = x;
            Y = y;
        }

        public long Tick { get; }

        /// <summary>Naam van de actie, zonder spaties (bijvoorbeeld <c>Camera/Pan</c>).</summary>
        public string Action { get; }

        public RecordedPhase Phase { get; }

        /// <summary>Waarde van de actie: één getal in X, een vector in X en Y.</summary>
        public float X { get; }

        public float Y { get; }

        public bool Equals(RecordedAction other) =>
            Tick == other.Tick && Action == other.Action && Phase == other.Phase && X.Equals(other.X) && Y.Equals(other.Y);

        public override bool Equals(object obj) => obj is RecordedAction other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Tick, Action, Phase, X, Y);
    }

    /// <summary>
    /// Een opname van invoeracties met de seed en versie waarmee hij is gemaakt (E24-08). Wordt als leesbare tekst
    /// bewaard, één actie per regel, zodat een opname in een bugmelding past en in een diff te vergelijken is.
    /// </summary>
    public sealed class InputRecording
    {
        private const string Header = "cities-input 1";

        public InputRecording(ulong seed, string gameVersion, IReadOnlyList<RecordedAction> actions)
        {
            Seed = seed;
            GameVersion = gameVersion ?? string.Empty;
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
        }

        public ulong Seed { get; }

        public string GameVersion { get; }

        public IReadOnlyList<RecordedAction> Actions { get; }

        public long LastTick => Actions.Count == 0 ? 0 : Actions[Actions.Count - 1].Tick;

        public void Write(TextWriter writer)
        {
            writer.WriteLine(Header);
            writer.WriteLine("seed " + Seed.ToString(CultureInfo.InvariantCulture));
            writer.WriteLine("version " + GameVersion);
            foreach (var action in Actions)
            {
                writer.WriteLine(string.Join(
                    " ",
                    action.Tick.ToString(CultureInfo.InvariantCulture),
                    action.Action,
                    action.Phase.ToString(),
                    action.X.ToString("R", CultureInfo.InvariantCulture),
                    action.Y.ToString("R", CultureInfo.InvariantCulture)));
            }
        }

        public static InputRecording Read(TextReader reader)
        {
            if (reader.ReadLine() != Header)
            {
                throw new FormatException("Dit is geen invoeropname van Cities.");
            }

            if (!ParseHeader(reader.ReadLine(), "seed", out var seedText) || !ulong.TryParse(seedText, NumberStyles.None, CultureInfo.InvariantCulture, out var seed))
            {
                throw new FormatException("De seed ontbreekt of is ongeldig.");
            }

            if (!ParseHeader(reader.ReadLine(), "version", out var version))
            {
                throw new FormatException("De versie ontbreekt.");
            }

            var actions = new List<RecordedAction>();
            var lineNumber = 3;
            var previous = long.MinValue;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (line.Length == 0)
                {
                    continue;
                }

                var action = ParseAction(line, lineNumber);
                if (action.Tick < previous)
                {
                    throw new FormatException($"Regel {lineNumber}: de tick loopt terug.");
                }

                previous = action.Tick;
                actions.Add(action);
            }

            return new InputRecording(seed, version, actions);
        }

        private static bool ParseHeader(string line, string key, out string value)
        {
            value = null;
            if (line is null || !line.StartsWith(key + " ", StringComparison.Ordinal))
            {
                return false;
            }

            value = line.Substring(key.Length + 1);
            return true;
        }

        private static RecordedAction ParseAction(string line, int lineNumber)
        {
            var parts = line.Split(' ');
            if (parts.Length != 5
                || !long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var tick)
                || !Enum.TryParse(parts[2], out RecordedPhase phase)
                || !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                || !float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                throw new FormatException($"Regel {lineNumber}: ongeldige actie \"{line}\".");
            }

            return new RecordedAction(tick, parts[1], phase, x, y);
        }
    }

    /// <summary>Neemt acties op tijdens het spelen; acties buiten een opname worden genegeerd.</summary>
    public sealed class InputRecorder
    {
        private readonly List<RecordedAction> _actions = new List<RecordedAction>();
        private ulong _seed;
        private string _version = string.Empty;

        public bool IsRecording { get; private set; }

        public int Count => _actions.Count;

        public void Start(ulong seed, string gameVersion)
        {
            if (IsRecording)
            {
                throw new InvalidOperationException("Er loopt al een opname.");
            }

            _actions.Clear();
            _seed = seed;
            _version = gameVersion ?? string.Empty;
            IsRecording = true;
        }

        /// <returns><c>true</c> als de actie is opgenomen.</returns>
        public bool Record(long tick, string action, RecordedPhase phase, float x = 0f, float y = 0f)
        {
            if (!IsRecording)
            {
                return false;
            }

            if (string.IsNullOrEmpty(action) || action.IndexOf(' ') >= 0)
            {
                throw new ArgumentException("Een actienaam is verplicht en bevat geen spaties.", nameof(action));
            }

            if (_actions.Count > 0 && tick < _actions[_actions.Count - 1].Tick)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "Een tick kan niet terug in de tijd.");
            }

            _actions.Add(new RecordedAction(tick, action, phase, x, y));
            return true;
        }

        public InputRecording Stop()
        {
            if (!IsRecording)
            {
                throw new InvalidOperationException("Er loopt geen opname.");
            }

            IsRecording = false;
            return new InputRecording(_seed, _version, _actions.ToArray());
        }
    }

    /// <summary>Speelt een opname af: elke tick geeft de acties die op of voor die tick vallen, in volgorde.</summary>
    public sealed class InputPlayer
    {
        private InputRecording _recording;
        private int _next;

        public bool IsPlaying => _recording != null && _next < _recording.Actions.Count;

        public int Played => _next;

        public int Total => _recording?.Actions.Count ?? 0;

        public float Progress => Total == 0 ? 1f : (float)_next / Total;

        public void Begin(InputRecording recording)
        {
            _recording = recording ?? throw new ArgumentNullException(nameof(recording));
            _next = 0;
        }

        /// <summary>Roept <paramref name="apply"/> aan voor elke nog niet afgespeelde actie met een tick tot en met <paramref name="tick"/>.</summary>
        public int Advance(long tick, Action<RecordedAction> apply)
        {
            if (apply is null)
            {
                throw new ArgumentNullException(nameof(apply));
            }

            var count = 0;
            while (_recording != null && _next < _recording.Actions.Count && _recording.Actions[_next].Tick <= tick)
            {
                apply(_recording.Actions[_next++]);
                count++;
            }

            return count;
        }

        public void Stop()
        {
            _recording = null;
            _next = 0;
        }
    }
}
