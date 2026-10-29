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

        /// <summary>Gooit een uitzondering als de instellingen niet bruikbaar zijn.</summary>
        public void Validate()
        {
            if (TicksPerSecond < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(TicksPerSecond), TicksPerSecond, "Minstens 1 tick per seconde.");
            }

            if (MaxTicksPerUpdate < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxTicksPerUpdate), MaxTicksPerUpdate, "Minstens 1 tick per update.");
            }

            if (MaxFrameSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(MaxFrameSeconds), MaxFrameSeconds, "Moet groter dan 0 zijn.");
            }

            if (TicksPerDay < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(TicksPerDay), TicksPerDay, "Minstens 1 tick per dag.");
            }

            if (StartYear < 1 || StartYear > 9000)
            {
                throw new ArgumentOutOfRangeException(nameof(StartYear), StartYear, "Startjaar moet tussen 1 en 9000 liggen.");
            }

            if (StartMonth < 1 || StartMonth > 12)
            {
                throw new ArgumentOutOfRangeException(nameof(StartMonth), StartMonth, "Maand moet tussen 1 en 12 liggen.");
            }

            if (StartDay < 1 || StartDay > DateTime.DaysInMonth(StartYear, StartMonth))
            {
                throw new ArgumentOutOfRangeException(nameof(StartDay), StartDay, "Dag bestaat niet in de startmaand.");
            }
        }
    }
}
