using System;
using System.Collections.Generic;
using Cities.Simulation.Economy;
using Cities.Simulation.Network;
using Unity.Mathematics;

namespace Cities.Simulation.Bikes
{
    /// <summary>Een verhuurpunt met een aantal dockingplaatsen en de fietsen die erin staan (E09-10).</summary>
    public sealed class BikeStation
    {
        internal BikeStation(int id, float3 position, int capacity, int bikes)
        {
            Id = id;
            Position = position;
            Capacity = capacity;
            Bikes = bikes;
        }

        public int Id { get; }

        public float3 Position { get; }

        public int Capacity { get; }

        public int Bikes { get; internal set; }

        public int FreeDocks => Capacity - Bikes;
    }

    /// <summary>Een voorgestelde deelfietsrit: lopen naar een verhuurpunt, fietsen naar een tweede en lopen naar het doel.</summary>
    public readonly struct BikeShareTrip
    {
        public BikeShareTrip(int fromStation, int toStation, float walkIn, float ride, float walkOut)
        {
            Found = true;
            FromStation = fromStation;
            ToStation = toStation;
            WalkIn = walkIn;
            Ride = ride;
            WalkOut = walkOut;
        }

        public bool Found { get; }

        public int FromStation { get; }

        public int ToStation { get; }

        /// <summary>Afstanden in meters (hemelsbreed).</summary>
        public float WalkIn { get; }

        public float Ride { get; }

        public float WalkOut { get; }

        public float Seconds => ((WalkIn + WalkOut) / PathNetwork.WalkSpeed) + (Ride / PathNetwork.BikeSpeed);
    }

    /// <summary>
    /// Deelfietsen (E09-10): verhuurpunten met dockingplaatsen. Een fiets pakken kan als er een staat, terugbrengen als er een plek vrij is.
    /// Een rit kost een opstaptarief plus een bedrag per begonnen tijdvak; <see cref="Settle"/> boekt de inkomsten en de kosten voor
    /// punten, fietsen en herverdeling in het <see cref="Ledger"/>. <see cref="Rebalance"/> brengt fietsen van volle naar lege punten
    /// (met een bus), tegen een prijs per fiets. Alle bedragen zijn schattingen.
    /// </summary>
    public sealed class BikeShare
    {
        public const int MaxCapacity = 60;
        public const int MaxStations = 5000;

        public const long UnlockFee = 1;
        public const long FarePerBlock = 1;

        /// <summary>Lengte van een tariefblok in seconden.</summary>
        public const float BlockSeconds = 600f;

        public const long StationUpkeepPerWeek = 40;
        public const long BikeUpkeepPerWeek = 3;
        public const long RebalanceCostPerBike = 2;

        /// <summary>Verste afstand die iemand wil lopen naar of van een verhuurpunt, in meters.</summary>
        public const float MaxWalkToStation = 400f;

        private readonly List<BikeStation> _stations = new List<BikeStation>();

        public IReadOnlyList<BikeStation> Stations => _stations;

        public int NextId { get; private set; } = 1;

        /// <summary>Fietsen die nu verhuurd zijn en dus niet in een punt staan.</summary>
        public int InUse { get; private set; }

        /// <summary>Nog niet afgerekende inkomsten en herverdelingskosten.</summary>
        public long PendingRevenue { get; private set; }

        public long PendingRebalanceCost { get; private set; }

        public long TotalRentals { get; private set; }

        public int TotalBikes
        {
            get
            {
                var total = InUse;
                foreach (var station in _stations)
                {
                    total += station.Bikes;
                }

                return total;
            }
        }

        public int AddStation(float3 position, int capacity, int bikes)
        {
            if (capacity < 1 || capacity > MaxCapacity)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, $"Een verhuurpunt heeft 1 tot {MaxCapacity} plaatsen.");
            }

            if (bikes < 0 || bikes > capacity)
            {
                throw new ArgumentOutOfRangeException(nameof(bikes), bikes, "Het aantal fietsen past niet in het punt.");
            }

            if (_stations.Count >= MaxStations)
            {
                throw new InvalidOperationException("Er zijn te veel verhuurpunten.");
            }

            var station = new BikeStation(NextId++, position, capacity, bikes);
            _stations.Add(station);
            return station.Id;
        }

        public BikeStation Get(int id)
        {
            var index = IndexOf(id);
            if (index < 0)
            {
                throw new ArgumentException($"Onbekend verhuurpunt {id}.", nameof(id));
            }

            return _stations[index];
        }

        /// <summary>Haalt een punt weg; fietsen die er stonden verdwijnen uit de vloot (zij worden verkocht, zonder opbrengst).</summary>
        public bool RemoveStation(int id)
        {
            var index = IndexOf(id);
            if (index < 0)
            {
                return false;
            }

            _stations.RemoveAt(index);
            return true;
        }

        /// <summary>Pakt een fiets uit het punt. Onwaar als er geen staat.</summary>
        public bool Rent(int stationId)
        {
            var station = Get(stationId);
            if (station.Bikes == 0)
            {
                return false;
            }

            station.Bikes--;
            InUse++;
            TotalRentals++;
            return true;
        }

        /// <summary>Brengt een fiets terug na <paramref name="seconds"/> rijden en geeft het tarief. Onwaar (tarief 0) als er geen plek vrij is of niets verhuurd is.</summary>
        public bool TryReturn(int stationId, float seconds, out long fare)
        {
            var station = Get(stationId);
            fare = 0;
            if (InUse == 0 || station.FreeDocks == 0)
            {
                return false;
            }

            fare = Fare(seconds);
            station.Bikes++;
            InUse--;
            PendingRevenue += fare;
            return true;
        }

        /// <summary>Opstaptarief plus een bedrag per begonnen tariefblok.</summary>
        public static long Fare(float seconds) => UnlockFee + ((long)Math.Ceiling(Math.Max(seconds, 0f) / BlockSeconds) * FarePerBlock);

        /// <summary>Het dichtstbijzijnde punt binnen <paramref name="maxDistance"/> dat een fiets heeft (of een plek, bij <paramref name="needDock"/>); -1 als er geen is.</summary>
        public int FindNearest(float3 position, bool needDock, float maxDistance = MaxWalkToStation)
        {
            var best = -1;
            var bestDistance = maxDistance;
            foreach (var station in _stations)
            {
                if ((needDock ? station.FreeDocks : station.Bikes) == 0)
                {
                    continue;
                }

                var distance = math.distance(station.Position, position);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = station.Id;
                }
            }

            return best;
        }

        /// <summary>Bedenkt een rit van <paramref name="origin"/> naar <paramref name="destination"/>; <c>Found</c> is onwaar als begin of eind geen punt in de buurt heeft.</summary>
        public BikeShareTrip Plan(float3 origin, float3 destination)
        {
            var from = FindNearest(origin, needDock: false);
            var to = FindNearest(destination, needDock: true);
            if (from < 0 || to < 0 || from == to)
            {
                return default;
            }

            var a = Get(from).Position;
            var b = Get(to).Position;
            return new BikeShareTrip(from, to, math.distance(origin, a), math.distance(a, b), math.distance(b, destination));
        }

        /// <summary>
        /// Verdeelt fietsen opnieuw: elk punt streeft naar de helft vol. Fietsen gaan van het volste punt naar het leegste, in vaste volgorde
        /// van id bij gelijke stand. Geeft het aantal verplaatste fietsen; de kosten komen bij <see cref="PendingRebalanceCost"/>.
        /// </summary>
        public int Rebalance()
        {
            var moved = 0;
            while (MoveOne())
            {
                moved++;
            }

            PendingRebalanceCost += moved * RebalanceCostPerBike;
            return moved;
        }

        /// <summary>Boekt de opbrengst en de kosten sinds de vorige afrekening in <paramref name="week"/> en geeft het saldo van deze afrekening.</summary>
        public long Settle(int week, Ledger ledger)
        {
            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            var cost = (_stations.Count * StationUpkeepPerWeek) + (TotalBikes * BikeUpkeepPerWeek) + PendingRebalanceCost;
            var revenue = PendingRevenue;
            ledger.Earn(week, LedgerCategory.Trade, revenue);
            ledger.Force(week, LedgerCategory.Upkeep, cost);
            PendingRevenue = 0;
            PendingRebalanceCost = 0;
            return revenue - cost;
        }

        internal static BikeStation CreateStation(int id, float3 position, int capacity, int bikes) => new BikeStation(id, position, capacity, bikes);

        internal void Restore(int nextId, int inUse, long revenue, long rebalanceCost, long rentals, List<BikeStation> stations)
        {
            _stations.Clear();
            _stations.AddRange(stations);
            NextId = nextId;
            InUse = inUse;
            PendingRevenue = revenue;
            PendingRebalanceCost = rebalanceCost;
            TotalRentals = rentals;
        }

        private static int Surplus(BikeStation station) => station.Bikes - (station.Capacity / 2);

        /// <summary>Verplaatst één fiets van het punt met het grootste overschot naar dat met het grootste tekort; onwaar als dat niet kan.</summary>
        private bool MoveOne()
        {
            BikeStation most = null;
            BikeStation least = null;
            foreach (var station in _stations)
            {
                if (most == null || Surplus(station) > Surplus(most))
                {
                    most = station;
                }

                if (least == null || Surplus(station) < Surplus(least))
                {
                    least = station;
                }
            }

            if (most == null || Surplus(most) <= 0 || Surplus(least) >= 0)
            {
                return false;
            }

            most.Bikes--;
            least.Bikes++;
            return true;
        }

        private int IndexOf(int id)
        {
            for (var i = 0; i < _stations.Count; i++)
            {
                if (_stations[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
