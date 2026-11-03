using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

namespace Cities.Simulation.Transit
{
    /// <summary>
    /// De metrostations van de stad (E09-06): een halte uit het <see cref="TransitNetwork"/> met een perrondiepte. Een metrolijn is een gewone
    /// <see cref="TransitLine"/> waarvan alle haltes stations zijn, ver genoeg uit elkaar liggen en met een tunnel die niet te steil is.
    /// Gebruik <see cref="AddStation"/> en <see cref="AddLine"/> in plaats van de netwerkmethodes, zodat de regels van <see cref="Metro"/> gelden.
    /// </summary>
    public sealed class MetroNetwork
    {
        private readonly TransitNetwork _network;
        private readonly SortedDictionary<int, float> _depths = new SortedDictionary<int, float>();

        public MetroNetwork(TransitNetwork network)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
        }

        public TransitNetwork Network => _network;

        /// <summary>De stations (halte-ID en perrondiepte in meter), op volgorde van ID.</summary>
        public IEnumerable<KeyValuePair<int, float>> Stations => _depths;

        public bool IsStation(int stop) => _depths.ContainsKey(stop);

        public float DepthOf(int stop) =>
            _depths.TryGetValue(stop, out var depth) ? depth : throw new ArgumentException($"Halte {stop} is geen metrostation.", nameof(stop));

        /// <summary>Zet een station op <paramref name="position"/> met een perron op <paramref name="depth"/> meter onder het maaiveld.</summary>
        public int AddStation(float2 position, float depth)
        {
            if (!Metro.IsDepthOk(depth))
            {
                throw new ArgumentOutOfRangeException(nameof(depth), depth, $"Een perron ligt {Metro.MinDepth} tot {Metro.MaxDepth} m onder het maaiveld.");
            }

            var tooClose = _depths.Keys
                .Where(id => !Metro.IsSpacingOk(_network.PositionOf(id), position))
                .Select(id => (int?)id)
                .FirstOrDefault();
            if (tooClose.HasValue)
            {
                throw new ArgumentException($"Een station ligt te dicht bij station {tooClose.Value} (minimaal {Metro.MinStationSpacing} m).", nameof(position));
            }

            var stop = _network.AddStop(position);
            _depths.Add(stop, depth);
            return stop;
        }

        /// <summary>Verwijdert een station; geeft false als het niet bestaat of nog door een lijn wordt gebruikt.</summary>
        public bool TryRemoveStation(int stop)
        {
            if (!_depths.ContainsKey(stop) || !_network.TryRemoveStop(stop))
            {
                return false;
            }

            _depths.Remove(stop);
            return true;
        }

        /// <summary>Controleert of de haltes een geldige metrolijn vormen; gooit een <see cref="ArgumentException"/> met de reden.</summary>
        public void CheckLine(IReadOnlyList<int> stops)
        {
            if (stops == null)
            {
                throw new ArgumentNullException(nameof(stops));
            }

            for (var i = 0; i < stops.Count; i++)
            {
                if (!IsStation(stops[i]))
                {
                    throw new ArgumentException($"Halte {stops[i]} is geen metrostation.", nameof(stops));
                }

                if (i > 0)
                {
                    CheckTunnel(stops[i - 1], stops[i]);
                }
            }
        }

        public int AddLine(string name, IReadOnlyList<int> stops, bool isLoop, int vehicles, float headway)
        {
            CheckLine(stops);
            if (isLoop)
            {
                CheckTunnel(stops[stops.Count - 1], stops[0]);
            }

            return _network.AddLine(name, stops, isLoop, vehicles, headway);
        }

        /// <summary>De reistijd van één rit tussen twee stations van een lijn in seconden, zonder wachten.</summary>
        public float RideTime(int from, int to)
        {
            DepthOf(from);
            DepthOf(to);
            return Metro.TravelTime(math.distance(_network.PositionOf(from), _network.PositionOf(to)));
        }

        /// <summary>De extra tijd van een overstap tussen een metrostation en de straat: trap en roltrap naar het perron.</summary>
        public float AccessTime(int stop) => Metro.AccessTime(DepthOf(stop));

        public void Restore(IReadOnlyList<KeyValuePair<int, float>> stations)
        {
            _depths.Clear();
            foreach (var station in stations)
            {
                _depths.Add(station.Key, station.Value);
            }
        }

        private void CheckTunnel(int a, int b)
        {
            var distance = math.distance(_network.PositionOf(a), _network.PositionOf(b));
            if (!Metro.IsGradeOk(DepthOf(a), DepthOf(b), distance))
            {
                throw new ArgumentException($"De tunnel tussen station {a} en {b} is te steil (hoogstens {Metro.MaxGrade:P0}).");
            }
        }
    }
}
