using System;

namespace Cities.Simulation.Timing
{
    /// <summary>Datum in de spelkalender (gregoriaans, met schrikkeljaren).</summary>
    public readonly struct GameDate : IEquatable<GameDate>, IComparable<GameDate>
    {
        public GameDate(int year, int month, int day)
        {
            Year = year;
            Month = month;
            Day = day;
        }

        public int Year { get; }

        public int Month { get; }

        public int Day { get; }

        public DayOfWeek DayOfWeek => ToDateTime().DayOfWeek;

        public bool IsWeekend => DayOfWeek == DayOfWeek.Saturday || DayOfWeek == DayOfWeek.Sunday;

        public static GameDate FromDateTime(DateTime date) => new GameDate(date.Year, date.Month, date.Day);

        public DateTime ToDateTime() => new DateTime(Year, Month, Day, 0, 0, 0, DateTimeKind.Unspecified);

        public GameDate AddDays(long days) => FromDateTime(ToDateTime().AddDays(days));

        public bool Equals(GameDate other) => Year == other.Year && Month == other.Month && Day == other.Day;

        public override bool Equals(object obj) => obj is GameDate other && Equals(other);

        public override int GetHashCode() => (Year * 10000) + (Month * 100) + Day;

        public int CompareTo(GameDate other) => GetHashCode().CompareTo(other.GetHashCode());

        public override string ToString() => $"{Year:D4}-{Month:D2}-{Day:D2}";

        public static bool operator ==(GameDate left, GameDate right) => left.Equals(right);

        public static bool operator !=(GameDate left, GameDate right) => !left.Equals(right);

        public static bool operator <(GameDate left, GameDate right) => left.CompareTo(right) < 0;

        public static bool operator >(GameDate left, GameDate right) => left.CompareTo(right) > 0;

        public static bool operator <=(GameDate left, GameDate right) => left.CompareTo(right) <= 0;

        public static bool operator >=(GameDate left, GameDate right) => left.CompareTo(right) >= 0;
    }
}
