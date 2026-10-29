using System;
using System.Globalization;

namespace Cities.Core.Diagnostics
{
    /// <summary>Bestemming van logregels; standaard de Unity-console (<see cref="UnityLogSink"/>).</summary>
    public interface ILogSink
    {
        void Write(LogLevel level, string category, string message);
    }

    /// <summary>
    /// Logger voor één categorie, meestal een systeem. Controleert het niveau vóór het opmaken, zodat uitgeschakelde
    /// meldingen geen strings of boxing kosten:
    /// <code>_log.Debug("Route van {0} naar {1}", from, to);</code>
    /// Alleen voor de main thread; in Burst-jobs wordt niet gelogd.
    /// </summary>
    public sealed class SystemLog
    {
        private static ILogSink _sink = new UnityLogSink();

        private LogLevel _cachedLevel;
        private int _cachedVersion = -1;

        public SystemLog(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                throw new ArgumentException("Categorie ontbreekt.", nameof(category));
            }

            Category = category;
        }

        /// <summary>Waar alle loggers naartoe schrijven; te vervangen voor tests of een logbestand.</summary>
        public static ILogSink Sink
        {
            get => _sink;
            set => _sink = value ?? throw new ArgumentNullException(nameof(value));
        }

        public string Category { get; }

        public LogLevel Level
        {
            get
            {
                if (_cachedVersion != LogLevels.Version)
                {
                    _cachedLevel = LogLevels.Get(Category);
                    _cachedVersion = LogLevels.Version;
                }

                return _cachedLevel;
            }
        }

        public bool IsEnabled(LogLevel level) => level != LogLevel.None && level >= Level;

        public void Trace(string message) => Write(LogLevel.Trace, message);

        public void Trace<T>(string format, T arg) => Write(LogLevel.Trace, format, arg);

        public void Debug(string message) => Write(LogLevel.Debug, message);

        public void Debug<T>(string format, T arg) => Write(LogLevel.Debug, format, arg);

        public void Debug<T1, T2>(string format, T1 arg1, T2 arg2) => Write(LogLevel.Debug, format, arg1, arg2);

        public void Info(string message) => Write(LogLevel.Info, message);

        public void Info<T>(string format, T arg) => Write(LogLevel.Info, format, arg);

        public void Info<T1, T2>(string format, T1 arg1, T2 arg2) => Write(LogLevel.Info, format, arg1, arg2);

        public void Warning(string message) => Write(LogLevel.Warning, message);

        public void Warning<T>(string format, T arg) => Write(LogLevel.Warning, format, arg);

        public void Error(string message) => Write(LogLevel.Error, message);

        public void Error(Exception exception, string message)
        {
            if (IsEnabled(LogLevel.Error))
            {
                _sink.Write(LogLevel.Error, Category, $"{message}: {exception}");
            }
        }

        private void Write(LogLevel level, string message)
        {
            if (IsEnabled(level))
            {
                _sink.Write(level, Category, message);
            }
        }

        private void Write<T>(LogLevel level, string format, T arg)
        {
            if (IsEnabled(level))
            {
                _sink.Write(level, Category, string.Format(CultureInfo.InvariantCulture, format, arg));
            }
        }

        private void Write<T1, T2>(LogLevel level, string format, T1 arg1, T2 arg2)
        {
            if (IsEnabled(level))
            {
                _sink.Write(level, Category, string.Format(CultureInfo.InvariantCulture, format, arg1, arg2));
            }
        }
    }
}
