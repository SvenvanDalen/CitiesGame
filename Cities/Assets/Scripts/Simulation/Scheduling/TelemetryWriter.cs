using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Cities.Simulation.Scheduling
{
    /// <summary>
    /// Schrijft tick-tijden en aantallen naar een CSV-bestand voor trendanalyse (E24-07). Elke <see cref="Interval"/> ticks komt er per
    /// systeem en per teller één regel: <c>tick,name,kind,average,max,last</c>. Tijden staan in milliseconden; bij een teller is
    /// de waarde de stand op dat moment (average, max en last zijn dan gelijk). Alleen voor de main thread.
    /// </summary>
    public sealed class TelemetryWriter : IDisposable
    {
        public const string Header = "tick,name,kind,average,max,last";
        public const string TimeKind = "ms";
        public const string CountKind = "count";

        private readonly TextWriter _writer;
        private readonly SystemTimings _timings;
        private readonly bool _ownsWriter;
        private readonly List<KeyValuePair<string, Func<long>>> _counters = new List<KeyValuePair<string, Func<long>>>();
        private long _lastWritten = long.MinValue;

        public TelemetryWriter(TextWriter writer, SystemTimings timings, int interval = 60, bool ownsWriter = false)
        {
            if (interval < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(interval), interval, "Interval moet minstens 1 tick zijn.");
            }

            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _timings = timings ?? throw new ArgumentNullException(nameof(timings));
            _ownsWriter = ownsWriter;
            Interval = interval;
            _writer.WriteLine(Header);
        }

        /// <summary>Aantal ticks tussen twee reeksen regels.</summary>
        public int Interval { get; }

        /// <summary>Aantal reeksen dat tot nu toe is geschreven.</summary>
        public int Samples { get; private set; }

        /// <summary>Registreert een teller, bijvoorbeeld het aantal burgers of voertuigen.</summary>
        public void AddCounter(string name, Func<long> value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Naam is leeg.", nameof(name));
            }

            _counters.Add(new KeyValuePair<string, Func<long>>(name, value ?? throw new ArgumentNullException(nameof(value))));
        }

        /// <summary>Roep aan na elke tick; schrijft alleen op een veelvoud van <see cref="Interval"/>. Geeft terug of er geschreven is.</summary>
        public bool OnTick(long tick)
        {
            if (tick % Interval != 0 || tick == _lastWritten)
            {
                return false;
            }

            _lastWritten = tick;
            WriteTime(tick, SystemTimings.TickName);
            foreach (var system in _timings.Systems)
            {
                WriteTime(tick, system);
            }

            foreach (var counter in _counters)
            {
                var value = counter.Value().ToString(CultureInfo.InvariantCulture);
                _writer.WriteLine(string.Join(",", tick.ToString(CultureInfo.InvariantCulture), Escape(counter.Key), CountKind, value, value, value));
            }

            _writer.Flush();
            Samples++;
            return true;
        }

        public void Dispose()
        {
            if (_ownsWriter)
            {
                _writer.Dispose();
            }
            else
            {
                _writer.Flush();
            }
        }

        private void WriteTime(long tick, string system)
        {
            var stats = _timings.Get(system);
            _writer.WriteLine(string.Join(
                ",",
                tick.ToString(CultureInfo.InvariantCulture),
                Escape(system),
                TimeKind,
                stats.AverageMs.ToString("0.000", CultureInfo.InvariantCulture),
                stats.MaxMs.ToString("0.000", CultureInfo.InvariantCulture),
                stats.LastMs.ToString("0.000", CultureInfo.InvariantCulture)));
        }

        private static string Escape(string name) =>
            name.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0 ? name : "\"" + name.Replace("\"", "\"\"") + "\"";
    }
}
