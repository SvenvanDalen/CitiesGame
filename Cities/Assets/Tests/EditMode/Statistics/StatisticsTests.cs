using System;
using Cities.Core.Persistence;
using Cities.Gameplay.Statistics;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Statistics
{
    public sealed class HistorySeriesTests
    {
        [Test]
        public void KeepsOnlyTheNewestPointsAndNeverGrows()
        {
            var series = new HistorySeries(3);
            for (var i = 1; i <= 10; i++)
            {
                series.Add(i);
            }

            Assert.That(series.Count, Is.EqualTo(3));
            Assert.That(series.Capacity, Is.EqualTo(3));
            Assert.That(series[0], Is.EqualTo(8f));
            Assert.That(series[2], Is.EqualTo(10f));
            Assert.That(series.Last, Is.EqualTo(10f));
            Assert.That(series.Previous, Is.EqualTo(9f));
            Assert.That(series.Min, Is.EqualTo(8f));
            Assert.That(series.Max, Is.EqualTo(10f));
            Assert.That(series.Average, Is.EqualTo(9f));
        }

        [Test]
        public void LowerResolutionAveragesSamples()
        {
            var series = new HistorySeries(10, 4);
            for (var i = 1; i <= 7; i++)
            {
                series.Add(i);
            }

            Assert.That(series.Count, Is.EqualTo(1));
            Assert.That(series[0], Is.EqualTo(2.5f));
            series.Add(8f);
            Assert.That(series.Count, Is.EqualTo(2));
            Assert.That(series[1], Is.EqualTo(6.5f));
        }

        [Test]
        public void EmptySeriesIsZeroEverywhere()
        {
            var series = new HistorySeries(2);

            Assert.That(series.Last, Is.EqualTo(0f));
            Assert.That(series.Previous, Is.EqualTo(0f));
            Assert.That(series.Min, Is.EqualTo(0f));
            Assert.That(series.Max, Is.EqualTo(0f));
            Assert.That(series.Average, Is.EqualTo(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = series[0]);
            series.Add(4f);
            Assert.That(series.Previous, Is.EqualTo(4f));
        }

        [Test]
        public void CopyToFillsTheBufferAndChecksSize()
        {
            var series = new HistorySeries(4);
            series.Add(1f);
            series.Add(2f);
            var buffer = new float[3];
            series.CopyTo(buffer);

            Assert.That(buffer, Is.EqualTo(new[] { 1f, 2f, 0f }));
            Assert.Throws<ArgumentException>(() => series.CopyTo(new float[1]));
            Assert.Throws<ArgumentNullException>(() => series.CopyTo(null));
        }

        [Test]
        public void ConstructorRejectsBadSizes()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HistorySeries(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HistorySeries(5, 0));
        }
    }

    public sealed class StatisticsHistoryTests
    {
        [Test]
        public void RowsGroupByCategoryWithTrend()
        {
            var history = new StatisticsHistory(10);
            history.Record(StatCategory.Population, "pop", "Inwoners", 1000f);
            history.Record(StatCategory.Population, "pop", "Inwoners", 1100f);
            history.Record(StatCategory.Economy, "money", "Saldo", 500f);
            history.Record(StatCategory.Economy, "money", "Saldo", 400f);
            history.Record(StatCategory.Traffic, "flow", null, 3f);
            history.Record(StatCategory.Traffic, "flow", null, 3f);

            var pop = history.Rows(StatCategory.Population);
            var money = history.Rows(StatCategory.Economy);
            var traffic = history.Rows(StatCategory.Traffic);

            Assert.That(pop.Count, Is.EqualTo(1));
            Assert.That(pop[0].Current, Is.EqualTo(1100f));
            Assert.That(pop[0].Trend, Is.EqualTo(1));
            Assert.That(money[0].Trend, Is.EqualTo(-1));
            Assert.That(traffic[0].Trend, Is.EqualTo(0));
            Assert.That(traffic[0].Label, Is.EqualTo("flow"));
            Assert.That(history.Rows(StatCategory.Services), Is.Empty);
            Assert.That(history.SeriesCount, Is.EqualTo(3));
            Assert.That(history.Series("pop").Count, Is.EqualTo(2));
            Assert.That(history.Series("nope"), Is.Null);
            Assert.That(history.Series(null), Is.Null);
        }

        [Test]
        public void ConstructorAndRecordValidate()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StatisticsHistory(0));
            Assert.Throws<ArgumentException>(() => new StatisticsHistory().Record(StatCategory.Services, " ", "x", 1f));
        }

        [Test]
        public void SectionRoundTripsAllSeries()
        {
            var history = new StatisticsHistory(5);
            for (var i = 0; i < 8; i++)
            {
                history.Record(StatCategory.Population, "pop", "Inwoners", i);
                history.Record(StatCategory.Services, "fire", "Brandweer", i * 2);
            }

            var bytes = SaveBytes.Write(new SaveMetadata(), new StatisticsHistorySection(history));
            var restored = new StatisticsHistory(5);
            SaveBytes.Read(bytes, new StatisticsHistorySection(restored));

            Assert.That(restored.SeriesCount, Is.EqualTo(2));
            Assert.That(restored.Series("pop").Count, Is.EqualTo(5));
            Assert.That(restored.Series("pop")[0], Is.EqualTo(3f));
            Assert.That(restored.Series("fire").Last, Is.EqualTo(14f));
            Assert.That(restored.Rows(StatCategory.Services)[0].Label, Is.EqualTo("Brandweer"));
        }

        [Test]
        public void SectionRejectsTooManyPointsAndNegativeCounts()
        {
            var history = new StatisticsHistory(5);
            var tooMany = SaveBytes.ReaderFor(w =>
            {
                w.Write(1);
                w.Write("k");
                w.Write("l");
                w.Write((byte)0);
                w.Write(99);
            });
            var negative = SaveBytes.ReaderFor(w => w.Write(-1));

            Assert.Throws<SaveFormatException>(() => new StatisticsHistorySection(history).Read(tooMany, 1));
            Assert.Throws<SaveFormatException>(() => new StatisticsHistorySection(history).Read(negative, 1));
            Assert.Throws<ArgumentNullException>(() => new StatisticsHistorySection(null));
        }
    }

    public sealed class FinanceLedgerTests
    {
        [Test]
        public void PeriodsTotalPerCategoryAndStartFresh()
        {
            var ledger = new FinanceLedger(10);
            ledger.Income("Belasting", 100);
            ledger.Income("Belasting", 50);
            ledger.Expense("Diensten", 80);
            Assert.That(ledger.CurrentIncome("Belasting"), Is.EqualTo(150.0));
            Assert.That(ledger.CurrentExpense("Diensten"), Is.EqualTo(80.0));
            Assert.That(ledger.CurrentIncome("onbekend"), Is.EqualTo(0.0));
            Assert.That(ledger.CurrentExpense(null), Is.EqualTo(0.0));

            ledger.ClosePeriod();
            ledger.Income("Belasting", 10);
            ledger.ClosePeriod();

            Assert.That(ledger.Periods, Is.EqualTo(2));
            Assert.That(ledger.IncomeSeries("Belasting").Count, Is.EqualTo(2));
            Assert.That(ledger.IncomeSeries("Belasting")[0], Is.EqualTo(150f));
            Assert.That(ledger.IncomeSeries("Belasting")[1], Is.EqualTo(10f));
            Assert.That(ledger.ExpenseSeries("Diensten")[1], Is.EqualTo(0f));
            Assert.That(ledger.IncomeSeries("Diensten")[0], Is.EqualTo(0f));
            Assert.That(ledger.LastNet(), Is.EqualTo(10.0));
            Assert.That(ledger.Categories, Is.EquivalentTo(new[] { "Belasting", "Diensten" }));
            Assert.That(ledger.IncomeSeries("x"), Is.Null);
            Assert.That(ledger.ExpenseSeries(null), Is.Null);
        }

        [Test]
        public void InvalidBookingsAreRejected()
        {
            var ledger = new FinanceLedger();

            Assert.Throws<ArgumentException>(() => ledger.Income(" ", 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ledger.Expense("a", -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ledger.Expense("a", double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FinanceLedger(0));
        }
    }

    public sealed class LineStatisticsTests
    {
        [Test]
        public void ComputesOccupancyWaitProfitAndPassengers()
        {
            var stats = new LineStatistics();
            stats.RecordTrip(1, 30, 60, 90.0, 40.0);
            stats.RecordTrip(1, 90, 60, 100.0, 40.0);
            stats.RecordBoarding(1, 10, 4, 60.0);
            stats.RecordBoarding(1, 10, 6, 120.0);
            stats.RecordBoarding(1, 11, 10, 30.0);
            stats.RecordAlighting(1, 11, 7);

            Assert.That(stats.Trips(1), Is.EqualTo(2));
            Assert.That(stats.Occupancy(1), Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(stats.Profit(1), Is.EqualTo(110.0));
            Assert.That(stats.Passengers(1), Is.EqualTo(20));
            Assert.That(stats.Passengers(1, 10), Is.EqualTo(10));
            Assert.That(stats.Alighted(1, 11), Is.EqualTo(7));
            Assert.That(stats.AverageWaitSeconds(1, 10), Is.EqualTo(96.0).Within(0.001));
            Assert.That(stats.AverageWaitSeconds(1), Is.EqualTo(63.0).Within(0.001));
        }

        [Test]
        public void UnknownLinesAndStopsAreZero()
        {
            var stats = new LineStatistics();

            Assert.That(stats.Trips(9), Is.EqualTo(0));
            Assert.That(stats.Occupancy(9), Is.EqualTo(0f));
            Assert.That(stats.Passengers(9), Is.EqualTo(0));
            Assert.That(stats.Passengers(9, 1), Is.EqualTo(0));
            Assert.That(stats.Alighted(9, 1), Is.EqualTo(0));
            Assert.That(stats.AverageWaitSeconds(9), Is.EqualTo(0.0));
            Assert.That(stats.AverageWaitSeconds(9, 1), Is.EqualTo(0.0));
            Assert.That(stats.Profit(9), Is.EqualTo(0.0));
        }

        [Test]
        public void ListsByProfitAndRemovesLines()
        {
            var stats = new LineStatistics();
            stats.RecordTrip(1, 10, 10, 10.0, 50.0);
            stats.RecordTrip(2, 10, 10, 90.0, 10.0);
            stats.RecordTrip(3, 10, 10, 90.0, 10.0);
            stats.RecordBoarding(2, 5, 3, 10.0);

            Assert.That(stats.LinesByProfit(), Is.EqualTo(new[] { 2, 3, 1 }));
            Assert.That(stats.Lines, Is.EquivalentTo(new[] { 1, 2, 3 }));
            stats.Remove(2);
            Assert.That(stats.LinesByProfit(), Is.EqualTo(new[] { 3, 1 }));
            Assert.That(stats.Passengers(2, 5), Is.EqualTo(0));
        }

        [Test]
        public void InvalidInputIsRejected()
        {
            var stats = new LineStatistics();

            Assert.Throws<ArgumentOutOfRangeException>(() => stats.RecordTrip(1, -1, 10, 0.0, 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => stats.RecordTrip(1, 1, 0, 0.0, 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => stats.RecordBoarding(1, 1, -1, 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => stats.RecordBoarding(1, 1, 1, -1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => stats.RecordAlighting(1, 1, -1));
        }
    }

    public sealed class NotificationTests
    {
        [Test]
        public void RepeatedProblemsAreMergedWithinTheCooldown()
        {
            var center = new NotificationCenter(100.0);

            var first = center.Post("power", Severity.Warning, "Stroom!", 0.0, new float2(5f, 6f));
            var repeat = center.Post("power", Severity.Warning, "Stroom!", 50.0);
            var later = center.Post("power", Severity.Warning, "Stroom!", 150.0);

            Assert.That(first, Is.GreaterThan(0));
            Assert.That(repeat, Is.EqualTo(-1));
            Assert.That(later, Is.GreaterThan(first));
            Assert.That(center.Items.Count, Is.EqualTo(2));
        }

        [Test]
        public void OpeningGivesTheLocationAndMarksAsRead()
        {
            var center = new NotificationCenter();
            var located = center.Post("a", Severity.Info, "Hier", 0.0, new float2(1f, 2f));
            var citywide = center.Post("b", Severity.Info, "Overal", 0.0);

            Assert.That(center.UnreadCount, Is.EqualTo(2));
            Assert.That(center.Open(located), Is.EqualTo(new float2(1f, 2f)));
            Assert.That(center.Open(citywide), Is.Null);
            Assert.That(center.UnreadCount, Is.EqualTo(0));
            Assert.Throws<ArgumentException>(() => center.Open(999));
        }

        [Test]
        public void DismissClearAndLookUpWork()
        {
            var center = new NotificationCenter();
            var id = center.Post("a", Severity.Info, "x", 0.0);

            Assert.That(center.TryGet(id, out var found), Is.True);
            Assert.That(found.Text, Is.EqualTo("x"));
            Assert.That(center.TryGet(77, out _), Is.False);
            Assert.That(center.Dismiss(id), Is.True);
            Assert.That(center.Dismiss(id), Is.False);
            center.Post("b", Severity.Info, "y", 0.0);
            center.Clear();
            Assert.That(center.Items, Is.Empty);
        }

        [Test]
        public void FullListDropsTheLeastImportantAndImportanceSorts()
        {
            var center = new NotificationCenter(0.0);
            center.Post("crit", Severity.Critical, "kritiek", 0.0);
            for (var i = 0; i < NotificationCenter.Capacity - 1; i++)
            {
                center.Post("info" + i, Severity.Info, "info", 0.0);
            }

            center.Open(2);
            center.Post("warn", Severity.Warning, "waarschuwing", 0.0);

            Assert.That(center.Items.Count, Is.EqualTo(NotificationCenter.Capacity));
            Assert.That(center.Items[0].Key, Is.EqualTo("crit"));
            var sorted = center.ByImportance();
            Assert.That(sorted[0].Severity, Is.EqualTo(Severity.Critical));
            Assert.That(sorted[1].Severity, Is.EqualTo(Severity.Warning));
        }

        [Test]
        public void InvalidPostsAreRejected()
        {
            var center = new NotificationCenter();

            Assert.Throws<ArgumentException>(() => center.Post(" ", Severity.Info, "x", 0.0));
            Assert.Throws<ArgumentException>(() => center.Post("k", Severity.Info, " ", 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NotificationCenter(-1.0));
        }

        [Test]
        public void AdvisorsPostForEachProblemWithSeverityAndLocation()
        {
            var center = new NotificationCenter();
            var spot = new float2(40f, 50f);
            var input = new AdvisorInput(0.2f, 0.5f, 0.9f, -10.0, 1.2f, spot);

            var posted = Advisors.Evaluate(center, input, 0.0);

            Assert.That(posted, Is.EqualTo(5));
            Assert.That(center.Items.Count, Is.EqualTo(5));
            Assert.That(Advisors.Evaluate(center, input, 1.0), Is.EqualTo(0));
            foreach (var item in center.Items)
            {
                if (item.Key == "power")
                {
                    Assert.That(item.Severity, Is.EqualTo(Severity.Critical));
                }

                if (item.Key == "water")
                {
                    Assert.That(item.Severity, Is.EqualTo(Severity.Warning));
                }

                if (item.Key == "traffic")
                {
                    Assert.That(item.Location, Is.EqualTo(spot));
                }
            }
        }

        [Test]
        public void HealthyCityGetsNoAdvice()
        {
            var center = new NotificationCenter();

            Assert.That(Advisors.Evaluate(center, new AdvisorInput(0.05f, 1.2f, 1.1f, 500.0, 0.4f, null), 0.0), Is.EqualTo(0));
            Assert.Throws<ArgumentNullException>(() => Advisors.Evaluate(null, default, 0.0));
        }
    }

    public sealed class CityMessagesTests
    {
        [Test]
        public void ReactsDeterministicallyAndRateLimits()
        {
            var a = new CityMessages(5UL, 20.0);
            var b = new CityMessages(5UL, 20.0);

            Assert.That(a.React(CityEvent.Fire, 0.0, out var m1), Is.True);
            Assert.That(b.React(CityEvent.Fire, 0.0, out var m2), Is.True);
            Assert.That(m1.Text, Is.EqualTo(m2.Text));
            Assert.That(m1.Handle, Is.EqualTo(m2.Handle));
            Assert.That(a.React(CityEvent.Flood, 5.0, out _), Is.False);
            Assert.That(a.React(CityEvent.Flood, 25.0, out _), Is.True);
            Assert.That(a.Messages.Count, Is.EqualTo(2));
        }

        [Test]
        public void CanBeSwitchedOffAndIgnoresUnknownEvents()
        {
            var feed = new CityMessages(1UL, 0.0) { Enabled = false };

            Assert.That(feed.React(CityEvent.Concert, 0.0, out _), Is.False);
            feed.Enabled = true;
            Assert.That(feed.React((CityEvent)99, 0.0, out _), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityMessages(1UL, -1.0));
        }

        [Test]
        public void EveryEventHasAMessageAndTheStreamIsCapped()
        {
            var feed = new CityMessages(2UL, 0.0);
            for (var i = 0; i < CityMessages.Capacity + 20; i++)
            {
                Assert.That(feed.React((CityEvent)(i % 8), i, out var message), Is.True);
                Assert.That(message.Text, Is.Not.Empty);
                Assert.That(message.Handle, Is.Not.Empty);
            }

            Assert.That(feed.Messages.Count, Is.EqualTo(CityMessages.Capacity));
        }
    }

    public sealed class FlowVisualizerTests
    {
        [Test]
        public void TrafficTurnsRedAndSlowsDownWhenBusy()
        {
            var calm = FlowVisualizer.For(100f, 1000f, 100f, 14f, FlowMode.Traffic);
            var mid = FlowVisualizer.For(500f, 1000f, 100f, 14f, FlowMode.Traffic);
            var jam = FlowVisualizer.For(1500f, 1000f, 100f, 14f, FlowMode.Traffic);

            Assert.That(calm.Color.y, Is.GreaterThan(calm.Color.x));
            Assert.That(mid.Color.x, Is.GreaterThan(0.9f));
            Assert.That(jam.Color.x, Is.GreaterThan(jam.Color.y));
            Assert.That(jam.Load, Is.EqualTo(1.5f));
            Assert.That(jam.Speed, Is.LessThan(calm.Speed));
            Assert.That(jam.Dots, Is.GreaterThan(calm.Dots));
        }

        [Test]
        public void TransitIsBlueAndKeepsItsSpeed()
        {
            var style = FlowVisualizer.For(800f, 1000f, 200f, 10f, FlowMode.Transit);

            Assert.That(style.Color.z, Is.GreaterThan(style.Color.x));
            Assert.That(style.Speed, Is.EqualTo(10f));
        }

        [Test]
        public void EmptySegmentsHaveNoDotsAndBusyOnesAreCapped()
        {
            Assert.That(FlowVisualizer.For(0f, 1000f, 100f, 14f, FlowMode.Traffic).Dots, Is.EqualTo(0));
            Assert.That(FlowVisualizer.For(5000f, 1000f, 100_000f, 14f, FlowMode.Traffic).Dots, Is.EqualTo(FlowVisualizer.MaxDots));
            Assert.Throws<ArgumentOutOfRangeException>(() => FlowVisualizer.For(1f, 0f, 100f, 14f, FlowMode.Traffic));
            Assert.Throws<ArgumentOutOfRangeException>(() => FlowVisualizer.For(-1f, 10f, 100f, 14f, FlowMode.Traffic));
        }

        [Test]
        public void DotsMoveAlongTheSegmentAndWrap()
        {
            var style = FlowVisualizer.For(1000f, 1000f, 80f, 8f, FlowMode.Transit);
            var start = new float[style.Dots];
            var later = new float[style.Dots];

            FlowVisualizer.Offsets(style, 80f, 0f, start);
            FlowVisualizer.Offsets(style, 80f, 5f, later);

            Assert.That(style.Dots, Is.EqualTo(10));
            Assert.That(start[0], Is.EqualTo(0f));
            Assert.That(later[0], Is.EqualTo(0.5f).Within(0.0001f));
            foreach (var value in later)
            {
                Assert.That(value, Is.InRange(0f, 1f));
            }

            Assert.Throws<ArgumentNullException>(() => FlowVisualizer.Offsets(style, 80f, 0f, null));
            Assert.Throws<ArgumentException>(() => FlowVisualizer.Offsets(style, 80f, 0f, new float[1]));
        }
    }

    public sealed class SearchIndexTests
    {
        private static SearchIndex Sample()
        {
            var index = new SearchIndex();
            index.Set(new SearchEntry(1, "Brandweerkazerne Noord", "Brandweer", new float2(10f, 10f)));
            index.Set(new SearchEntry(2, "Noorderpark", "Park", new float2(20f, 20f)));
            index.Set(new SearchEntry(3, "Station Noord", "Station", new float2(30f, 30f)));
            index.Set(new SearchEntry(4, "Centrumschool", "School", new float2(40f, 40f)));
            return index;
        }

        [Test]
        public void RanksPrefixThenWordThenContains()
        {
            var results = Sample().Search("noord");

            Assert.That(results.Count, Is.EqualTo(3));
            Assert.That(results[0].Name, Is.EqualTo("Noorderpark"));
            Assert.That(results[1].Name, Is.EqualTo("Brandweerkazerne Noord"));
            Assert.That(results[2].Name, Is.EqualTo("Station Noord"));
        }

        [Test]
        public void CaseIsIgnoredAndContainsMatchesInsideWords()
        {
            var results = Sample().Search("SCHOOL");

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Id, Is.EqualTo(4));
        }

        [Test]
        public void FiltersByTypeAndMatchesTheTypeName()
        {
            var index = Sample();

            Assert.That(index.Search("noord", "park").Count, Is.EqualTo(1));
            Assert.That(index.Search("", "Station")[0].Id, Is.EqualTo(3));
            Assert.That(index.Search("station").Count, Is.EqualTo(1));
            Assert.That(index.Search("brandweer").Count, Is.EqualTo(1));
            Assert.That(index.Types(), Is.EqualTo(new[] { "Brandweer", "Park", "School", "Station" }));
        }

        [Test]
        public void EmptyQueryListsEverythingUpToTheMaximum()
        {
            var index = Sample();

            Assert.That(index.Search(null).Count, Is.EqualTo(4));
            Assert.That(index.Search("", null, 2).Count, Is.EqualTo(2));
            Assert.That(index.Search("   ")[0].Name, Is.EqualTo("Brandweerkazerne Noord"));
        }

        [Test]
        public void SetReplacesAndRemoveDeletes()
        {
            var index = Sample();
            index.Set(new SearchEntry(2, "Zuiderpark", "Park", float2.zero));

            Assert.That(index.Count, Is.EqualTo(4));
            Assert.That(index.Search("zuider").Count, Is.EqualTo(1));
            Assert.That(index.Remove(2), Is.True);
            Assert.That(index.Remove(2), Is.False);
            Assert.That(index.Count, Is.EqualTo(3));
        }

        [Test]
        public void InvalidInputIsRejected()
        {
            var index = new SearchIndex();

            Assert.Throws<ArgumentException>(() => index.Set(new SearchEntry(1, " ", "x", float2.zero)));
            Assert.Throws<ArgumentOutOfRangeException>(() => index.Search("a", null, 0));
        }
    }
}
