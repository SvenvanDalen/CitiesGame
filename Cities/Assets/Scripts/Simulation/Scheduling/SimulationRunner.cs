using System;
using Cities.Simulation.Commands;
using Cities.Simulation.Timing;

namespace Cities.Simulation.Scheduling
{
    /// <summary>Wat de <see cref="SimulationRunner"/> van een scheduler nodig heeft.</summary>
    public interface ITickScheduler
    {
        void ScheduleTick(in SimulationTick tick);

        /// <summary>Wacht op alle simulatiejobs en voert daarna <paramref name="work"/> uit op de main thread.</summary>
        void ExecuteOnMainThread(Action work);
    }

    /// <summary>
    /// De vaste volgorde van één tick: eerst de ingediende commands (op de main thread, dus na wachten op lopende jobs),
    /// daarna alle simulatiesystemen als jobs. Frames zonder tick (bijvoorbeeld tijdens pauze) verwerken wachtende
    /// commands direct, zodat bouwen meteen zichtbaar is.
    /// </summary>
    /// <remarks>
    /// Een command krijgt altijd het nummer van de laatste tick die vóór het command is uitgevoerd. Of het nu tussen
    /// twee ticks of aan het begin van een tick draait: de positie ten opzichte van de systemen en het nummer zijn gelijk,
    /// zodat een opgenomen sessie identiek na te spelen is.
    /// </remarks>
    public sealed class SimulationRunner : IDisposable
    {
        private readonly SimulationClock _clock;
        private readonly CommandBus _commands;
        private readonly ITickScheduler _scheduler;
        private readonly float _stepSeconds;

        public SimulationRunner(SimulationClock clock, CommandBus commands, ITickScheduler scheduler)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _stepSeconds = (float)clock.Settings.TickSeconds;
            _clock.Ticked += OnTicked;
        }

        /// <summary>Aantal ticks dat is ingepland.</summary>
        public long ScheduledTicks { get; private set; }

        /// <summary>
        /// Aanroepen elk frame met de echte verstreken tijd: laat de klok lopen en verwerkt tijdens pauze de commands.
        /// </summary>
        /// <returns>Het aantal uitgevoerde ticks.</returns>
        public int Update(double realSeconds)
        {
            var ticks = _clock.Advance(realSeconds);
            if (ticks == 0 && _commands.PendingCount > 0)
            {
                ExecuteCommands(_clock.Tick);
            }

            return ticks;
        }

        public void Dispose() => _clock.Ticked -= OnTicked;

        private void OnTicked(long tick)
        {
            if (_commands.PendingCount > 0)
            {
                ExecuteCommands(tick - 1);
            }

            var calendar = _clock.Calendar;
            _scheduler.ScheduleTick(new SimulationTick(tick, _stepSeconds, calendar.Date, calendar.TimeOfDay));
            ScheduledTicks++;
        }

        private void ExecuteCommands(long tick)
        {
            // Commands wijzigen data die jobs mogelijk nog lezen of schrijven.
            _scheduler.ExecuteOnMainThread(() => _commands.ExecutePending(tick));
        }
    }
}
