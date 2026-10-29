using System;

namespace Cities.Simulation.Timing
{
    /// <summary>
    /// Vaste-stap klok van de simulatie, los van de framerate. Elk frame krijgt de klok de echte verstreken tijd
    /// en voert zoveel vaste ticks uit als de snelheid toelaat. Bij lage framerates loopt de simulatie liever achter
    /// dan dat de framerate instort (begrensd door <see cref="SimulationClockSettings.MaxTicksPerUpdate"/>).
    /// </summary>
    public sealed class SimulationClock
    {
        // Kleine marge zodat bijvoorbeeld drie frames van 1/30 s precies één tick van 0,1 s opleveren.
        private const double TickEpsilon = 1e-9;

        private readonly SimulationClockSettings _settings;
        private double _accumulatedTicks;
        private SimulationSpeed _speed = SimulationSpeed.Normal;
        private SimulationSpeed _speedBeforePause = SimulationSpeed.Normal;

        public SimulationClock(SimulationClockSettings settings)
        {
            settings.Validate();
            _settings = settings;
            Calendar = new GameCalendar(new GameDate(settings.StartYear, settings.StartMonth, settings.StartDay), settings.TicksPerDay);
        }

        /// <summary>Wordt na elke tick aangeroepen met het nieuwe tickgetal; hier hangen de simulatiesystemen aan.</summary>
        public event Action<long> Ticked;

        public event Action<SimulationSpeed> SpeedChanged;

        public GameCalendar Calendar { get; }

        public SimulationClockSettings Settings => _settings;

        /// <summary>Aantal uitgevoerde ticks sinds het begin van het spel.</summary>
        public long Tick { get; private set; }

        public SimulationSpeed Speed => _speed;

        public bool IsPaused => _speed == SimulationSpeed.Paused;

        /// <summary>
        /// Positie tussen de vorige en de volgende tick (0 tot 1), om beweging vloeiend te tekenen.
        /// </summary>
        public float InterpolationFactor => (float)Math.Min(_accumulatedTicks, 1.0);

        /// <summary>Aantal ticks dat bij de laatste <see cref="Advance"/> is overgeslagen omdat het maximum per update bereikt was.</summary>
        public int DroppedTicks { get; private set; }

        public void SetSpeed(SimulationSpeed speed)
        {
            if (!Enum.IsDefined(typeof(SimulationSpeed), speed))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "Onbekende snelheid.");
            }

            if (speed == _speed)
            {
                return;
            }

            if (speed == SimulationSpeed.Paused)
            {
                _speedBeforePause = _speed;
            }

            _speed = speed;
            SpeedChanged?.Invoke(speed);
        }

        /// <summary>Pauzeert, of hervat met de snelheid van voor de pauze.</summary>
        public void TogglePause() => SetSpeed(IsPaused ? _speedBeforePause : SimulationSpeed.Paused);

        /// <summary>Verwerkt de echte verstreken tijd en voert de bijbehorende ticks uit.</summary>
        /// <returns>Het aantal uitgevoerde ticks.</returns>
        public int Advance(double realSeconds)
        {
            if (realSeconds < 0 || double.IsNaN(realSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(realSeconds), realSeconds, "Verstreken tijd moet 0 of groter zijn.");
            }

            DroppedTicks = 0;
            if (IsPaused)
            {
                return 0;
            }

            _accumulatedTicks += Math.Min(realSeconds, _settings.MaxFrameSeconds) * (int)_speed * _settings.TicksPerSecond;
            var due = (int)Math.Floor(_accumulatedTicks + TickEpsilon);
            var ticks = Math.Min(due, _settings.MaxTicksPerUpdate);
            _accumulatedTicks = Math.Max(_accumulatedTicks - due, 0.0);
            DroppedTicks = due - ticks;

            for (var i = 0; i < ticks; i++)
            {
                RunTick();
            }

            return ticks;
        }

        /// <summary>Voert precies één tick uit, ook als de klok gepauzeerd is (stap-voor-stap debuggen).</summary>
        public void Step() => RunTick();

        /// <summary>Zet de klok op een opgeslagen tickgetal zonder gebeurtenissen te melden.</summary>
        public void Restore(long tick, SimulationSpeed speed)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "Tick kan niet negatief zijn.");
            }

            Tick = tick;
            _accumulatedTicks = 0;
            _speed = speed;
            _speedBeforePause = speed == SimulationSpeed.Paused ? SimulationSpeed.Normal : speed;
            Calendar.Restore(tick);
        }

        private void RunTick()
        {
            Tick++;
            Calendar.Update(Tick);
            Ticked?.Invoke(Tick);
        }
    }
}
