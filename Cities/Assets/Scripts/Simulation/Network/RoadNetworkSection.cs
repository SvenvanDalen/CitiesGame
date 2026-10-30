using System;
using Cities.Core.Ids;
using Cities.Core.Persistence;

namespace Cities.Simulation.Network
{
    /// <summary>Een gelezen en gecontroleerde staat van het wegennet, klaar om toe te passen.</summary>
    internal sealed class RoadNetworkState
    {
        public IdAllocatorState Nodes { get; set; }

        public IdAllocatorState Segments { get; set; }

        public IdAllocatorState Lanes { get; set; }

        public RoadNode[] NodeSlots { get; set; }

        public RoadSegment[] SegmentSlots { get; set; }

        public Lane[] LaneSlots { get; set; }
    }

    /// <summary>
    /// Sectie <c>NETW</c>: de ID-allocators en alle slots van knooppunten, segmenten en lanes. De verbindingen tussen
    /// lanes worden na het laden opnieuw afgeleid. Een save met verwijzingen naar ontbrekende knooppunten, segmenten of
    /// lanes wordt geweigerd.
    /// </summary>
    public sealed class RoadNetworkSection : ISaveSection
    {
        public const string SectionId = "NETW";

        /// <summary>Ruime grens tegen kapotte bestanden; een groot wegennet heeft zo'n honderdduizend segmenten.</summary>
        private const int MaxSlots = 4_000_000;

        private readonly RoadNetwork _network;

        public RoadNetworkSection(RoadNetwork network) => _network = network ?? throw new ArgumentNullException(nameof(network));

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer) => _network.WriteTo(writer);

        public Action Read(SaveReader reader, int version)
        {
            var state = new RoadNetworkState
            {
                Nodes = IdAllocatorState.Read(reader, MaxSlots),
                Segments = IdAllocatorState.Read(reader, MaxSlots),
                Lanes = IdAllocatorState.Read(reader, MaxSlots),
            };
            state.NodeSlots = Slots<RoadNode>(reader, state.Nodes);
            state.SegmentSlots = Slots<RoadSegment>(reader, state.Segments);
            state.LaneSlots = Slots<Lane>(reader, state.Lanes);
            reader.ExpectEnd();
            Validate(reader, state);
            return () => _network.Restore(state);
        }

        private static T[] Slots<T>(SaveReader reader, IdAllocatorState allocator) where T : unmanaged
        {
            var slots = new T[allocator.Generations.Length];
            reader.ReadShuffled<T>(slots);
            return slots;
        }

        private static void Validate(SaveReader reader, RoadNetworkState state)
        {
            for (var i = 0; i < state.SegmentSlots.Length; i++)
            {
                if (!Alive(state.Segments, i))
                {
                    continue;
                }

                var segment = state.SegmentSlots[i];
                if (!Alive(state.Nodes, segment.Start) || !Alive(state.Nodes, segment.End))
                {
                    throw reader.Corrupt($"segment {i} verwijst naar een ontbrekend knooppunt");
                }

                foreach (var lane in segment.Lanes)
                {
                    if (!Alive(state.Lanes, lane) || state.LaneSlots[lane.Index].Segment.Index != i)
                    {
                        throw reader.Corrupt($"segment {i} verwijst naar een ontbrekende lane");
                    }
                }
            }

            for (var i = 0; i < state.NodeSlots.Length; i++)
            {
                if (!Alive(state.Nodes, i))
                {
                    continue;
                }

                foreach (var segment in state.NodeSlots[i].Segments)
                {
                    if (!Alive(state.Segments, segment))
                    {
                        throw reader.Corrupt($"knooppunt {i} verwijst naar een ontbrekend segment");
                    }
                }
            }
        }

        private static bool Alive(IdAllocatorState allocator, int index) =>
            index >= 0 && index < allocator.Generations.Length && (allocator.Generations[index] & 1u) == 1u;

        private static bool Alive<TKind>(IdAllocatorState allocator, EntityId<TKind> id) where TKind : struct, IEntityKind =>
            Alive(allocator, id.Index) && allocator.Generations[id.Index] == id.Generation;
    }
}
