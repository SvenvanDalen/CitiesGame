using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;

namespace Cities.Simulation.Transit
{
    /// <summary>
    /// Sectie <c>WATR</c>: de havens (soort en kadediepte) en kanalen van het waterverkeer. De haltes en lijnen zelf staan in sectie
    /// <c>TRNS</c> (<see cref="TransitSection"/>); hier staat alleen welke haltes havens zijn en welke kanalen er liggen.
    /// </summary>
    public sealed class WaterSection : ISaveSection
    {
        public const string SectionId = "WATR";

        private const int MaxItems = 1_000_000;

        private readonly WaterNetwork _water;

        public WaterSection(WaterNetwork water)
        {
            _water = water ?? throw new ArgumentNullException(nameof(water));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            var ports = _water.Ports.ToArray();
            writer.Write(ports.Length);
            foreach (var port in ports)
            {
                writer.Write(port.Stop);
                writer.Write((int)port.Kind);
                writer.Write(port.Depth);
            }

            var canals = _water.Canals.ToArray();
            writer.Write(canals.Length);
            foreach (var canal in canals)
            {
                writer.Write(canal.Id);
                writer.Write(canal.From);
                writer.Write(canal.To);
                writer.Write(canal.Width);
                writer.Write(canal.Depth);
            }

            writer.Write(_water.NextCanalId);
        }

        public Action Read(SaveReader reader, int version)
        {
            var ports = ReadPorts(reader);
            var canals = ReadCanals(reader, ports);
            var nextCanal = reader.ReadInt32();
            if (canals.Any(c => c.Id >= nextCanal) || nextCanal < 1)
            {
                throw reader.Corrupt($"onmogelijke volgende kanaal-ID {nextCanal}");
            }

            reader.ExpectEnd();
            return () => _water.Restore(ports, canals, nextCanal);
        }

        private static List<Port> ReadPorts(SaveReader reader)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > MaxItems)
            {
                throw reader.Corrupt($"onmogelijk aantal havens: {count}");
            }

            var ports = new List<Port>(count);
            var seen = new HashSet<int>();
            for (var i = 0; i < count; i++)
            {
                var stop = reader.ReadInt32();
                var kind = reader.ReadInt32();
                var depth = reader.ReadSingle();
                if (!Enum.IsDefined(typeof(VesselKind), kind))
                {
                    throw reader.Corrupt($"onbekend soort haven {kind} bij halte {stop}");
                }

                if (!WaterTraffic.IsDepthOk((VesselKind)kind, depth))
                {
                    throw reader.Corrupt($"onmogelijke kadediepte {depth} bij halte {stop}");
                }

                if (!seen.Add(stop))
                {
                    throw reader.Corrupt($"haven {stop} komt dubbel voor");
                }

                ports.Add(new Port(stop, (VesselKind)kind, depth));
            }

            return ports;
        }

        private static List<Canal> ReadCanals(SaveReader reader, List<Port> ports)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > MaxItems)
            {
                throw reader.Corrupt($"onmogelijk aantal kanalen: {count}");
            }

            var stops = new HashSet<int>(ports.Select(p => p.Stop));
            var canals = new List<Canal>(count);
            var ids = new HashSet<int>();
            for (var i = 0; i < count; i++)
            {
                var canal = new Canal(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadSingle(), reader.ReadSingle());
                if (!stops.Contains(canal.From) || !stops.Contains(canal.To) || canal.From == canal.To)
                {
                    throw reader.Corrupt($"kanaal {canal.Id} verbindt geen twee havens");
                }

                if (!WaterTraffic.IsCanalWidthOk(canal.Width) || !WaterTraffic.IsCanalDepthOk(canal.Depth))
                {
                    throw reader.Corrupt($"onmogelijke afmetingen van kanaal {canal.Id}");
                }

                if (!ids.Add(canal.Id))
                {
                    throw reader.Corrupt($"kanaal {canal.Id} komt dubbel voor");
                }

                canals.Add(canal);
            }

            return canals;
        }
    }
}
