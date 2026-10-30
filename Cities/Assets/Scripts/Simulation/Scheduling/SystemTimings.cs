using System;
using System.Collections.Generic;

namespace Cities.Simulation.Scheduling
{
    /// <summary>
    /// Tijden per systeem over de laatste ticks (een rollend venster), voor het debugpaneel: gemiddelde, maximum en
    /// laatste waarde in milliseconden. Ook de hele tick wordt bijgehouden.
    /// </summary>
    public sealed class SystemTimings
    {
        public const string TickName = "Tick";

        private readonly Dictionary<string, Window> _windows = new Dictionary<string, Window>(StringComparer.Ordinal);
        private readonly List<string> _names = new List<string>();

        public SystemTimings(int windowSize = 60)
        {
            if (windowSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(windowSize), windowSize, "Venster moet minstens 1 tick zijn.");
            }

            WindowSize = windowSize;
        }

        public int WindowSize { get; }

        /// <summary>Namen van de systemen in de volgorde waarin ze voor het eerst gemeten zijn (zonder de tick zelf).</summary>
        public IReadOnlyList<string> Systems => _names;

        public void Record(string system, double milliseconds)
        {
            if (system is null)
            {
                throw new ArgumentNullException(nameof(system));
            }

            if (!_windows.TryGetValue(system, out var window))
            {
                window = new Window(WindowSize);
                _windows.Add(system, window);
                if (system != TickName)
                {
                    _names.Add(system);
                }
            }

            window.Add(Math.Max(0d, milliseconds));
        }

        public TimingStats Get(string system) =>
            _windows.TryGetValue(system, out var window) ? window.Stats() : default;

        public TimingStats Tick => Get(TickName);

        public void Clear()
        {
            _windows.Clear();
            _names.Clear();
        }

        private sealed class Window
        {
            private readonly double[] _values;
            private int _next;
            private int _count;
            private double _last;

            public Window(int size) => _values = new double[size];

            public void Add(double value)
            {
                _values[_next] = value;
                _next = (_next + 1) % _values.Length;
                _count = Math.Min(_count + 1, _values.Length);
                _last = value;
            }

            public TimingStats Stats()
            {
                var sum = 0d;
                var max = 0d;
                for (var i = 0; i < _count; i++)
                {
                    sum += _values[i];
                    max = Math.Max(max, _values[i]);
                }

                return new TimingStats(_count, _count == 0 ? 0d : sum / _count, max, _last);
            }
        }
    }

    public readonly struct TimingStats
    {
        public TimingStats(int samples, double averageMs, double maxMs, double lastMs)
        {
            Samples = samples;
            AverageMs = averageMs;
            MaxMs = maxMs;
            LastMs = lastMs;
        }

        public int Samples { get; }

        public double AverageMs { get; }

        public double MaxMs { get; }

        public double LastMs { get; }
    }
}
