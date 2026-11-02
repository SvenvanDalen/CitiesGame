using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

namespace Cities.Simulation.Transit
{
    /// <summary>
    /// De bussen van het openbaar vervoer (E09-02): depots brengen bussen in dienst, een bus rijdt zijn lijn (heen en terug of een ronde),
    /// stopt bij elke halte, laat passagiers uitstappen en laadt wachtende in tot de bus vol is. Een busstation is een halte met een grotere
    /// wachtruimte. shortcut: bussen rijden hemelsbreed met een vaste snelheid en een passagier stapt een paar haltes verder uit; dat wordt
    /// echte routekeuze zodra de lijnen over het wegennet lopen en burgers een reis plannen (E08 en E07).
    /// </summary>
    public sealed class TransitFleet
    {
        public const float Speed = 10f;
        public const int BusCapacity = 60;
        public const int StopCapacity = 50;
        public const int StationCapacity = 250;
        public const float MinDwell = 5f;
        public const float DwellPerPassenger = 0.5f;

        private const int RideStops = 3;

        private readonly TransitNetwork _network;
        private readonly List<int> _depots = new List<int>();
        private readonly HashSet<int> _stations = new HashSet<int>();
        private readonly Dictionary<int, int> _waiting = new Dictionary<int, int>();
        private readonly Dictionary<int, float> _sinceSpawn = new Dictionary<int, float>();
        private readonly List<Bus> _buses = new List<Bus>();

        public TransitFleet(TransitNetwork network)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
        }

        public IReadOnlyList<Bus> Buses => _buses;

        /// <summary>Aantal passagiers dat op zijn bestemming is uitgestapt.</summary>
        public int Delivered { get; private set; }

        public int DepotCapacity => _depots.Sum();

        public void AddDepot(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Een depot heeft plaats voor minstens één bus.");
            }

            _depots.Add(capacity);
        }

        public void SetStation(int stop)
        {
            _network.PositionOf(stop);
            _stations.Add(stop);
        }

        public int Waiting(int stop) => _waiting.TryGetValue(stop, out var count) ? count : 0;

        /// <summary>Zet wachtende reizigers neer; geeft terug hoeveel er pasten.</summary>
        public int AddWaiting(int stop, int count)
        {
            _network.PositionOf(stop);
            var room = (_stations.Contains(stop) ? StationCapacity : StopCapacity) - Waiting(stop);
            var added = math.clamp(count, 0, math.max(room, 0));
            _waiting[stop] = Waiting(stop) + added;
            return added;
        }

        public void Tick(float dt)
        {
            if (!(dt >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(dt), dt, "De tijd kan niet negatief zijn.");
            }

            var lines = _network.Lines.ToList();
            // een bus op een verwijderde lijn gaat terug naar het depot
            _buses.RemoveAll(b => lines.All(l => l.Id != b.LineId));
            foreach (var line in lines)
            {
                SpawnFor(line, dt);
            }

            foreach (var bus in _buses)
            {
                Drive(bus, dt);
            }
        }

        private void SpawnFor(TransitLine line, float dt)
        {
            _sinceSpawn.TryGetValue(line.Id, out var since);
            since += dt;
            var onLine = _buses.Count(b => b.LineId == line.Id);
            if (onLine < line.Vehicles && _buses.Count < DepotCapacity && since >= line.Headway)
            {
                _buses.Add(new Bus(line.Id));
                since = 0f;
            }

            _sinceSpawn[line.Id] = since;
        }

        private void Drive(Bus bus, float dt)
        {
            var visits = Visits(_network.GetLine(bus.LineId), out var cycle);
            var left = dt;
            while (left > 0f)
            {
                if (bus.Dwell > 0f)
                {
                    var used = math.min(bus.Dwell, left);
                    bus.Dwell -= used;
                    left -= used;
                    continue;
                }

                var target = bus.Next < visits.Count ? visits[bus.Next].Offset : cycle;
                var gap = target - bus.Offset;
                if (gap > left * Speed)
                {
                    bus.Offset += left * Speed;
                    return;
                }

                left -= math.max(gap, 0f) / Speed;
                bus.Offset = target;
                if (bus.Next >= visits.Count)
                {
                    bus.Offset -= cycle;
                    bus.Next = 0;
                    continue;
                }

                Arrive(bus, visits);
                bus.Next++;
            }
        }

        private void Arrive(Bus bus, List<Visit> visits)
        {
            var stop = visits[bus.Next].Stop;
            var alighted = bus.Alight(stop);
            Delivered += alighted;
            var take = math.min(Waiting(stop), BusCapacity - bus.Count);
            if (take > 0)
            {
                var destination = visits[(bus.Next + RideStops) % visits.Count].Stop;
                if (destination == stop)
                {
                    destination = visits[(bus.Next + 1) % visits.Count].Stop;
                }

                bus.Board(destination, take);
                _waiting[stop] = Waiting(stop) - take;
            }

            bus.Dwell = MinDwell + (DwellPerPassenger * (alighted + take));
        }

        /// <summary>De haltes in de volgorde waarin een bus ze aandoet, met hun afstand vanaf het begin van de cyclus.</summary>
        private List<Visit> Visits(TransitLine line, out float cycle)
        {
            var order = line.Stops.ToList();
            if (!line.IsLoop)
            {
                order.AddRange(line.Stops.Reverse().Skip(1).Take(line.Stops.Count - 2));
            }

            var visits = new List<Visit>();
            var offset = 0f;
            for (var i = 0; i < order.Count; i++)
            {
                if (i > 0)
                {
                    offset += math.distance(_network.PositionOf(order[i - 1]), _network.PositionOf(order[i]));
                }

                visits.Add(new Visit(order[i], offset));
            }

            cycle = offset + math.distance(_network.PositionOf(order[order.Count - 1]), _network.PositionOf(order[0]));
            return visits;
        }

        private readonly struct Visit
        {
            public Visit(int stop, float offset)
            {
                Stop = stop;
                Offset = offset;
            }

            public int Stop { get; }

            public float Offset { get; }
        }

        public sealed class Bus
        {
            private readonly Dictionary<int, int> _onboard = new Dictionary<int, int>();

            public Bus(int lineId)
            {
                LineId = lineId;
            }

            public int LineId { get; }

            /// <summary>Afgelegde afstand in de huidige cyclus van de lijn.</summary>
            public float Offset { get; internal set; }

            internal int Next { get; set; }

            /// <summary>Resterende stoptijd bij een halte in seconden.</summary>
            public float Dwell { get; internal set; }

            public int Count => _onboard.Values.Sum();

            internal int Alight(int stop)
            {
                var n = _onboard.TryGetValue(stop, out var count) ? count : 0;
                _onboard.Remove(stop);
                return n;
            }

            internal void Board(int destination, int count) =>
                _onboard[destination] = (_onboard.TryGetValue(destination, out var had) ? had : 0) + count;
        }
    }
}
