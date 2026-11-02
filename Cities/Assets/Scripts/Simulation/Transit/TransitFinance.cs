using System;
using System.Collections.Generic;
using Cities.Simulation.Economy;

namespace Cities.Simulation.Transit
{
    /// <summary>Wat één lijn in een periode opbracht en kostte (E09-04).</summary>
    public readonly struct LineReport
    {
        public LineReport(int lineId, int boardings, long revenue, long cost)
        {
            LineId = lineId;
            Boardings = boardings;
            Revenue = revenue;
            Cost = cost;
        }

        public int LineId { get; }

        public int Boardings { get; }

        public long Revenue { get; }

        public long Cost { get; }

        public long Profit => Revenue - Cost;
    }

    /// <summary>
    /// Tickets en lijnkosten (E09-04): elke lijn heeft een eigen ticketprijs (<see cref="DefaultFare"/> tot <see cref="MaxFare"/>).
    /// Inkomsten zijn de nieuwe instappers sinds de vorige afrekening keer de prijs; de kosten zijn het onderhoud per voertuig per week.
    /// <see cref="Settle"/> boekt dat in het <see cref="Ledger"/> en geeft per lijn een <see cref="LineReport"/> voor de lijnenlijst.
    /// </summary>
    public sealed class TransitFinance
    {
        public const long DefaultFare = 2;
        public const long MaxFare = 50;

        /// <summary>Onderhoud per voertuig per week; schatting, nog niet gekalibreerd.</summary>
        public const long UpkeepPerVehiclePerWeek = 150;

        private readonly TransitNetwork _network;
        private readonly TransitFleet _fleet;
        private readonly Dictionary<int, long> _fares = new Dictionary<int, long>();
        private readonly Dictionary<int, int> _settled = new Dictionary<int, int>();

        public TransitFinance(TransitNetwork network, TransitFleet fleet)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _fleet = fleet ?? throw new ArgumentNullException(nameof(fleet));
        }

        public long FareOf(int lineId) => _fares.TryGetValue(lineId, out var fare) ? fare : DefaultFare;

        public void SetFare(int lineId, long fare)
        {
            _network.GetLine(lineId);
            if (fare < 0 || fare > MaxFare)
            {
                throw new ArgumentOutOfRangeException(nameof(fare), fare, $"De ticketprijs ligt tussen 0 en {MaxFare}.");
            }

            _fares[lineId] = fare;
        }

        /// <summary>Boekt de inkomsten en het onderhoud van alle lijnen voor deze week en geeft het overzicht per lijn.</summary>
        public IReadOnlyList<LineReport> Settle(int week, Ledger ledger)
        {
            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            var reports = new List<LineReport>();
            foreach (var line in _network.Lines)
            {
                _settled.TryGetValue(line.Id, out var before);
                var total = _fleet.Boardings(line.Id);
                var revenue = (total - before) * FareOf(line.Id);
                var cost = line.Vehicles * UpkeepPerVehiclePerWeek;
                _settled[line.Id] = total;
                ledger.Earn(week, LedgerCategory.Trade, revenue);
                ledger.Force(week, LedgerCategory.Upkeep, cost);
                reports.Add(new LineReport(line.Id, total - before, revenue, cost));
            }

            return reports;
        }
    }
}
