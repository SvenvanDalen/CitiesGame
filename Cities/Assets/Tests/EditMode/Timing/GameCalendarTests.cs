using System;
using System.Collections.Generic;
using Cities.Simulation.Timing;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Timing
{
    public class GameCalendarTests
    {
        private const int TicksPerDay = 100;

        [Test]
        public void StartsOnStartDateAtMidnight()
        {
            var calendar = new GameCalendar(new GameDate(2000, 1, 1), TicksPerDay);

            Assert.That(calendar.Date, Is.EqualTo(new GameDate(2000, 1, 1)));
            Assert.That(calendar.TimeOfDay, Is.EqualTo(0f));
        }

        [Test]
        public void TimeOfDayIsFractionOfDay()
        {
            var calendar = new GameCalendar(new GameDate(2000, 1, 1), TicksPerDay);

            calendar.Update(150);

            Assert.That(calendar.Date, Is.EqualTo(new GameDate(2000, 1, 2)));
            Assert.That(calendar.TimeOfDay, Is.EqualTo(0.5f));
        }

        [Test]
        public void RaisesDayMonthAndYearInOrder()
        {
            var calendar = new GameCalendar(new GameDate(1999, 12, 31), TicksPerDay);
            var events = new List<string>();
            calendar.DayStarted += d => events.Add($"day {d}");
            calendar.MonthStarted += d => events.Add($"month {d}");
            calendar.YearStarted += d => events.Add($"year {d}");

            calendar.Update(TicksPerDay);

            Assert.That(events, Is.EqualTo(new[] { "day 2000-01-01", "month 2000-01-01", "year 2000-01-01" }));
        }

        [Test]
        public void SkippedDaysAreAllReported()
        {
            var calendar = new GameCalendar(new GameDate(2001, 1, 30), TicksPerDay);
            var days = new List<GameDate>();
            var months = new List<GameDate>();
            calendar.DayStarted += days.Add;
            calendar.MonthStarted += months.Add;

            calendar.Update(3 * TicksPerDay);

            Assert.That(days, Is.EqualTo(new[] { new GameDate(2001, 1, 31), new GameDate(2001, 2, 1), new GameDate(2001, 2, 2) }));
            Assert.That(months, Is.EqualTo(new[] { new GameDate(2001, 2, 1) }));
        }

        [TestCase(2000, 29)]
        [TestCase(2004, 29)]
        [TestCase(2100, 28)]
        [TestCase(2023, 28)]
        public void FebruaryFollowsLeapYearRules(int year, int expectedDays)
        {
            var calendar = new GameCalendar(new GameDate(year, 2, 1), TicksPerDay);
            var lastDayOfFebruary = 0;
            calendar.DayStarted += d =>
            {
                if (d.Month == 2)
                {
                    lastDayOfFebruary = d.Day;
                }
            };

            calendar.Update(31L * TicksPerDay);

            Assert.That(lastDayOfFebruary, Is.EqualTo(expectedDays));
        }

        [Test]
        public void FirstTickOfIsInverseOfUpdate()
        {
            var calendar = new GameCalendar(new GameDate(2000, 1, 1), TicksPerDay);
            var target = new GameDate(2003, 7, 14);

            calendar.Update(calendar.FirstTickOf(target));

            Assert.That(calendar.Date, Is.EqualTo(target));
            Assert.That(calendar.TimeOfDay, Is.EqualTo(0f));
        }

        [Test]
        public void WeekdayMatchesRealCalendar()
        {
            Assert.That(new GameDate(2026, 10, 29).DayOfWeek, Is.EqualTo(DayOfWeek.Thursday));
            Assert.That(new GameDate(2026, 10, 31).IsWeekend, Is.True);
        }

        [Test]
        public void DatesCompareChronologically()
        {
            Assert.That(new GameDate(2000, 12, 31) < new GameDate(2001, 1, 1), Is.True);
            Assert.That(new GameDate(2001, 2, 1) > new GameDate(2001, 1, 31), Is.True);
        }

        [Test]
        public void RejectsNegativeTicks()
        {
            var calendar = new GameCalendar(new GameDate(2000, 1, 1), TicksPerDay);

            Assert.Throws<ArgumentOutOfRangeException>(() => calendar.Update(-1));
        }
    }
}
