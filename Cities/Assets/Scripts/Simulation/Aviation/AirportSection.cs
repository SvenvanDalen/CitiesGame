using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;
using Unity.Mathematics;

namespace Cities.Simulation.Aviation
{
    /// <summary>Sectie <c>AIRP</c>: de luchthavens met hun baan, terminal, gates, vraag en tellers.</summary>
    public sealed class AirportSection : ISaveSection
    {
        public const string SectionId = "AIRP";

        private readonly AirportNetwork _network;

        public AirportSection(AirportNetwork network)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_network.NextId);
            var airports = _network.Airports.ToArray();
            writer.Write(airports.Length);
            foreach (var airport in airports)
            {
                var stats = _network.StatsOf(airport.Id);
                writer.Write(airport.Id);
                writer.Write(airport.Name);
                writer.Write(airport.Position.x);
                writer.Write(airport.Position.y);
                writer.Write(airport.Heading);
                writer.Write(airport.RunwayLength);
                writer.Write(airport.TerminalCapacity);
                writer.Write(stats.Demand);
                writer.Write(stats.Passengers);
                writer.Write(stats.Movements);
                writer.Write(airport.Gates.Count);
                foreach (var gate in airport.Gates)
                {
                    writer.Write((byte)gate);
                }
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var nextId = reader.ReadInt32();
            var count = reader.ReadInt32();
            if (count < 0 || count > AirportNetwork.MaxAirports)
            {
                throw reader.Corrupt($"onmogelijk aantal luchthavens: {count}");
            }

            var entries = new List<KeyValuePair<Airport, AirportStats>>(count);
            var airports = new List<Airport>(count);
            for (var i = 0; i < count; i++)
            {
                var entry = ReadAirport(reader);
                if (airports.Any(a => a.Id == entry.Key.Id))
                {
                    throw reader.Corrupt($"luchthaven {entry.Key.Id} komt dubbel voor");
                }

                if (entry.Key.Id >= nextId)
                {
                    throw reader.Corrupt($"luchthaven {entry.Key.Id} past niet bij de volgende ID {nextId}");
                }

                airports.Add(entry.Key);
                entries.Add(entry);
            }

            reader.ExpectEnd();
            try
            {
                AirportNetwork.CheckSpacing(airports);
            }
            catch (ArgumentException e)
            {
                throw reader.Corrupt(e.Message);
            }

            return () => _network.Restore(entries, nextId);
        }

        private static KeyValuePair<Airport, AirportStats> ReadAirport(SaveReader reader)
        {
            var id = reader.ReadInt32();
            var name = reader.ReadString();
            var position = new float2(reader.ReadSingle(), reader.ReadSingle());
            var heading = reader.ReadSingle();
            var runway = reader.ReadSingle();
            var terminal = reader.ReadSingle();
            var demand = reader.ReadSingle();
            var passengers = reader.ReadDouble();
            var movements = reader.ReadDouble();
            var gateCount = reader.ReadInt32();
            if (gateCount < 1 || gateCount > AviationRules.MaxGates)
            {
                throw reader.Corrupt($"onmogelijk aantal gates: {gateCount}");
            }

            var gates = new AircraftClass[gateCount];
            for (var g = 0; g < gateCount; g++)
            {
                var raw = reader.ReadByte();
                if (!Enum.IsDefined(typeof(AircraftClass), (int)raw))
                {
                    throw reader.Corrupt($"onbekende vliegtuigsoort {raw}");
                }

                gates[g] = (AircraftClass)raw;
            }

            if (!(demand >= 0f) || float.IsInfinity(demand) || !(passengers >= 0.0) || !(movements >= 0.0))
            {
                throw reader.Corrupt($"onmogelijke vraag of tellers bij luchthaven {id}");
            }

            try
            {
                return new KeyValuePair<Airport, AirportStats>(
                    new Airport(id, name, position, heading, runway, terminal, gates),
                    new AirportStats(demand, passengers, movements));
            }
            catch (ArgumentException e)
            {
                throw reader.Corrupt(e.Message);
            }
        }
    }
}
