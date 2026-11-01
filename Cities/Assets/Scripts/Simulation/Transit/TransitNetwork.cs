using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

namespace Cities.Simulation.Transit
{
    /// <summary>
    /// De haltes en lijnen van het openbaar vervoer (E09-01), een laag bovenop het wegennet. Haltes zijn punten; een lijn verbindt ze in
    /// volgorde. Een halte die een lijn gebruikt kan niet weg. shortcut: de afstand tussen haltes is hemelsbreed en er is alleen een bus, tot
    /// de lijnen over het wegennet lopen (E08-pathfinding) en de andere vervoersmiddelen (E09-05 en verder) komen.
    /// </summary>
    public sealed class TransitNetwork
    {
        private readonly SortedDictionary<int, float2> _stops = new SortedDictionary<int, float2>();
        private readonly SortedDictionary<int, TransitLine> _lines = new SortedDictionary<int, TransitLine>();
        private int _nextStop = 1;
        private int _nextLine = 1;

        public IEnumerable<KeyValuePair<int, float2>> Stops => _stops;

        public IEnumerable<TransitLine> Lines => _lines.Values;

        public int NextStopId => _nextStop;

        public int NextLineId => _nextLine;

        public int AddStop(float2 position)
        {
            var id = _nextStop++;
            _stops.Add(id, position);
            return id;
        }

        public bool HasStop(int stop) => _stops.ContainsKey(stop);

        public float2 PositionOf(int stop) =>
            _stops.TryGetValue(stop, out var position) ? position : throw new ArgumentException($"Onbekende halte {stop}.", nameof(stop));

        /// <summary>Verwijdert een halte; geeft false als hij niet bestaat of nog door een lijn wordt gebruikt.</summary>
        public bool TryRemoveStop(int stop) => !_lines.Values.Any(l => l.Stops.Contains(stop)) && _stops.Remove(stop);

        public TransitLine GetLine(int id) =>
            _lines.TryGetValue(id, out var line) ? line : throw new ArgumentException($"Onbekende lijn {id}.", nameof(id));

        public int AddLine(string name, IReadOnlyList<int> stops, bool isLoop, int vehicles, float headway)
        {
            var line = new TransitLine(_nextLine, name, stops, isLoop, vehicles, headway);
            CheckStops(line);
            _lines.Add(line.Id, line);
            return _nextLine++;
        }

        /// <summary>Vervangt de naam, haltes, ronde, voertuigen en frequentie van een lijn; bij een fout blijft de lijn zoals hij was.</summary>
        public void Update(int id, string name, IReadOnlyList<int> stops, bool isLoop, int vehicles, float headway)
        {
            GetLine(id);
            var line = new TransitLine(id, name, stops, isLoop, vehicles, headway);
            CheckStops(line);
            _lines[id] = line;
        }

        public bool RemoveLine(int id) => _lines.Remove(id);

        /// <summary>Lengte van één rit over de lijn in meter: van halte naar halte, bij een ronde ook terug naar het begin.</summary>
        public float Length(int id)
        {
            var line = GetLine(id);
            var length = 0f;
            for (var i = 1; i < line.Stops.Count; i++)
            {
                length += math.distance(_stops[line.Stops[i - 1]], _stops[line.Stops[i]]);
            }

            return line.IsLoop ? length + math.distance(_stops[line.Stops[line.Stops.Count - 1]], _stops[line.Stops[0]]) : length;
        }

        /// <summary>
        /// De tijd tussen twee voertuigen die de lijn bij <paramref name="speed"/> (m/s) echt haalt: de gewenste tijd, of langer als de
        /// voertuigen een rondrit niet vaker kunnen rijden.
        /// </summary>
        public float EffectiveHeadway(int id, float speed)
        {
            if (!(speed > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "De snelheid moet groter dan nul zijn.");
            }

            var line = GetLine(id);
            var cycle = Length(id) * (line.IsLoop ? 1f : 2f) / speed;
            return math.max(line.Headway, cycle / line.Vehicles);
        }

        /// <summary>Controleert een volledige toestand zonder iets te veranderen; gooit een <see cref="ArgumentException"/> bij een fout.</summary>
        public static void Check(IReadOnlyCollection<int> stopIds, IReadOnlyCollection<TransitLine> lines)
        {
            var known = new HashSet<int>(stopIds);
            if (known.Count != stopIds.Count)
            {
                throw new ArgumentException("Een halte komt dubbel voor.");
            }

            if (lines.Select(l => l.Id).Distinct().Count() != lines.Count)
            {
                throw new ArgumentException("Een lijn komt dubbel voor.");
            }

            if (lines.Any(l => l.Stops.Any(s => !known.Contains(s))))
            {
                throw new ArgumentException("Een lijn gebruikt een onbekende halte.");
            }
        }

        public void Restore(IReadOnlyList<KeyValuePair<int, float2>> stops, IReadOnlyList<TransitLine> lines, int nextStop, int nextLine)
        {
            _stops.Clear();
            _lines.Clear();
            foreach (var stop in stops)
            {
                _stops.Add(stop.Key, stop.Value);
            }

            foreach (var line in lines)
            {
                _lines.Add(line.Id, line);
            }

            _nextStop = nextStop;
            _nextLine = nextLine;
        }

        private void CheckStops(TransitLine line)
        {
            var missing = line.Stops.Where(s => !_stops.ContainsKey(s)).Select(s => (int?)s).FirstOrDefault();
            if (missing.HasValue)
            {
                throw new ArgumentException($"Onbekende halte {missing.Value} in de lijn.", nameof(line));
            }
        }
    }
}
