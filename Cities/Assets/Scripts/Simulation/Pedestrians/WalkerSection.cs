using System;
using System.Collections.Generic;
using Cities.Core.Persistence;
using Cities.Simulation.Network;

namespace Cities.Simulation.Pedestrians
{
    /// <summary>Sectie <c>WALK</c>: de voetgangers en fietsers onderweg met hun route en voortgang.</summary>
    public sealed class WalkerSection : ISaveSection
    {
        public const string SectionId = "WALK";

        private const int MaxWalkers = 4_000_000;
        private const int MaxRouteNodes = 1_000_000;

        private readonly WalkerSimulation _simulation;

        public WalkerSection(WalkerSimulation simulation)
        {
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_simulation.NextId);
            writer.Write(_simulation.Walkers.Count);
            foreach (var walker in _simulation.Walkers)
            {
                writer.Write(walker.Id);
                writer.Write((byte)walker.Mode);
                writer.Write(walker.Index);
                writer.Write(walker.Offset);
                writer.Write(walker.OnCrossing);
                writer.Write(walker.Nodes.Count);
                foreach (var node in walker.Nodes)
                {
                    writer.Write(node);
                }
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var nextId = reader.ReadInt32();
            var count = reader.ReadInt32();
            if (count < 0 || count > MaxWalkers)
            {
                throw reader.Corrupt($"onmogelijk aantal wandelaars ({count})");
            }

            var walkers = new List<Walker>(count);
            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadInt32();
                var mode = reader.ReadByte();
                var index = reader.ReadInt32();
                var offset = reader.ReadSingle();
                var onCrossing = reader.ReadBool();
                var nodeCount = reader.ReadInt32();
                if (mode > (byte)PathMode.Bike || nodeCount < 2 || nodeCount > MaxRouteNodes)
                {
                    throw reader.Corrupt($"onmogelijke wandelaar {id}");
                }

                var nodes = new int[nodeCount];
                for (var n = 0; n < nodeCount; n++)
                {
                    nodes[n] = reader.ReadInt32();
                }

                var walker = new Walker(id, (PathMode)mode, nodes, index, offset, onCrossing);
                if (!_simulation.IsValid(walker))
                {
                    throw reader.Corrupt($"wandelaar {id} past niet op het padennet");
                }

                walkers.Add(walker);
            }

            reader.ExpectEnd();
            return () => _simulation.Restore(nextId, walkers);
        }
    }
}
