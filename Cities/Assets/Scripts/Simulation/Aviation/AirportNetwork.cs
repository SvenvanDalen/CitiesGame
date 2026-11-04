using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Simulation.Fields;
using Unity.Mathematics;

namespace Cities.Simulation.Aviation
{
    /// <summary>Wat een luchthaven tot nu toe deed: de vraag, en de opgetelde passagiers en bewegingen.</summary>
    public readonly struct AirportStats
    {
        public AirportStats(float demand, double passengers, double movements)
        {
            Demand = demand;
            Passengers = passengers;
            Movements = movements;
        }

        /// <summary>Gevraagde passagiers per uur.</summary>
        public float Demand { get; }

        /// <summary>Totaal vervoerde passagiers.</summary>
        public double Passengers { get; }

        /// <summary>Totaal aantal landingen en starts.</summary>
        public double Movements { get; }
    }

    /// <summary>
    /// De luchthavens van de stad (E09-09). Een luchthaven moet ver genoeg van een andere liggen (<see cref="AviationRules.MinAirportSpacing"/>).
    /// <see cref="Step"/> laat de luchthavens passagiers verwerken, begrensd door baan, gates en terminal; <see cref="NoiseSources"/> geeft het
    /// bijbehorende lawaai voor het <see cref="NoiseModel"/>.
    /// </summary>
    public sealed class AirportNetwork
    {
        public const int MaxAirports = 1000;

        private readonly SortedDictionary<int, Airport> _airports = new SortedDictionary<int, Airport>();
        private readonly Dictionary<int, AirportStats> _stats = new Dictionary<int, AirportStats>();
        private int _nextId = 1;

        public IEnumerable<Airport> Airports => _airports.Values;

        public int NextId => _nextId;

        public int Add(string name, float2 position, float heading, float runwayLength, float terminalCapacity, IReadOnlyList<AircraftClass> gates)
        {
            if (_airports.Count >= MaxAirports)
            {
                throw new InvalidOperationException($"Er kunnen niet meer dan {MaxAirports} luchthavens zijn.");
            }

            var airport = new Airport(_nextId, name, position, heading, runwayLength, terminalCapacity, gates);
            var tooClose = _airports.Values
                .Where(o => math.distance(o.Position, position) < AviationRules.MinAirportSpacing)
                .Select(o => (int?)o.Id)
                .FirstOrDefault();
            if (tooClose.HasValue)
            {
                throw new ArgumentException($"Een luchthaven ligt te dicht bij luchthaven {tooClose.Value} (minimaal {AviationRules.MinAirportSpacing} m).", nameof(position));
            }

            _airports.Add(airport.Id, airport);
            _stats.Add(airport.Id, default);
            return _nextId++;
        }

        public Airport Get(int id) =>
            _airports.TryGetValue(id, out var airport) ? airport : throw new ArgumentException($"Onbekende luchthaven {id}.", nameof(id));

        public bool Remove(int id) => _airports.Remove(id) && _stats.Remove(id);

        /// <summary>Vervangt baan, terminal en gates van een luchthaven (bijvoorbeeld een verlengde baan); bij een fout blijft alles zoals het was.</summary>
        public void Update(int id, float runwayLength, float terminalCapacity, IReadOnlyList<AircraftClass> gates)
        {
            var old = Get(id);
            _airports[id] = new Airport(id, old.Name, old.Position, old.Heading, runwayLength, terminalCapacity, gates);
        }

        public AirportStats StatsOf(int id)
        {
            Get(id);
            return _stats[id];
        }

        /// <summary>Stelt de gevraagde passagiers per uur in; de luchthaven verwerkt hooguit zijn capaciteit.</summary>
        public void SetDemand(int id, float passengersPerHour)
        {
            if (!(passengersPerHour >= 0f) || float.IsInfinity(passengersPerHour))
            {
                throw new ArgumentOutOfRangeException(nameof(passengersPerHour), passengersPerHour, "De vraag kan niet negatief zijn.");
            }

            var stats = StatsOf(id);
            _stats[id] = new AirportStats(passengersPerHour, stats.Passengers, stats.Movements);
        }

        /// <summary>Benutting 0 tot 1 van een luchthaven bij zijn huidige vraag.</summary>
        public float Utilisation(int id) => Get(id).Utilisation(StatsOf(id).Demand);

        /// <summary>Laat <paramref name="seconds"/> seconden verstrijken: elke luchthaven verwerkt zijn vraag, begrensd door zijn capaciteit.</summary>
        public void Step(float seconds)
        {
            if (!(seconds >= 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "De tijd kan niet negatief zijn.");
            }

            foreach (var airport in _airports.Values)
            {
                var stats = _stats[airport.Id];
                var served = math.min(stats.Demand, airport.PassengersPerHour) * (double)seconds / 3600.0;
                var movements = served / airport.AveragePassengersPerMovement;
                _stats[airport.Id] = new AirportStats(stats.Demand, stats.Passengers + served, stats.Movements + movements);
            }
        }

        /// <summary>De lawaaibronnen van alle luchthavens, om in <see cref="NoiseModel"/> te voeren.</summary>
        public NoiseSource[] NoiseSources() =>
            _airports.Values.SelectMany(a => AirportNoise.Sources(a, a.Utilisation(_stats[a.Id].Demand))).ToArray();

        public void Restore(IReadOnlyList<KeyValuePair<Airport, AirportStats>> airports, int nextId)
        {
            _airports.Clear();
            _stats.Clear();
            foreach (var entry in airports)
            {
                _airports.Add(entry.Key.Id, entry.Key);
                _stats.Add(entry.Key.Id, entry.Value);
            }

            _nextId = nextId;
        }

        /// <summary>Controleert of de luchthavens elkaar niet te dicht naderen; gooit een <see cref="ArgumentException"/> met de reden.</summary>
        public static void CheckSpacing(IReadOnlyList<Airport> airports)
        {
            for (var i = 0; i < airports.Count; i++)
            {
                for (var j = i + 1; j < airports.Count; j++)
                {
                    if (math.distance(airports[i].Position, airports[j].Position) < AviationRules.MinAirportSpacing)
                    {
                        throw new ArgumentException($"Luchthaven {airports[i].Id} ligt te dicht bij {airports[j].Id}.");
                    }
                }
            }
        }
    }
}
