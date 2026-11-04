using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Cities.Simulation.Disasters
{
    /// <summary>Een gebouw zoals het zinkgat het ziet: wie, waar, hoe groot de voetafdruk is en wat het waard is.</summary>
    public readonly struct SinkholeBuilding
    {
        public SinkholeBuilding(int id, float2 position, float footprintRadius, long value)
        {
            Id = id;
            Position = position;
            FootprintRadius = footprintRadius;
            Value = value;
        }

        public int Id { get; }

        public float2 Position { get; }

        /// <summary>Straal in meter van de cirkel die de voetafdruk omvat.</summary>
        public float FootprintRadius { get; }

        public long Value { get; }
    }

    /// <summary>Een wegsegment als rechte lijn tussen twee punten (genoeg om te zien of de grond eronder wegzakt).</summary>
    public readonly struct SinkholeSegment
    {
        public SinkholeSegment(int id, float2 from, float2 to)
        {
            Id = id;
            From = from;
            To = to;
        }

        public int Id { get; }

        public float2 From { get; }

        public float2 To { get; }

        public float Length => math.distance(From, To);
    }

    /// <summary>Wat een dag zinkgat extra kostte: de nieuw verdwenen gebouwen en wegsegmenten met hun waarde en lengte.</summary>
    public sealed class SinkholeOutcome
    {
        internal SinkholeOutcome(float radius, IReadOnlyList<int> buildings, IReadOnlyList<int> segments, long valueLost, float roadLengthLost)
        {
            Radius = radius;
            Buildings = buildings;
            Segments = segments;
            ValueLost = valueLost;
            RoadLengthLost = roadLengthLost;
        }

        /// <summary>Straal van het gat na deze dag.</summary>
        public float Radius { get; }

        /// <summary>ID's van gebouwen die vandaag zijn ingestort of verdwenen.</summary>
        public IReadOnlyList<int> Buildings { get; }

        /// <summary>ID's van wegsegmenten die vandaag zijn weggezakt.</summary>
        public IReadOnlyList<int> Segments { get; }

        /// <summary>Totale waarde van de vandaag verloren gebouwen.</summary>
        public long ValueLost { get; }

        /// <summary>Totale lengte in meter van de vandaag verloren wegen.</summary>
        public float RoadLengthLost { get; }
    }

    /// <summary>
    /// Zinkgat (E15-07). De grond stort in een cirkel rond het middelpunt in; de straal groeit elke dag met <see cref="GrowthPerDay"/>
    /// meter tot <see cref="MaxRadius"/> (daarna is het gat stabiel). Een gebouw verdwijnt zodra zijn middelpunt binnen de straal plus
    /// <see cref="FootprintShare"/> van zijn eigen straal valt, een wegsegment zodra de rijlijn het gat raakt. Wat weg is blijft weg: het
    /// gat wordt alleen groter. De uitkomst per dag is data voor de rest van de sim (verwijderen van gebouwen en wegen, kosten).
    /// </summary>
    public sealed class Sinkhole
    {
        /// <summary>Aandeel van de eigen straal dat een gebouw buiten de rand mag liggen en toch wegzakt.</summary>
        public const float FootprintShare = 0.5f;

        /// <summary>Kosten per meter weg om het gat te vullen en te herstellen.</summary>
        public const long RepairPerMeter = 500;

        private readonly List<int> _lostBuildings = new List<int>();
        private readonly List<int> _lostSegments = new List<int>();

        public Sinkhole(int id, float2 center, float maxRadius, float growthPerDay)
        {
            if (!(maxRadius > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(maxRadius), maxRadius, "Het zinkgat heeft een straal groter dan nul nodig.");
            }

            if (!(growthPerDay > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(growthPerDay), growthPerDay, "Het zinkgat groeit meer dan nul meter per dag.");
            }

            Id = id;
            Center = center;
            MaxRadius = maxRadius;
            GrowthPerDay = growthPerDay;
        }

        // shortcut: vaste kosten per dag; de echte schade volgt uit SinkholeOutcome, koppelen aan het Ledger zodra gebouwen in de wereld staan.
        /// <summary>Scheuren en verzakkingen kondigen het een dag eerder aan; het gat groeit drie dagen.</summary>
        public static DisasterSpec Spec { get; } = new DisasterSpec("Zinkgat", 0.0003f, 1, 3, 30, 40_000, 15_000);

        public int Id { get; }

        public float2 Center { get; }

        public float MaxRadius { get; }

        public float GrowthPerDay { get; }

        /// <summary>Dagen sinds het gat opende.</summary>
        public int AgeDays { get; private set; }

        public float Radius => math.min(MaxRadius, GrowthPerDay * AgeDays);

        public bool IsStable => Radius >= MaxRadius;

        public IReadOnlyList<int> LostBuildings => _lostBuildings;

        public IReadOnlyList<int> LostSegments => _lostSegments;

        /// <summary>Of een punt in het gat ligt.</summary>
        public bool Contains(float2 point) => math.distance(point, Center) <= Radius;

        public bool Swallows(SinkholeBuilding building) =>
            math.distance(building.Position, Center) <= Radius + (FootprintShare * building.FootprintRadius);

        public bool Swallows(SinkholeSegment segment) => DistanceToSegment(Center, segment.From, segment.To) <= Radius;

        /// <summary>Kosten van de weggezakte weg (vullen en herstellen) voor een uitkomst.</summary>
        public static long RepairCost(SinkholeOutcome outcome)
        {
            if (outcome is null)
            {
                throw new ArgumentNullException(nameof(outcome));
            }

            return (long)math.round(outcome.RoadLengthLost * RepairPerMeter);
        }

        /// <summary>
        /// Eén dag verder: het gat groeit en wat nu in het gat valt (en nog niet verloren was) komt in de uitkomst. De volgorde van
        /// <paramref name="buildings"/> en <paramref name="segments"/> bepaalt de volgorde van de uitkomst.
        /// </summary>
        public SinkholeOutcome Advance(IReadOnlyList<SinkholeBuilding> buildings, IReadOnlyList<SinkholeSegment> segments)
        {
            if (buildings is null)
            {
                throw new ArgumentNullException(nameof(buildings));
            }

            if (segments is null)
            {
                throw new ArgumentNullException(nameof(segments));
            }

            AgeDays++;
            var newBuildings = new List<int>();
            var value = 0L;
            foreach (var building in buildings)
            {
                if (!_lostBuildings.Contains(building.Id) && Swallows(building))
                {
                    newBuildings.Add(building.Id);
                    value += building.Value;
                }
            }

            var newSegments = new List<int>();
            var length = 0f;
            foreach (var segment in segments)
            {
                if (!_lostSegments.Contains(segment.Id) && Swallows(segment))
                {
                    newSegments.Add(segment.Id);
                    length += segment.Length;
                }
            }

            _lostBuildings.AddRange(newBuildings);
            _lostSegments.AddRange(newSegments);
            return new SinkholeOutcome(Radius, newBuildings, newSegments, value, length);
        }

        /// <summary>Zet de status terug uit een save.</summary>
        internal void Restore(int ageDays, IEnumerable<int> lostBuildings, IEnumerable<int> lostSegments)
        {
            AgeDays = ageDays;
            _lostBuildings.Clear();
            _lostBuildings.AddRange(lostBuildings);
            _lostSegments.Clear();
            _lostSegments.AddRange(lostSegments);
        }

        private static float DistanceToSegment(float2 point, float2 a, float2 b)
        {
            var ab = b - a;
            var lengthSquared = math.lengthsq(ab);
            if (lengthSquared <= 0f)
            {
                return math.distance(point, a);
            }

            var t = math.saturate(math.dot(point - a, ab) / lengthSquared);
            return math.distance(point, a + (ab * t));
        }
    }
}
