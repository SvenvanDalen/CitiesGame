using System;
using System.Collections.Generic;
using Cities.Core.Persistence;
using Unity.Mathematics;

namespace Cities.Simulation.Freight
{
    /// <summary>Slaat knooppunten, vrachtwagens en tellers van het goederenverkeer op (E08-11).</summary>
    public sealed class FreightSection : ISaveSection
    {
        public const string SectionId = "FRGT";

        private const int MaxItems = FreightNetwork.MaxNodes;

        private readonly FreightDispatcher _dispatcher;

        public FreightSection(FreightDispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_dispatcher.DeliveredTonnes);
            writer.Write(_dispatcher.ExportedTonnes);
            writer.Write(_dispatcher.ImportedTonnes);
            writer.Write(_dispatcher.Trips);
            writer.Write(_dispatcher.Unreachable);
            writer.Write(_dispatcher.Network.Nodes.Count);
            foreach (var node in _dispatcher.Network.Nodes)
            {
                WriteNode(writer, node);
            }

            writer.Write(_dispatcher.Trucks.Count);
            foreach (var truck in _dispatcher.Trucks)
            {
                WriteTruck(writer, truck);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var delivered = reader.ReadSingle();
            var exported = reader.ReadSingle();
            var imported = reader.ReadSingle();
            var trips = reader.ReadInt32();
            var unreachable = reader.ReadInt32();
            var nodes = ReadNodes(reader);
            var trucks = ReadTrucks(reader, nodes.Count);
            reader.ExpectEnd();
            return () =>
            {
                _dispatcher.Network.Restore(nodes);
                _dispatcher.Restore(trucks);
                _dispatcher.DeliveredTonnes = delivered;
                _dispatcher.ExportedTonnes = exported;
                _dispatcher.ImportedTonnes = imported;
                _dispatcher.Trips = trips;
                _dispatcher.Unreachable = unreachable;
            };
        }

        private static void WriteNode(SaveWriter writer, FreightNode node)
        {
            writer.Write((byte)node.Kind);
            writer.Write((long)node.Owner);
            writer.Write(node.Position.x);
            writer.Write(node.Position.y);
            writer.Write(node.Position.z);
            writer.Write(node.Rate);
            writer.Write(node.Capacity);
            writer.Write(node.Level);
            writer.Write(node.Reserved);
            writer.Write(node.Lost);
            writer.Write(node.Unmet);
            writer.Write(node.EmptyDays);
        }

        private static void WriteTruck(SaveWriter writer, FreightTruck truck)
        {
            writer.Write(truck.At);
            writer.Write((byte)truck.State);
            writer.Write(truck.Source);
            writer.Write(truck.Destination);
            writer.Write(truck.Planned);
            writer.Write(truck.Load);
            writer.Write(truck.Remaining);
            writer.Write(truck.DeliverySeconds);
        }

        private static List<FreightNode> ReadNodes(SaveReader reader)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > MaxItems)
            {
                throw reader.Corrupt($"onmogelijk aantal knooppunten ({count})");
            }

            var nodes = new List<FreightNode>(count);
            for (var i = 0; i < count; i++)
            {
                var kind = reader.ReadByte();
                if (!Enum.IsDefined(typeof(FreightNodeKind), kind))
                {
                    throw reader.Corrupt($"onbekend soort knooppunt {kind}");
                }

                var owner = (ulong)reader.ReadInt64();
                var position = new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                var node = new FreightNode(i, (FreightNodeKind)kind, owner, position)
                {
                    Rate = reader.ReadSingle(),
                    Capacity = reader.ReadSingle(),
                    Level = reader.ReadSingle(),
                    Reserved = reader.ReadSingle(),
                    Lost = reader.ReadSingle(),
                    Unmet = reader.ReadSingle(),
                    EmptyDays = reader.ReadSingle(),
                };
                if (!(node.Rate >= 0f && node.Capacity >= 0f && node.Level >= 0f && node.Reserved >= 0f))
                {
                    throw reader.Corrupt("negatieve hoeveelheid in een knooppunt");
                }

                nodes.Add(node);
            }

            return nodes;
        }

        private static List<FreightTruck> ReadTrucks(SaveReader reader, int nodeCount)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > MaxItems)
            {
                throw reader.Corrupt($"onmogelijk aantal vrachtwagens ({count})");
            }

            var trucks = new List<FreightTruck>(count);
            for (var i = 0; i < count; i++)
            {
                var at = reader.ReadInt32();
                var state = reader.ReadByte();
                var source = reader.ReadInt32();
                var destination = reader.ReadInt32();
                if (!Enum.IsDefined(typeof(FreightTruckState), state) || !InRange(at, nodeCount) || !InRange(source, nodeCount) || !InRange(destination, nodeCount))
                {
                    throw reader.Corrupt($"vrachtwagen {i} verwijst naar iets onbekends");
                }

                trucks.Add(new FreightTruck(i, at)
                {
                    State = (FreightTruckState)state,
                    Source = source,
                    Destination = destination,
                    Planned = reader.ReadSingle(),
                    Load = reader.ReadSingle(),
                    Remaining = reader.ReadSingle(),
                    DeliverySeconds = reader.ReadSingle(),
                });
            }

            return trucks;
        }

        private static bool InRange(int index, int count) => index >= 0 && index < count;
    }
}
