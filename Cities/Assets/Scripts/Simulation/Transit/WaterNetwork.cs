using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

namespace Cities.Simulation.Transit
{
    /// <summary>Een haven (E09-08): een halte uit het <see cref="TransitNetwork"/> met een soort en de waterdiepte bij de kade.</summary>
    public sealed class Port
    {
        public Port(int stop, VesselKind kind, float depth)
        {
            Stop = stop;
            Kind = kind;
            Depth = depth;
        }

        public int Stop { get; }

        public VesselKind Kind { get; }

        /// <summary>Waterdiepte bij de kade in meter.</summary>
        public float Depth { get; }
    }

    /// <summary>Een kanaal tussen twee havens (E09-08) met breedte en diepte in meter.</summary>
    public sealed class Canal
    {
        public Canal(int id, int from, int to, float width, float depth)
        {
            Id = id;
            From = from;
            To = to;
            Width = width;
            Depth = depth;
        }

        public int Id { get; }

        public int From { get; }

        public int To { get; }

        public float Width { get; }

        public float Depth { get; }

        public bool Connects(int a, int b) => (From == a && To == b) || (From == b && To == a);
    }

    /// <summary>
    /// De havens, kanalen en vaarlijnen van de stad (E09-08). Een haven is een halte van het <see cref="TransitNetwork"/>; een veer- of
    /// vrachtlijn is een gewone <see cref="TransitLine"/> waarvan alle haltes havens van dezelfde soort zijn. Elk stuk van de lijn moet
    /// bevaarbaar zijn: over een kanaal tussen de twee havens, of anders over open water dat overal diep genoeg is volgens de
    /// <see cref="IWaterDepthSource"/>. Gebruik <see cref="AddPort"/>, <see cref="AddCanal"/> en <see cref="AddLine"/> zodat de regels van
    /// <see cref="WaterTraffic"/> gelden.
    /// </summary>
    public sealed class WaterNetwork
    {
        private readonly TransitNetwork _network;
        private readonly IWaterDepthSource _water;
        private readonly SortedDictionary<int, Port> _ports = new SortedDictionary<int, Port>();
        private readonly SortedDictionary<int, Canal> _canals = new SortedDictionary<int, Canal>();
        private int _nextCanal = 1;

        public WaterNetwork(TransitNetwork network, IWaterDepthSource water)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _water = water ?? throw new ArgumentNullException(nameof(water));
        }

        public TransitNetwork Network => _network;

        public IEnumerable<Port> Ports => _ports.Values;

        public IEnumerable<Canal> Canals => _canals.Values;

        public int NextCanalId => _nextCanal;

        public bool IsPort(int stop) => _ports.ContainsKey(stop);

        public Port GetPort(int stop) =>
            _ports.TryGetValue(stop, out var port) ? port : throw new ArgumentException($"Halte {stop} is geen haven.", nameof(stop));

        public Canal GetCanal(int id) =>
            _canals.TryGetValue(id, out var canal) ? canal : throw new ArgumentException($"Kanaal {id} bestaat niet.", nameof(id));

        /// <summary>Zet een haven van de gegeven soort op <paramref name="position"/>; het water daar moet diep genoeg zijn.</summary>
        public int AddPort(float2 position, VesselKind kind)
        {
            var depth = _water.DepthAt(position);
            if (!WaterTraffic.IsDepthOk(kind, depth))
            {
                throw new ArgumentException($"Het water is te ondiep voor een haven ({depth} m, nodig {WaterTraffic.RequiredDepth(kind)} m).", nameof(position));
            }

            var tooClose = _ports.Keys
                .Where(id => !WaterTraffic.IsSpacingOk(_network.PositionOf(id), position))
                .Select(id => (int?)id)
                .FirstOrDefault();
            if (tooClose.HasValue)
            {
                throw new ArgumentException($"Een haven ligt te dicht bij haven {tooClose.Value} (minimaal {WaterTraffic.MinPortSpacing} m).", nameof(position));
            }

            var stop = _network.AddStop(position);
            _ports.Add(stop, new Port(stop, kind, depth));
            return stop;
        }

        /// <summary>Verwijdert een haven; geeft false als ze niet bestaat, nog door een lijn of door een kanaal wordt gebruikt.</summary>
        public bool TryRemovePort(int stop)
        {
            if (!_ports.ContainsKey(stop) || _canals.Values.Any(c => c.From == stop || c.To == stop) || !_network.TryRemoveStop(stop))
            {
                return false;
            }

            _ports.Remove(stop);
            return true;
        }

        /// <summary>Graaft een kanaal tussen twee havens.</summary>
        public int AddCanal(int from, int to, float width, float depth)
        {
            GetPort(from);
            GetPort(to);
            if (from == to)
            {
                throw new ArgumentException("Een kanaal verbindt twee verschillende havens.", nameof(to));
            }

            if (!WaterTraffic.IsCanalWidthOk(width))
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, $"Een kanaal is {WaterTraffic.MinCanalWidth} tot {WaterTraffic.MaxCanalWidth} m breed.");
            }

            if (!WaterTraffic.IsCanalDepthOk(depth))
            {
                throw new ArgumentOutOfRangeException(nameof(depth), depth, $"Een kanaal is hoogstens {WaterTraffic.MaxCanalDepth} m diep.");
            }

            if (_canals.Values.Any(c => c.Connects(from, to)))
            {
                throw new ArgumentException("Tussen deze havens ligt al een kanaal.", nameof(to));
            }

            var id = _nextCanal++;
            _canals.Add(id, new Canal(id, from, to, width, depth));
            return id;
        }

        public bool RemoveCanal(int id) => _canals.Remove(id);

        /// <summary>Of een schip van deze soort rechtstreeks van haven <paramref name="a"/> naar <paramref name="b"/> kan varen.</summary>
        public bool IsNavigable(int a, int b, VesselKind kind)
        {
            var canal = _canals.Values.FirstOrDefault(c => c.Connects(a, b));
            if (canal != null)
            {
                return WaterTraffic.CanalAdmits(kind, canal.Width, canal.Depth);
            }

            return IsOpenWater(_network.PositionOf(a), _network.PositionOf(b), kind);
        }

        /// <summary>De vaartijd van één stuk tussen twee havens in seconden, zonder aan de kade te liggen; gooit als het stuk niet bevaarbaar is.</summary>
        public float LegTime(int a, int b, VesselKind kind)
        {
            var distance = math.distance(_network.PositionOf(a), _network.PositionOf(b));
            if (!IsNavigable(a, b, kind))
            {
                throw new ArgumentException($"Tussen haven {a} en {b} kan een {kind} niet varen.");
            }

            return _canals.Values.Any(c => c.Connects(a, b))
                ? WaterTraffic.CanalTime(distance)
                : WaterTraffic.SailTime(kind, distance);
        }

        /// <summary>Controleert of de haltes een geldige vaarlijn vormen; gooit een <see cref="ArgumentException"/> met de reden.</summary>
        public void CheckLine(IReadOnlyList<int> stops, bool isLoop)
        {
            if (stops == null)
            {
                throw new ArgumentNullException(nameof(stops));
            }

            if (stops.Count == 0)
            {
                return;
            }

            var kind = GetPort(stops[0]).Kind;
            for (var i = 0; i < stops.Count; i++)
            {
                if (GetPort(stops[i]).Kind != kind)
                {
                    throw new ArgumentException($"Haven {stops[i]} is een haven voor een andere soort schip dan haven {stops[0]}.", nameof(stops));
                }

                if (i > 0)
                {
                    CheckLeg(stops[i - 1], stops[i], kind);
                }
            }

            if (isLoop && stops.Count > 1)
            {
                CheckLeg(stops[stops.Count - 1], stops[0], kind);
            }
        }

        public int AddLine(string name, IReadOnlyList<int> stops, bool isLoop, int vehicles, float headway)
        {
            CheckLine(stops, isLoop);
            return _network.AddLine(name, stops, isLoop, vehicles, headway);
        }

        /// <summary>De soort schip van een lijn, bepaald door haar havens.</summary>
        public VesselKind KindOf(int lineId) => GetPort(_network.GetLine(lineId).Stops[0]).Kind;

        /// <summary>
        /// De rondrit van een lijn in seconden: alle stukken heen en terug (bij een ronde één keer rond), plus de tijd aan de kade
        /// bij elke haven. <paramref name="tonnes"/> is de lading per haven voor een vrachtlijn (genegeerd bij een veer).
        /// </summary>
        public float RoundTripTime(int lineId, float tonnes = 0f)
        {
            var line = _network.GetLine(lineId);
            var kind = KindOf(lineId);
            var sailing = 0f;
            for (var i = 1; i < line.Stops.Count; i++)
            {
                sailing += LegTime(line.Stops[i - 1], line.Stops[i], kind);
            }

            if (line.IsLoop)
            {
                sailing += LegTime(line.Stops[line.Stops.Count - 1], line.Stops[0], kind);
            }
            else
            {
                sailing *= 2f;
            }

            var calls = line.IsLoop ? line.Stops.Count : 2 * line.Stops.Count - 2;
            var dwell = kind == VesselKind.CargoShip ? WaterTraffic.HandlingTime(tonnes) : WaterTraffic.DwellTime(kind);
            return sailing + calls * dwell;
        }

        /// <summary>Vervoerscapaciteit per dag van een lijn (passagiers of ton, één richting).</summary>
        public float DailyCapacity(int lineId, float tonnes = 0f)
        {
            var line = _network.GetLine(lineId);
            return WaterTraffic.DailyCapacity(KindOf(lineId), line.Vehicles, RoundTripTime(lineId, tonnes));
        }

        public void Restore(IReadOnlyList<Port> ports, IReadOnlyList<Canal> canals, int nextCanal)
        {
            _ports.Clear();
            foreach (var port in ports)
            {
                _ports.Add(port.Stop, port);
            }

            _canals.Clear();
            foreach (var canal in canals)
            {
                _canals.Add(canal.Id, canal);
            }

            _nextCanal = nextCanal;
        }

        private void CheckLeg(int a, int b, VesselKind kind)
        {
            if (!IsNavigable(a, b, kind))
            {
                throw new ArgumentException($"Tussen haven {a} en {b} kan een {kind} niet varen: te ondiep of te smal.");
            }
        }

        private bool IsOpenWater(float2 a, float2 b, VesselKind kind)
        {
            var distance = math.distance(a, b);
            var steps = math.max(1, (int)math.ceil(distance / WaterTraffic.SampleStep));
            for (var i = 0; i <= steps; i++)
            {
                if (!WaterTraffic.IsDepthOk(kind, _water.DepthAt(math.lerp(a, b, i / (float)steps))))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
