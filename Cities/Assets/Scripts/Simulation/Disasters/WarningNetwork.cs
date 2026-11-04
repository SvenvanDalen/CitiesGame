using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;
using Unity.Mathematics;

namespace Cities.Simulation.Disasters
{
    public enum WarningKind : byte
    {
        /// <summary>Sirene: waarschuwt alleen de mensen binnen haar bereik.</summary>
        Siren,

        /// <summary>Vroegwaarschuwingscentrum: waarschuwt de hele stad en geeft extra voorspellingstijd.</summary>
        EarlyWarningCenter,

        /// <summary>Radar: ziet weerrampen eerder aankomen binnen haar bereik.</summary>
        Radar,
    }

    /// <summary>Eén waarschuwingsvoorziening op de kaart (E15-09).</summary>
    public sealed class WarningStation
    {
        public WarningStation(int id, WarningKind kind, float2 position, float radius)
        {
            if (!Enum.IsDefined(typeof(WarningKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Onbekende waarschuwingsvoorziening.");
            }

            if (kind != WarningKind.EarlyWarningCenter && !(radius > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "Het bereik is groter dan nul.");
            }

            Id = id;
            Kind = kind;
            Position = position;
            Radius = radius;
        }

        public int Id { get; }

        public WarningKind Kind { get; }

        public float2 Position { get; }

        /// <summary>Bereik in meter; een vroegwaarschuwingscentrum bereikt de hele stad en negeert dit.</summary>
        public float Radius { get; }

        public bool Reaches(float2 point) => Kind == WarningKind.EarlyWarningCenter || math.distance(point, Position) <= Radius;
    }

    /// <summary>
    /// De waarschuwingsvoorzieningen van de stad (E15-09): sirenes, een vroegwaarschuwingscentrum en radar geven tijd om te reageren.
    /// Een voorspelbare ramp (waarschuwingsdagen in zijn <see cref="DisasterSpec"/>) wordt door het centrum een dag eerder gemeld, een weerramp
    /// binnen het bereik van een radar nog een dag eerder. Die tijd helpt alleen wie ook gewaarschuwd is: iemand in het bereik van een
    /// sirene, of iedereen zodra er een centrum is. Rampen zonder voorspelling (aardbeving, meteoriet) geven nooit extra tijd.
    /// </summary>
    public sealed class WarningNetwork
    {
        /// <summary>Extra dagen voorspellingstijd van het vroegwaarschuwingscentrum.</summary>
        public const float CenterBonusDays = 1f;

        /// <summary>Extra dagen die een radar een weerramp eerder ziet.</summary>
        public const float RadarBonusDays = 1f;

        private static readonly HashSet<string> WeatherDisasters = new HashSet<string>(StringComparer.Ordinal) { "Storm", "Tornado", "Overstroming" };

        private readonly List<WarningStation> _stations = new List<WarningStation>();

        public int NextId { get; private set; } = 1;

        public IReadOnlyList<WarningStation> Stations => _stations;

        public WarningStation Add(WarningKind kind, float2 position, float radius = 0f)
        {
            var station = new WarningStation(NextId, kind, position, radius);
            NextId++;
            _stations.Add(station);
            return station;
        }

        public bool Remove(int id) => _stations.RemoveAll(s => s.Id == id) > 0;

        /// <summary>Wordt dit punt gewaarschuwd? Door een sirene binnen bereik of door een vroegwaarschuwingscentrum.</summary>
        public bool IsAlerted(float2 point) => _stations.Any(s => s.Kind != WarningKind.Radar && s.Reaches(point));

        /// <summary>Dagen voorspellingstijd die de voorzieningen aan een ramp op <paramref name="disasterPosition"/> geven, los van wie gewaarschuwd wordt.</summary>
        public float LeadDays(DisasterSpec spec, float2 disasterPosition)
        {
            if (spec is null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var days = (float)spec.WarningDays;
            if (spec.WarningDays <= 0)
            {
                return days;
            }

            if (_stations.Any(s => s.Kind == WarningKind.EarlyWarningCenter))
            {
                days += CenterBonusDays;
            }

            if (WeatherDisasters.Contains(spec.Name) && _stations.Any(s => s.Kind == WarningKind.Radar && s.Reaches(disasterPosition)))
            {
                days += RadarBonusDays;
            }

            return days;
        }

        /// <summary>De tijd die iemand op <paramref name="point"/> echt heeft om te reageren: de voorspellingstijd als hij gewaarschuwd wordt, anders nul.</summary>
        public float ReactionDays(DisasterSpec spec, float2 disasterPosition, float2 point) =>
            IsAlerted(point) ? LeadDays(spec, disasterPosition) : 0f;

        /// <summary>Het aandeel (0 tot 1) van de punten dat gewaarschuwd wordt; zonder punten 0.</summary>
        public float Coverage(IReadOnlyCollection<float2> points)
        {
            if (points is null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            return points.Count == 0 ? 0f : points.Count(IsAlerted) / (float)points.Count;
        }

        internal void Restore(int nextId, IEnumerable<WarningStation> stations)
        {
            NextId = nextId;
            _stations.Clear();
            _stations.AddRange(stations);
        }
    }

    /// <summary>Sectie <c>WARN</c>: de waarschuwingsvoorzieningen van de stad (E15-09).</summary>
    public sealed class WarningSection : ISaveSection
    {
        public const string SectionId = "WARN";

        private const int MaxStations = 100_000;

        private readonly WarningNetwork _network;

        public WarningSection(WarningNetwork network)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_network.NextId);
            writer.Write(_network.Stations.Count);
            foreach (var station in _network.Stations)
            {
                writer.Write(station.Id);
                writer.Write((byte)station.Kind);
                writer.Write(station.Position.x);
                writer.Write(station.Position.y);
                writer.Write(station.Radius);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var nextId = reader.ReadInt32();
            var count = reader.ReadInt32();
            if (nextId < 1 || count < 0 || count > MaxStations)
            {
                throw reader.Corrupt($"onmogelijke waarschuwingen: volgnummer {nextId}, aantal {count}");
            }

            var stations = new List<WarningStation>(count);
            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadInt32();
                var kind = (WarningKind)reader.ReadByte();
                var position = new float2(reader.ReadSingle(), reader.ReadSingle());
                var radius = reader.ReadSingle();
                var valid = id >= 1 && id < nextId && Enum.IsDefined(typeof(WarningKind), kind)
                    && (kind == WarningKind.EarlyWarningCenter || radius > 0f);
                if (!valid)
                {
                    throw reader.Corrupt($"onmogelijke waarschuwingsvoorziening {id}");
                }

                stations.Add(new WarningStation(id, kind, position, radius));
            }

            reader.ExpectEnd();
            return () => _network.Restore(nextId, stations);
        }
    }
}
