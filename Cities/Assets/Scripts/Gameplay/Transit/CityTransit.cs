using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Simulation.Transit;
using Unity.Mathematics;

namespace Cities.Gameplay.Transit
{
    /// <summary>
    /// Openbaar vervoer van de stad (E26-08): haltes, buslijnen en bussen met ticketinkomsten. Haltes worden geplaatst, een lijn verbindt
    /// haltes (op volgorde van de dichtstbijzijnde), en wachtende reizigers komen uit de bevolking.
    /// </summary>
    public sealed class CityTransit
    {
        public const int DepotSize = 40;

        /// <summary>Wachtende reizigers per dag per 1000 inwoners, verdeeld over de haltes (begrensd door de haltecapaciteit).</summary>
        public const float PassengersPerThousandResidents = 60f;

        public CityTransit()
        {
            Network = new TransitNetwork();
            Fleet = new TransitFleet(Network);
            Finance = new TransitFinance(Network, Fleet);
            Fleet.AddDepot(DepotSize);
        }

        public TransitNetwork Network { get; }

        public TransitFleet Fleet { get; }

        public TransitFinance Finance { get; }

        public int AddStop(float2 position) => Network.AddStop(position);

        /// <summary>Haltes die nog op geen enkele lijn liggen.</summary>
        public IReadOnlyList<int> UnlinkedStops()
        {
            var used = new HashSet<int>(Network.Lines.SelectMany(l => l.Stops));
            return Network.Stops.Select(s => s.Key).Where(id => !used.Contains(id)).ToList();
        }

        /// <summary>Maakt een lijn langs alle losse haltes, op volgorde van dichtstbij; -1 bij minder dan twee haltes.</summary>
        public int CreateLineFromUnlinked(string name, int vehicles = 2, float headway = 120f)
        {
            var stops = UnlinkedStops().ToList();
            if (stops.Count < 2)
            {
                return -1;
            }

            return Network.AddLine(name, Order(stops), false, vehicles, headway);
        }

        /// <summary>Aantal voertuigen van een lijn wijzigen met <paramref name="delta"/>, begrensd op 1 tot de maximale vloot.</summary>
        public void AdjustVehicles(int lineId, int delta)
        {
            var line = Network.GetLine(lineId);
            var vehicles = Math.Max(1, Math.Min(TransitLine.MaxVehicles, line.Vehicles + delta));
            Network.Update(lineId, line.Name, line.Stops, line.IsLoop, vehicles, line.Headway);
        }

        /// <summary>Wachtende reizigers voor een dag: een deel van de bevolking meldt zich bij de haltes.</summary>
        public void Daily(int population)
        {
            var stops = Network.Stops.Select(s => s.Key).ToList();
            if (stops.Count == 0 || population <= 0)
            {
                return;
            }

            var perStop = (int)Math.Ceiling(population / 1000f * PassengersPerThousandResidents / stops.Count);
            foreach (var stop in stops)
            {
                Fleet.AddWaiting(stop, perStop);
            }
        }

        /// <summary>Positie van een bus op zijn lijn in de wereld.</summary>
        public float2 PositionOf(TransitFleet.Bus bus)
        {
            var line = Network.GetLine(bus.LineId);
            var points = line.Stops.Select(Network.PositionOf).ToList();
            if (line.IsLoop)
            {
                points.Add(points[0]);
            }

            var length = 0f;
            for (var i = 1; i < points.Count; i++)
            {
                length += math.distance(points[i - 1], points[i]);
            }

            if (length <= 0f)
            {
                return points[0];
            }

            var distance = bus.Offset;
            if (!line.IsLoop)
            {
                distance %= length * 2f;
                if (distance > length)
                {
                    distance = (length * 2f) - distance;
                }
            }
            else
            {
                distance %= length;
            }

            for (var i = 1; i < points.Count; i++)
            {
                var segment = math.distance(points[i - 1], points[i]);
                if (distance <= segment)
                {
                    return segment <= 0f ? points[i] : math.lerp(points[i - 1], points[i], distance / segment);
                }

                distance -= segment;
            }

            return points[points.Count - 1];
        }

        private List<int> Order(List<int> stops)
        {
            var ordered = new List<int> { stops[0] };
            stops.RemoveAt(0);
            while (stops.Count > 0)
            {
                var last = Network.PositionOf(ordered[ordered.Count - 1]);
                var best = 0;
                for (var i = 1; i < stops.Count; i++)
                {
                    if (math.distancesq(Network.PositionOf(stops[i]), last) < math.distancesq(Network.PositionOf(stops[best]), last))
                    {
                        best = i;
                    }
                }

                ordered.Add(stops[best]);
                stops.RemoveAt(best);
            }

            return ordered;
        }
    }
}
