using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;
using Unity.Mathematics;

namespace Cities.Simulation.Transit
{
    /// <summary>Sectie <c>TRNS</c>: de haltes en lijnen van het openbaar vervoer.</summary>
    public sealed class TransitSection : ISaveSection
    {
        public const string SectionId = "TRNS";

        private const int MaxStops = 1_000_000;
        private const int MaxLines = 100_000;

        private readonly TransitNetwork _network;

        public TransitSection(TransitNetwork network)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_network.NextStopId);
            writer.Write(_network.NextLineId);
            var stops = _network.Stops.ToArray();
            writer.Write(stops.Length);
            foreach (var stop in stops)
            {
                writer.Write(stop.Key);
                writer.Write(stop.Value.x);
                writer.Write(stop.Value.y);
            }

            var lines = _network.Lines.ToArray();
            writer.Write(lines.Length);
            foreach (var line in lines)
            {
                writer.Write(line.Id);
                writer.Write(line.Name);
                writer.Write(line.IsLoop);
                writer.Write(line.Vehicles);
                writer.Write(line.Headway);
                writer.Write(line.Stops.Count);
                foreach (var stop in line.Stops)
                {
                    writer.Write(stop);
                }
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var nextStop = reader.ReadInt32();
            var nextLine = reader.ReadInt32();
            var stops = new List<KeyValuePair<int, float2>>();
            var stopCount = ReadCount(reader, MaxStops, "haltes");
            for (var i = 0; i < stopCount; i++)
            {
                stops.Add(new KeyValuePair<int, float2>(reader.ReadInt32(), new float2(reader.ReadSingle(), reader.ReadSingle())));
            }

            var lines = new List<TransitLine>();
            var lineCount = ReadCount(reader, MaxLines, "lijnen");
            for (var i = 0; i < lineCount; i++)
            {
                lines.Add(ReadLine(reader));
            }

            reader.ExpectEnd();
            if (stops.Any(s => s.Key >= nextStop) || lines.Any(l => l.Id >= nextLine))
            {
                throw reader.Corrupt("volgnummer is te laag");
            }

            try
            {
                TransitNetwork.Check(stops.Select(s => s.Key).ToArray(), lines);
            }
            catch (ArgumentException e)
            {
                throw reader.Corrupt(e.Message);
            }

            return () => _network.Restore(stops, lines, nextStop, nextLine);
        }

        private static TransitLine ReadLine(SaveReader reader)
        {
            var id = reader.ReadInt32();
            var name = reader.ReadString();
            var isLoop = reader.ReadBool();
            var vehicles = reader.ReadInt32();
            var headway = reader.ReadSingle();
            var stops = new int[ReadCount(reader, TransitLine.MaxStops, "haltes in een lijn")];
            for (var i = 0; i < stops.Length; i++)
            {
                stops[i] = reader.ReadInt32();
            }

            try
            {
                return new TransitLine(id, name, stops, isLoop, vehicles, headway);
            }
            catch (ArgumentException e)
            {
                throw reader.Corrupt(e.Message);
            }
        }

        private static int ReadCount(SaveReader reader, int max, string what)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > max)
            {
                throw reader.Corrupt($"onmogelijk aantal {what}: {count}");
            }

            return count;
        }
    }
}
