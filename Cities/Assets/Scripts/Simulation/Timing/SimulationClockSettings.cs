using System;
using UnityEngine;

namespace Cities.Simulation.Timing
{
    /// <summary>Instellingen van de simulatieklok en de spelkalender.</summary>
    [Serializable]
    public struct SimulationClockSettings
    {
        [Tooltip("Aantal simulatieticks per echte seconde bij snelheid 1x.")]
        [Min(1)]
        public int TicksPerSecond;

        [Tooltip("Maximaal aantal ticks per frame; daarboven loopt de simulatie achter in plaats van de framerate te laten instorten.")]
        [Min(1)]
        public int MaxTicksPerUpdate;

        [Tooltip("Langere frametijden (bijvoorbeeld na een laadmoment of breakpoint) worden tot deze waarde afgekapt, in seconden.")]
        [Min(0.01f)]
        public float MaxFrameSeconds;

        [Tooltip("Aantal ticks in één speldag.")]
        [Min(1)]
        public int TicksPerDay;

        [Min(1)]
        public int StartYear;

        [Range(1, 12)]
        public int StartMonth;

        [Range(1, 31)]
        public int StartDay;

        /// <summary>
        /// Standaard: 10 Hz en 100 ticks per dag. Bij 1x duurt een dag 10 seconden, een maand ongeveer vijf minuten
        /// en een jaar een uur; bij 3x een derde daarvan.
        /// </summary>
        public static SimulationClockSettings Default => new SimulationClockSettings
        {
            TicksPerSecond = 10,
            MaxTicksPerUpdate = 8,
            MaxFrameSeconds = 0.25f,
            TicksPerDay = 100,
            StartYear = 2000,
            StartMonth = 1,
            StartDay = 1,
        };

        public double TickSeconds => 1.0 / TicksPerSecond;

        /// <summary>Gooit een <see cref="ArgumentException"/> als de instellingen niet bruikbaar zijn.</summary>
        public static void Validate(SimulationClockSettings settings)
        {
            var error = settings.FindError();
            if (error != null)
            {
                throw new ArgumentException(error, nameof(settings));
            }
        }

        /// <summary>Beschrijving van de eerste ongeldige instelling, of null als alles klopt.</summary>
        public string FindError()
        {
            if (TicksPerSecond < 1)
            {
                return $"{nameof(TicksPerSecond)} moet minstens 1 zijn (is {TicksPerSecond}).";
            }

            if (MaxTicksPerUpdate < 1)
            {
                return $"{nameof(MaxTicksPerUpdate)} moet minstens 1 zijn (is {MaxTicksPerUpdate}).";
            }

            if (MaxFrameSeconds <= 0f)
            {
                return $"{nameof(MaxFrameSeconds)} moet groter dan 0 zijn (is {MaxFrameSeconds}).";
            }

            if (TicksPerDay < 1)
            {
                return $"{nameof(TicksPerDay)} moet minstens 1 zijn (is {TicksPerDay}).";
            }

            if (StartYear < 1 || StartYear > 9000)
            {
                return $"{nameof(StartYear)} moet tussen 1 en 9000 liggen (is {StartYear}).";
            }

            if (StartMonth < 1 || StartMonth > 12)
            {
                return $"{nameof(StartMonth)} moet tussen 1 en 12 liggen (is {StartMonth}).";
            }

            if (StartDay < 1 || StartDay > DateTime.DaysInMonth(StartYear, StartMonth))
            {
                return $"{nameof(StartDay)} {StartDay} bestaat niet in maand {StartMonth} van {StartYear}.";
            }

            return null;
        }
    }
}
