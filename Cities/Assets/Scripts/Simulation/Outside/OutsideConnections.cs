using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Simulation.Economy;
using Cities.Simulation.Freight;

namespace Cities.Simulation.Outside
{
    /// <summary>Voertuigen per uur die over één snelwegaansluiting de stad in rijden.</summary>
    public readonly struct TrafficFlow
    {
        public TrafficFlow(int connectionId, int vehicles)
        {
            ConnectionId = connectionId;
            Vehicles = vehicles;
        }

        public int ConnectionId { get; }

        public int Vehicles { get; }
    }

    /// <summary>
    /// De buitenverbindingen van de stad (E14-04): snelweg, spoor, haven en luchthaven aan de kaartrand. Ze bepalen hoeveel verkeer er van
    /// buiten komt, hoeveel goederen er kunnen worden ingevoerd en uitgevoerd (<see cref="OutsideTrade"/>) en hoeveel bezoekers er per dag aankomen.
    /// shortcut: of een haven aan water ligt controleert de aanroeper; de kaart kent hier alleen de rand.
    /// </summary>
    public sealed class OutsideConnections
    {
        public const int MaxConnections = 64;
        public const int MaxPerKind = 8;

        /// <summary>Voertuigen per inwoner per dag die van buiten komen of erheen gaan.</summary>
        public const float TripsPerCitizenPerDay = 0.35f;

        /// <summary>Minimale afstand in meter tussen twee verbindingen aan dezelfde rand.</summary>
        public const float MinSpacing = 100f;

        // Gemiddelde 1: nacht rustig, spits 's ochtends en 's avonds.
        private static readonly float[] DayShape =
        {
            0.2f, 0.15f, 0.1f, 0.1f, 0.2f, 0.5f, 1.1f, 1.8f, 1.8f, 1.4f, 1.2f, 1.2f,
            1.3f, 1.2f, 1.2f, 1.4f, 1.8f, 1.9f, 1.5f, 1.1f, 0.8f, 0.6f, 0.4f, 0.3f,
        };

        private readonly List<OutsideConnection> _connections = new List<OutsideConnection>();

        public OutsideConnections(float mapSize)
        {
            if (!(mapSize > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(mapSize), "De kaart heeft een positieve maat nodig.");
            }

            MapSize = mapSize;
            NextId = 1;
        }

        public float MapSize { get; }

        public int NextId { get; private set; }

        public IReadOnlyList<OutsideConnection> Connections => _connections;

        public int TotalVehiclesPerHour => _connections.Sum(c => c.VehiclesPerHour);

        /// <summary>Ton per dag die in elke richting de grens over kan.</summary>
        public float FreightTonnesPerDay => _connections.Sum(c => c.TonnesPerDay);

        /// <summary>Reizigers per dag die per trein, schip of vliegtuig aankomen; de bovengrens voor het toerisme.</summary>
        public int VisitorsPerDay => _connections.Sum(c => c.PassengersPerDay);

        public long UpkeepPerWeek => _connections.Sum(c => c.UpkeepPerWeek);

        public int Count(OutsideKind kind) => _connections.Count(c => c.Kind == kind);

        public OutsideConnection Get(int id) =>
            _connections.FirstOrDefault(c => c.Id == id) ?? throw new KeyNotFoundException("Onbekende buitenverbinding: " + id);

        /// <summary>Legt een verbinding aan, zonder te betalen. Weigert een plek buiten de rand, te dicht bij een andere en te veel van een soort.</summary>
        public OutsideConnection Add(OutsideKind kind, MapEdge edge, float offset)
        {
            Validate(kind, edge, offset);
            var connection = new OutsideConnection(NextId++, kind, edge, offset, 1, true);
            _connections.Add(connection);
            return connection;
        }

        /// <summary>Als <see cref="Add"/>, maar de bouwkosten gaan van het saldo; <c>null</c> als er geen geld is.</summary>
        public OutsideConnection TryBuild(Ledger ledger, int week, OutsideKind kind, MapEdge edge, float offset)
        {
            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            Validate(kind, edge, offset);
            return ledger.TrySpend(week, LedgerCategory.Construction, OutsideProfile.For(kind).BuildCost) ? Add(kind, edge, offset) : null;
        }

        /// <summary>Verhoogt het niveau met één, tegen de bouwkosten maal het nieuwe niveau.</summary>
        public bool TryUpgrade(Ledger ledger, int week, int id)
        {
            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            var connection = Get(id);
            if (connection.Level >= OutsideConnection.MaxLevel)
            {
                return false;
            }

            if (!ledger.TrySpend(week, LedgerCategory.Construction, connection.Profile.BuildCost * (connection.Level + 1)))
            {
                return false;
            }

            connection.Level++;
            return true;
        }

        public void SetOpen(int id, bool open) => Get(id).IsOpen = open;

        public bool Remove(int id) => _connections.RemoveAll(c => c.Id == id) > 0;

        /// <summary>Boekt het onderhoud van de open verbindingen.</summary>
        public long SettleUpkeep(Ledger ledger, int week)
        {
            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            var upkeep = UpkeepPerWeek;
            ledger.Force(week, LedgerCategory.Upkeep, upkeep);
            return upkeep;
        }

        /// <summary>
        /// Voertuigen per uur die de stad in rijden: de vraag van de inwoners volgt het dagverloop en wordt over de open snelwegen verdeeld
        /// naar capaciteit. Een aansluiting kan nooit meer aan dan haar capaciteit; de rest van de vraag valt weg.
        /// </summary>
        public IReadOnlyList<TrafficFlow> HourlyTraffic(int hour, long population)
        {
            var roads = _connections.Where(c => c.VehiclesPerHour > 0).OrderBy(c => c.Id).ToList();
            var result = new List<TrafficFlow>(roads.Count);
            var capacity = roads.Sum(c => c.VehiclesPerHour);
            if (capacity == 0)
            {
                return result;
            }

            var demand = Math.Max(0L, population) * TripsPerCitizenPerDay / 24f * DayShape[((hour % 24) + 24) % 24];
            foreach (var road in roads)
            {
                var share = demand * road.VehiclesPerHour / capacity;
                result.Add(new TrafficFlow(road.Id, (int)Math.Min(road.VehiclesPerHour, Math.Round(share))));
            }

            return result;
        }

        /// <summary>
        /// Hoeveel voertuigen er in één simulatietick bij komen, zonder bijgehouden rest: het verschil tussen twee afrondingen over het uur
        /// geeft over het hele uur precies het uurtotaal. <paramref name="tickInHour"/> loopt van 0 tot <paramref name="ticksPerHour"/>.
        /// </summary>
        public static int VehiclesInTick(int hourlyVehicles, int tickInHour, int ticksPerHour)
        {
            if (ticksPerHour <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ticksPerHour), "Een uur heeft minstens één tick.");
            }

            if (hourlyVehicles <= 0 || tickInHour < 0 || tickInHour >= ticksPerHour)
            {
                return 0;
            }

            var before = (long)hourlyVehicles * tickInHour / ticksPerHour;
            var after = (long)hourlyVehicles * (tickInHour + 1) / ticksPerHour;
            return (int)(after - before);
        }

        /// <summary>Meldt de open verbindingen met goederenvervoer als buitenknooppunt bij het goederenverkeer; geeft de knooppunt-ID's op volgorde van verbinding.</summary>
        public IReadOnlyList<int> AttachFreight(FreightNetwork network)
        {
            if (network == null)
            {
                throw new ArgumentNullException(nameof(network));
            }

            return _connections.Where(c => c.TonnesPerDay > 0f).OrderBy(c => c.Id)
                .Select(c => network.AddOutsideConnection(c.PositionOn(MapSize)).Id).ToList();
        }

        internal void Restore(IEnumerable<OutsideConnection> connections, int nextId)
        {
            _connections.Clear();
            _connections.AddRange(connections);
            NextId = nextId;
        }

        private void Validate(OutsideKind kind, MapEdge edge, float offset)
        {
            if (!Enum.IsDefined(typeof(OutsideKind), kind) || !Enum.IsDefined(typeof(MapEdge), edge))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), "Onbekende soort of rand.");
            }

            if (!(offset >= 0f && offset <= MapSize))
            {
                throw new ArgumentOutOfRangeException(nameof(offset), "De verbinding ligt buiten de kaartrand.");
            }

            if (_connections.Count >= MaxConnections || Count(kind) >= MaxPerKind)
            {
                throw new InvalidOperationException("Te veel buitenverbindingen.");
            }

            if (_connections.Any(c => c.Edge == edge && Math.Abs(c.Offset - offset) < MinSpacing))
            {
                throw new InvalidOperationException("Te dicht bij een andere buitenverbinding.");
            }
        }
    }
}
