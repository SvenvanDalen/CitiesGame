using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;

namespace Cities.Simulation.Transit
{
    /// <summary>
    /// Sectie <c>METR</c>: de perrondiepte van elk metrostation. De haltes en lijnen zelf staan in sectie <c>TRNS</c>
    /// (<see cref="TransitSection"/>); hier staat alleen welke haltes een station zijn en hoe diep.
    /// </summary>
    public sealed class MetroSection : ISaveSection
    {
        public const string SectionId = "METR";

        private const int MaxStations = 1_000_000;

        private readonly MetroNetwork _metro;

        public MetroSection(MetroNetwork metro)
        {
            _metro = metro ?? throw new ArgumentNullException(nameof(metro));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            var stations = _metro.Stations.ToArray();
            writer.Write(stations.Length);
            foreach (var station in stations)
            {
                writer.Write(station.Key);
                writer.Write(station.Value);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > MaxStations)
            {
                throw reader.Corrupt($"onmogelijk aantal stations: {count}");
            }

            var stations = new List<KeyValuePair<int, float>>(count);
            var seen = new HashSet<int>();
            for (var i = 0; i < count; i++)
            {
                var stop = reader.ReadInt32();
                var depth = reader.ReadSingle();
                if (!Metro.IsDepthOk(depth))
                {
                    throw reader.Corrupt($"onmogelijke perrondiepte {depth} bij halte {stop}");
                }

                if (!seen.Add(stop))
                {
                    throw reader.Corrupt($"station {stop} komt dubbel voor");
                }

                stations.Add(new KeyValuePair<int, float>(stop, depth));
            }

            reader.ExpectEnd();
            return () => _metro.Restore(stations);
        }
    }
}
