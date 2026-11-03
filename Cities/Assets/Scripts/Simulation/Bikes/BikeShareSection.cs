using System;
using System.Collections.Generic;
using Cities.Core.Persistence;
using Unity.Mathematics;

namespace Cities.Simulation.Bikes
{
    /// <summary>Sectie <c>BIKE</c>: de verhuurpunten met hun fietsen, de verhuurde fietsen en de nog niet afgerekende bedragen.</summary>
    public sealed class BikeShareSection : ISaveSection
    {
        public const string SectionId = "BIKE";

        private readonly BikeShare _share;

        public BikeShareSection(BikeShare share)
        {
            _share = share ?? throw new ArgumentNullException(nameof(share));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_share.NextId);
            writer.Write(_share.InUse);
            writer.Write(_share.PendingRevenue);
            writer.Write(_share.PendingRebalanceCost);
            writer.Write(_share.TotalRentals);
            writer.Write(_share.Stations.Count);
            foreach (var station in _share.Stations)
            {
                writer.Write(station.Id);
                writer.Write(station.Position.x);
                writer.Write(station.Position.y);
                writer.Write(station.Position.z);
                writer.Write(station.Capacity);
                writer.Write(station.Bikes);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var nextId = reader.ReadInt32();
            var inUse = reader.ReadInt32();
            var revenue = reader.ReadInt64();
            var cost = reader.ReadInt64();
            var rentals = reader.ReadInt64();
            var count = reader.ReadInt32();
            if (count < 0 || count > BikeShare.MaxStations || inUse < 0 || revenue < 0 || cost < 0 || rentals < 0)
            {
                throw reader.Corrupt($"onmogelijke deelfietsstatus ({count} punten)");
            }

            var stations = new List<BikeStation>(count);
            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadInt32();
                var position = new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                var capacity = reader.ReadInt32();
                var bikes = reader.ReadInt32();
                if (id < 1 || id >= nextId || capacity < 1 || capacity > BikeShare.MaxCapacity || bikes < 0 || bikes > capacity)
                {
                    throw reader.Corrupt($"onmogelijk verhuurpunt {id}");
                }

                stations.Add(BikeShare.CreateStation(id, position, capacity, bikes));
            }

            reader.ExpectEnd();
            return () => _share.Restore(nextId, inUse, revenue, cost, rentals, stations);
        }
    }
}
