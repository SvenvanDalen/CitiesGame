using System;
using System.Collections.Generic;
using Cities.Core.Persistence;

namespace Cities.Gameplay.Statistics
{
    /// <summary>
    /// Ringbuffer met meetwaarden (E17-08): vaste grootte, dus geen geheugengroei. Met <see cref="SamplesPerPoint"/> groter dan 1 wordt de
    /// resolutie lager: die aantal metingen worden tot één punt gemiddeld, zodat dezelfde buffer een langere tijd beslaat.
    /// </summary>
    public sealed class HistorySeries
    {
        private readonly float[] _points;
        private int _head;
        private int _pendingCount;
        private double _pendingSum;

        public HistorySeries(int capacity, int samplesPerPoint = 1)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Een reeks bewaart minstens één punt.");
            }

            if (samplesPerPoint <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(samplesPerPoint), samplesPerPoint, "Een punt bestaat uit minstens één meting.");
            }

            _points = new float[capacity];
            SamplesPerPoint = samplesPerPoint;
        }

        public int Capacity => _points.Length;

        public int SamplesPerPoint { get; }

        public int Count { get; private set; }

        /// <summary>Oudste punt is index 0.</summary>
        public float this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), index, "Er is geen punt op deze plek.");
                }

                return _points[(_head - Count + index + _points.Length) % _points.Length];
            }
        }

        public float Last => Count == 0 ? 0f : this[Count - 1];

        public float Previous => Count < 2 ? Last : this[Count - 2];

        public float Min => Fold(float.MaxValue, Math.Min);

        public float Max => Fold(float.MinValue, Math.Max);

        public float Average
        {
            get
            {
                if (Count == 0)
                {
                    return 0f;
                }

                double sum = 0;
                for (var i = 0; i < Count; i++)
                {
                    sum += this[i];
                }

                return (float)(sum / Count);
            }
        }

        public void Add(float value)
        {
            _pendingSum += value;
            _pendingCount++;
            if (_pendingCount < SamplesPerPoint)
            {
                return;
            }

            Push((float)(_pendingSum / _pendingCount));
            _pendingSum = 0;
            _pendingCount = 0;
        }

        public void CopyTo(float[] destination)
        {
            if (destination is null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (destination.Length < Count)
            {
                throw new ArgumentException("De bestemming is te klein.", nameof(destination));
            }

            for (var i = 0; i < Count; i++)
            {
                destination[i] = this[i];
            }
        }

        internal void Restore(IReadOnlyList<float> points)
        {
            Count = 0;
            _head = 0;
            _pendingCount = 0;
            _pendingSum = 0;
            foreach (var point in points)
            {
                Push(point);
            }
        }

        private void Push(float value)
        {
            _points[_head] = value;
            _head = (_head + 1) % _points.Length;
            if (Count < _points.Length)
            {
                Count++;
            }
        }

        private float Fold(float seed, Func<float, float, float> combine)
        {
            if (Count == 0)
            {
                return 0f;
            }

            var result = seed;
            for (var i = 0; i < Count; i++)
            {
                result = combine(result, this[i]);
            }

            return result;
        }
    }

    public enum StatCategory : byte
    {
        Population,
        Economy,
        Services,
        Traffic,
    }

    public readonly struct StatRow
    {
        public StatRow(string key, string label, float current, float change)
        {
            Key = key;
            Label = label;
            Current = current;
            Change = change;
        }

        public string Key { get; }

        public string Label { get; }

        public float Current { get; }

        /// <summary>Verschil met het vorige punt; positief is stijgend.</summary>
        public float Change { get; }

        public int Trend => Math.Sign(Change);
    }

    /// <summary>
    /// Statistiekenpaneel (E17-07) op de historie van E17-08: alle reeksen van de stad per categorie (bevolking, economie, diensten, verkeer),
    /// elk met een huidige waarde, een trend en een grafiek in de tijd. Reeksen worden bij de eerste meting aangemaakt.
    /// </summary>
    public sealed class StatisticsHistory
    {
        public const int DefaultCapacity = 240;

        private readonly SortedDictionary<string, Entry> _entries = new SortedDictionary<string, Entry>(StringComparer.Ordinal);

        public StatisticsHistory(int capacity = DefaultCapacity, int samplesPerPoint = 1)
        {
            Capacity = capacity;
            SamplesPerPoint = samplesPerPoint;

            // Valideert de waarden meteen, ook als er nog geen reeks is.
            _ = new HistorySeries(capacity, samplesPerPoint);
        }

        public int Capacity { get; }

        public int SamplesPerPoint { get; }

        public int SeriesCount => _entries.Count;

        public void Record(StatCategory category, string key, string label, float value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Een statistiek heeft een sleutel.", nameof(key));
            }

            if (!_entries.TryGetValue(key, out var entry))
            {
                entry = new Entry(category, label ?? key, new HistorySeries(Capacity, SamplesPerPoint));
                _entries[key] = entry;
            }

            entry.Series.Add(value);
        }

        public HistorySeries Series(string key) => _entries.TryGetValue(key ?? string.Empty, out var entry) ? entry.Series : null;

        /// <summary>De regels van één tabblad, gesorteerd op sleutel.</summary>
        public List<StatRow> Rows(StatCategory category)
        {
            var rows = new List<StatRow>();
            foreach (var pair in _entries)
            {
                if (pair.Value.Category == category)
                {
                    var series = pair.Value.Series;
                    rows.Add(new StatRow(pair.Key, pair.Value.Label, series.Last, series.Last - series.Previous));
                }
            }

            return rows;
        }

        internal IEnumerable<KeyValuePair<string, Entry>> Entries => _entries;

        internal Entry Ensure(StatCategory category, string key, string label)
        {
            if (!_entries.TryGetValue(key, out var entry))
            {
                entry = new Entry(category, label, new HistorySeries(Capacity, SamplesPerPoint));
                _entries[key] = entry;
            }

            return entry;
        }

        internal sealed class Entry
        {
            public Entry(StatCategory category, string label, HistorySeries series)
            {
                Category = category;
                Label = label;
                Series = series;
            }

            public StatCategory Category { get; }

            public string Label { get; }

            public HistorySeries Series { get; }
        }
    }

    /// <summary>Sectie <c>HIST</c>: de punten van alle statistiekreeksen (E17-08).</summary>
    public sealed class StatisticsHistorySection : ISaveSection
    {
        public const string SectionId = "HIST";

        private readonly StatisticsHistory _history;

        public StatisticsHistorySection(StatisticsHistory history)
        {
            _history = history ?? throw new ArgumentNullException(nameof(history));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_history.SeriesCount);
            foreach (var pair in _history.Entries)
            {
                writer.Write(pair.Key);
                writer.Write(pair.Value.Label);
                writer.Write((byte)pair.Value.Category);
                var series = pair.Value.Series;
                writer.Write(series.Count);
                for (var i = 0; i < series.Count; i++)
                {
                    writer.Write(series[i]);
                }
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > 10_000)
            {
                throw reader.Corrupt($"onmogelijk aantal reeksen {count}");
            }

            var loaded = new List<(string Key, string Label, StatCategory Category, float[] Points)>(count);
            for (var i = 0; i < count; i++)
            {
                var key = reader.ReadString();
                var label = reader.ReadString();
                var category = (StatCategory)reader.ReadByte();
                var points = reader.ReadInt32();
                if (!Enum.IsDefined(typeof(StatCategory), category) || points < 0 || points > _history.Capacity || key.Length == 0)
                {
                    throw reader.Corrupt($"onmogelijke reeks {key} met {points} punten");
                }

                var values = new float[points];
                for (var p = 0; p < points; p++)
                {
                    values[p] = reader.ReadSingle();
                }

                loaded.Add((key, label, category, values));
            }

            reader.ExpectEnd();
            return () =>
            {
                foreach (var (key, label, category, points) in loaded)
                {
                    _history.Ensure(category, key, label).Series.Restore(points);
                }
            };
        }
    }

    /// <summary>
    /// Financiële grafieken (E11-11): inkomsten en uitgaven per categorie in de tijd. Binnen een periode (bijvoorbeeld een speldag) worden
    /// bedragen per categorie opgeteld; <see cref="ClosePeriod"/> zet de totalen als punt in de reeksen, zodat een grafiek per categorie en
    /// een totaaloverzicht uit dezelfde data komen.
    /// </summary>
    public sealed class FinanceLedger
    {
        private readonly Dictionary<string, double> _income = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> _expense = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly SortedDictionary<string, HistorySeries> _incomeSeries = new SortedDictionary<string, HistorySeries>(StringComparer.Ordinal);
        private readonly SortedDictionary<string, HistorySeries> _expenseSeries = new SortedDictionary<string, HistorySeries>(StringComparer.Ordinal);
        private readonly int _capacity;

        public FinanceLedger(int capacity = 120)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Een grafiek heeft minstens één punt.");
            }

            _capacity = capacity;
        }

        public int Periods { get; private set; }

        public IEnumerable<string> Categories
        {
            get
            {
                var all = new SortedSet<string>(_incomeSeries.Keys, StringComparer.Ordinal);
                all.UnionWith(_expenseSeries.Keys);
                all.UnionWith(_income.Keys);
                all.UnionWith(_expense.Keys);
                return all;
            }
        }

        public void Income(string category, double amount) => Add(_income, category, amount, "inkomsten");

        public void Expense(string category, double amount) => Add(_expense, category, amount, "uitgaven");

        /// <summary>Het bedrag dat deze periode tot nu toe is bijgeboekt.</summary>
        public double CurrentIncome(string category) => _income.TryGetValue(category ?? string.Empty, out var v) ? v : 0.0;

        public double CurrentExpense(string category) => _expense.TryGetValue(category ?? string.Empty, out var v) ? v : 0.0;

        /// <summary>Sluit de periode af; elke bekende categorie krijgt een punt, ook als er niets bijkwam.</summary>
        public void ClosePeriod()
        {
            foreach (var category in Categories)
            {
                Series(_incomeSeries, category).Add((float)CurrentIncome(category));
                Series(_expenseSeries, category).Add((float)CurrentExpense(category));
            }

            _income.Clear();
            _expense.Clear();
            Periods++;
        }

        public HistorySeries IncomeSeries(string category) => _incomeSeries.TryGetValue(category ?? string.Empty, out var s) ? s : null;

        public HistorySeries ExpenseSeries(string category) => _expenseSeries.TryGetValue(category ?? string.Empty, out var s) ? s : null;

        /// <summary>Saldo (inkomsten min uitgaven) van de afgelopen periode over alle categorieën.</summary>
        public double LastNet()
        {
            double net = 0;
            foreach (var series in _incomeSeries.Values)
            {
                net += series.Last;
            }

            foreach (var series in _expenseSeries.Values)
            {
                net -= series.Last;
            }

            return net;
        }

        private static void Add(Dictionary<string, double> target, string category, double amount, string what)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                throw new ArgumentException("Een boeking heeft een categorie.", nameof(category));
            }

            if (!(amount >= 0.0) || double.IsInfinity(amount))
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, $"Bedragen bij {what} zijn nul of meer.");
            }

            target[category] = (target.TryGetValue(category, out var current) ? current : 0.0) + amount;
        }

        private HistorySeries Series(SortedDictionary<string, HistorySeries> all, string category)
        {
            if (!all.TryGetValue(category, out var series))
            {
                series = new HistorySeries(_capacity);
                all[category] = series;
            }

            return series;
        }
    }

    /// <summary>
    /// Lijnstatistieken (E09-12): bezetting, wachttijd, winst en passagiersaantallen per lijn en per halte. Ritten leveren passagiers, plaatsen,
    /// opbrengst en kosten; haltes leveren instappers, uitstappers en wachttijden.
    /// </summary>
    public sealed class LineStatistics
    {
        private readonly Dictionary<int, LineTotals> _lines = new Dictionary<int, LineTotals>();
        private readonly Dictionary<(int Line, int Stop), StopTotals> _stops = new Dictionary<(int, int), StopTotals>();

        public IEnumerable<int> Lines => _lines.Keys;

        /// <summary>Eén rit van een voertuig: gemiddeld aantal passagiers aan boord, plaatsen, opbrengst en kosten van die rit.</summary>
        public void RecordTrip(int line, int passengers, int capacity, double revenue, double cost)
        {
            if (passengers < 0 || capacity <= 0 || revenue < 0.0 || cost < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(passengers), "Passagiers, opbrengst en kosten zijn niet negatief en er zijn plaatsen.");
            }

            var totals = Line(line);
            totals.Trips++;
            totals.PassengerSeats += Math.Min(passengers, capacity);
            totals.Seats += capacity;
            totals.Revenue += revenue;
            totals.Cost += cost;
        }

        public void RecordBoarding(int line, int stop, int count, double waitSeconds)
        {
            if (count < 0 || waitSeconds < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Aantal en wachttijd zijn niet negatief.");
            }

            var totals = Stop(line, stop);
            totals.Boarded += count;
            totals.WaitSum += waitSeconds * count;
            Line(line).Boarded += count;
            Line(line).WaitSum += waitSeconds * count;
        }

        public void RecordAlighting(int line, int stop, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "Aantal is niet negatief.");
            }

            Stop(line, stop).Alighted += count;
            Line(line).Alighted += count;
        }

        public int Trips(int line) => _lines.TryGetValue(line, out var t) ? t.Trips : 0;

        /// <summary>Bezettingsgraad van de plaatsen, 0 tot 1.</summary>
        public float Occupancy(int line) => _lines.TryGetValue(line, out var t) && t.Seats > 0 ? (float)((double)t.PassengerSeats / t.Seats) : 0f;

        public int Passengers(int line) => _lines.TryGetValue(line, out var t) ? t.Boarded : 0;

        public int Passengers(int line, int stop) => _stops.TryGetValue((line, stop), out var t) ? t.Boarded : 0;

        public int Alighted(int line, int stop) => _stops.TryGetValue((line, stop), out var t) ? t.Alighted : 0;

        public double AverageWaitSeconds(int line) => _lines.TryGetValue(line, out var t) && t.Boarded > 0 ? t.WaitSum / t.Boarded : 0.0;

        public double AverageWaitSeconds(int line, int stop) => _stops.TryGetValue((line, stop), out var t) && t.Boarded > 0 ? t.WaitSum / t.Boarded : 0.0;

        public double Profit(int line) => _lines.TryGetValue(line, out var t) ? t.Revenue - t.Cost : 0.0;

        /// <summary>Lijnen van de winstgevendste naar de duurste, voor een lijstweergave.</summary>
        public List<int> LinesByProfit()
        {
            var lines = new List<int>(_lines.Keys);
            lines.Sort((a, b) =>
            {
                var compare = Profit(b).CompareTo(Profit(a));
                return compare != 0 ? compare : a.CompareTo(b);
            });
            return lines;
        }

        /// <summary>Wis alle cijfers van een lijn, bijvoorbeeld als hij wordt verwijderd.</summary>
        public void Remove(int line)
        {
            _lines.Remove(line);
            var stops = new List<(int Line, int Stop)>();
            foreach (var key in _stops.Keys)
            {
                if (key.Line == line)
                {
                    stops.Add(key);
                }
            }

            foreach (var key in stops)
            {
                _stops.Remove(key);
            }
        }

        private LineTotals Line(int line)
        {
            if (!_lines.TryGetValue(line, out var totals))
            {
                totals = new LineTotals();
                _lines[line] = totals;
            }

            return totals;
        }

        private StopTotals Stop(int line, int stop)
        {
            if (!_stops.TryGetValue((line, stop), out var totals))
            {
                totals = new StopTotals();
                _stops[(line, stop)] = totals;
            }

            return totals;
        }

        private sealed class LineTotals
        {
            public int Trips { get; set; }
            public long PassengerSeats { get; set; }
            public long Seats { get; set; }
            public int Boarded { get; set; }
            public int Alighted { get; set; }
            public double WaitSum { get; set; }
            public double Revenue { get; set; }
            public double Cost { get; set; }
        }

        private sealed class StopTotals
        {
            public int Boarded { get; set; }
            public int Alighted { get; set; }
            public double WaitSum { get; set; }
        }
    }
}
