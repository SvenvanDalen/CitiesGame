using System;

namespace Cities.Gameplay.Persistence
{
    /// <summary>
    /// Telt echte speeltijd (niet simulatietijd) en geeft aan wanneer een autosave aan de beurt is. Staat uit tot
    /// <see cref="IntervalSeconds"/> groter dan nul is.
    /// </summary>
    public sealed class AutosaveTimer
    {
        public const double MinimumIntervalSeconds = 30d;

        private double _interval;
        private double _elapsed;

        /// <summary>Interval in seconden; 0 zet autosave uit, andere waarden worden minstens 30 s.</summary>
        public double IntervalSeconds
        {
            get => _interval;
            set
            {
                if (double.IsNaN(value) || value <= 0d || double.IsInfinity(value))
                {
                    _interval = 0d;
                }
                else
                {
                    _interval = Math.Max(MinimumIntervalSeconds, value);
                }

                _elapsed = 0d;
            }
        }

        public bool Enabled => _interval > 0d;

        public double SecondsUntilDue => Enabled ? Math.Max(0d, _interval - _elapsed) : double.PositiveInfinity;

        /// <summary>Telt <paramref name="seconds"/> bij; true als de autosave nu aan de beurt is (daarna begint de telling opnieuw).</summary>
        public bool Advance(double seconds)
        {
            if (!Enabled || seconds <= 0d || double.IsNaN(seconds) || double.IsInfinity(seconds))
            {
                return false;
            }

            _elapsed += seconds;
            if (_elapsed < _interval)
            {
                return false;
            }

            _elapsed = 0d;
            return true;
        }

        public void Reset() => _elapsed = 0d;
    }
}
