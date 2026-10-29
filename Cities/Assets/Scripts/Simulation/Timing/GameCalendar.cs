using System;

namespace Cities.Simulation.Timing
{
    /// <summary>
    /// Vertaalt het tickgetal van de simulatie naar een datum en tijd van de dag,
    /// en meldt wanneer een nieuwe dag, maand of jaar begint.
    /// </summary>
    public sealed class GameCalendar
    {
        private readonly int _ticksPerDay;
        private readonly DateTime _start;
        private long _dayIndex;

        public GameCalendar(GameDate startDate, int ticksPerDay)
        {
            if (ticksPerDay < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(ticksPerDay), ticksPerDay, "Minstens 1 tick per dag.");
            }

            _ticksPerDay = ticksPerDay;
            StartDate = startDate;
            _start = startDate.ToDateTime();
            Date = startDate;
        }

        /// <summary>Begint een nieuwe dag; wordt aangeroepen vóór <see cref="MonthStarted"/> en <see cref="YearStarted"/>.</summary>
        public event Action<GameDate> DayStarted;

        public event Action<GameDate> MonthStarted;

        public event Action<GameDate> YearStarted;

        public GameDate StartDate { get; }

        public int TicksPerDay => _ticksPerDay;

        public GameDate Date { get; private set; }

        /// <summary>Aantal hele dagen sinds de startdatum.</summary>
        public long DaysElapsed => _dayIndex;

        /// <summary>Tijd van de dag als fractie: 0 is middernacht, 0,5 is twaalf uur 's middags.</summary>
        public float TimeOfDay { get; private set; }

        /// <summary>Werkt de kalender bij naar het opgegeven tickgetal en meldt de grenzen die daarbij gepasseerd zijn.</summary>
        public void Update(long tick)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "Tick kan niet negatief zijn.");
            }

            var dayIndex = tick / _ticksPerDay;
            TimeOfDay = (float)(tick % _ticksPerDay) / _ticksPerDay;
            while (_dayIndex < dayIndex)
            {
                _dayIndex++;
                var previous = Date;
                Date = GameDate.FromDateTime(_start.AddDays(_dayIndex));
                DayStarted?.Invoke(Date);
                if (Date.Month != previous.Month || Date.Year != previous.Year)
                {
                    MonthStarted?.Invoke(Date);
                }

                if (Date.Year != previous.Year)
                {
                    YearStarted?.Invoke(Date);
                }
            }
        }

        /// <summary>Zet de kalender direct op een tick zonder gebeurtenissen te melden, bijvoorbeeld na het laden.</summary>
        public void Restore(long tick)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "Tick kan niet negatief zijn.");
            }

            _dayIndex = tick / _ticksPerDay;
            TimeOfDay = (float)(tick % _ticksPerDay) / _ticksPerDay;
            Date = GameDate.FromDateTime(_start.AddDays(_dayIndex));
        }

        /// <summary>Het eerste tickgetal van de opgegeven datum.</summary>
        public long FirstTickOf(GameDate date)
        {
            var days = (long)(date.ToDateTime() - _start).TotalDays;
            if (days < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(date), date, "Datum ligt voor de startdatum.");
            }

            return days * _ticksPerDay;
        }
    }
}
