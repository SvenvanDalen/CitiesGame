using System;
using System.Globalization;
using System.IO;

namespace Cities.Core.Diagnostics
{
    /// <summary>
    /// Schrijft alle logregels naar een bestand per sessie (<c>cities-yyyyMMdd-HHmmss.log</c>) en geeft ze door aan
    /// de vorige bestemming (de Unity-console). Elke regel wordt direct weggeschreven, zodat na een crash alles tot
    /// het laatste moment in het logbestand staat. Oude logbestanden worden opgeruimd tot <see cref="MaxFiles"/>.
    /// Alleen voor de main thread.
    /// </summary>
    public sealed class FileLogSink : ILogSink, IDisposable
    {
        public const string Prefix = "cities-";
        public const string Extension = ".log";
        public const int DefaultMaxFiles = 10;

        private readonly ILogSink _next;
        private readonly Func<DateTime> _utcNow;
        private StreamWriter _writer;

        public FileLogSink(string directory, ILogSink next = null, int maxFiles = DefaultMaxFiles, Func<DateTime> utcNow = null)
        {
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("Map is leeg.", nameof(directory));
            }

            if (maxFiles < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxFiles), "Bewaar minstens één logbestand.");
            }

            Directory = directory;
            MaxFiles = maxFiles;
            _next = next;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            System.IO.Directory.CreateDirectory(directory);
            Prune(directory, maxFiles - 1);
            Path = System.IO.Path.Combine(directory, Prefix + _utcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + Extension);
            _writer = new StreamWriter(new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
        }

        public string Directory { get; }

        public string Path { get; }

        public int MaxFiles { get; }

        public void Write(LogLevel level, string category, string message)
        {
            _next?.Write(level, category, message);
            WriteLine($"{_utcNow().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)} {level,-7} [{category}] {message}");
        }

        /// <summary>Schrijft een regel buiten het logniveau om, bijvoorbeeld een onverwerkte uitzondering vlak voor een crash.</summary>
        public void WriteFatal(string category, Exception exception) =>
            WriteLine($"{_utcNow().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)} FATAL   [{category}] {exception}");

        public void Dispose()
        {
            _writer?.Dispose();
            _writer = null;
        }

        /// <summary>Verwijdert de oudste logbestanden tot er hoogstens <paramref name="keep"/> over zijn.</summary>
        public static void Prune(string directory, int keep)
        {
            if (!System.IO.Directory.Exists(directory))
            {
                return;
            }

            var files = new DirectoryInfo(directory).GetFiles(Prefix + "*" + Extension);
            Array.Sort(files, (a, b) => string.CompareOrdinal(b.Name, a.Name));
            for (var i = Math.Max(keep, 0); i < files.Length; i++)
            {
                try
                {
                    files[i].Delete();
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    // Een logbestand dat in gebruik is, blijft staan; dat is niet erg.
                }
            }
        }

        private void WriteLine(string line) => _writer?.WriteLine(line);
    }
}
