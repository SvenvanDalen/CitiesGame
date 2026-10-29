using System;
using Cities.Core.Diagnostics;
using Cities.Core.Persistence;
using Cities.Simulation.Commands;

namespace Cities.Simulation.Timing
{
    /// <summary>
    /// Sectie <c>CLCK</c>: tick en snelheid van de klok. De datum volgt uit de tick en de kalenderinstellingen; die
    /// worden meegeschreven om een andere instelling bij laden te melden. Openstaande commands worden bij laden
    /// weggegooid, omdat ze bij de oude stad horen.
    /// </summary>
    public sealed class SimulationClockSection : ISaveSection
    {
        public const string SectionId = "CLCK";

        private static readonly SystemLog Log = new SystemLog("Save");

        private readonly SimulationClock _clock;
        private readonly CommandBus _commands;

        public SimulationClockSection(SimulationClock clock, CommandBus commands)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            var settings = _clock.Settings;
            writer.Write(_clock.Tick);
            writer.Write((byte)_clock.Speed);
            writer.Write(settings.TicksPerDay);
            writer.Write(settings.StartYear);
            writer.Write(settings.StartMonth);
            writer.Write(settings.StartDay);
        }

        public Action Read(SaveReader reader, int version)
        {
            var tick = reader.ReadInt64();
            var speed = (SimulationSpeed)reader.ReadByte();
            var ticksPerDay = reader.ReadInt32();
            var start = new GameDate(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            reader.ExpectEnd();
            if (tick < 0)
            {
                throw reader.Corrupt($"negatieve tick {tick}");
            }

            if (!Enum.IsDefined(typeof(SimulationSpeed), speed))
            {
                throw reader.Corrupt($"onbekende snelheid {(int)speed}");
            }

            return () =>
            {
                var settings = _clock.Settings;
                var current = new GameDate(settings.StartYear, settings.StartMonth, settings.StartDay);
                if (ticksPerDay != settings.TicksPerDay || !start.Equals(current))
                {
                    Log.Warning("De kalender van de save ({0}) verschilt van deze scene; de datum wordt opnieuw berekend.", $"{start}, {ticksPerDay} ticks per dag");
                }

                _commands.ClearPending();
                _clock.Restore(tick, speed);
            };
        }
    }
}
