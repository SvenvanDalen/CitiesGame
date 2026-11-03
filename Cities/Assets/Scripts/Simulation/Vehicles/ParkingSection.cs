using System;
using System.Collections.Generic;
using Cities.Core.Persistence;
using Unity.Mathematics;

namespace Cities.Simulation.Vehicles
{
    /// <summary>Sectie <c>PARK</c>: alle parkeerlots met capaciteit en bezetting (E08-10).</summary>
    public sealed class ParkingSection : ISaveSection
    {
        public const string SectionId = "PARK";

        private const int MaxLots = 4_000_000;

        private readonly ParkingRegistry _registry;

        public ParkingSection(ParkingRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_registry.Lots.Count);
            foreach (var lot in _registry.Lots)
            {
                writer.Write((byte)lot.Kind);
                writer.Write((long)lot.Owner);
                writer.Write(lot.Position.x);
                writer.Write(lot.Position.y);
                writer.Write(lot.Position.z);
                writer.Write(lot.Capacity);
                writer.Write(lot.Occupied);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > MaxLots)
            {
                throw reader.Corrupt($"onmogelijk aantal parkeerlots ({count})");
            }

            var lots = new List<(ParkingKind Kind, ulong Owner, float3 Position, int Capacity, int Occupied)>(count);
            for (var i = 0; i < count; i++)
            {
                var kind = reader.ReadByte();
                if (!Enum.IsDefined(typeof(ParkingKind), kind))
                {
                    throw reader.Corrupt($"onbekend soort parkeerplek {kind}");
                }

                var owner = (ulong)reader.ReadInt64();
                var position = new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                var capacity = reader.ReadInt32();
                var occupied = reader.ReadInt32();
                if (capacity < 0 || occupied < 0)
                {
                    throw reader.Corrupt("negatieve capaciteit of bezetting");
                }

                lots.Add(((ParkingKind)kind, owner, position, capacity, occupied));
            }

            reader.ExpectEnd();
            return () => _registry.Restore(lots);
        }
    }
}
