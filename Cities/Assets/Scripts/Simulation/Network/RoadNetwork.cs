using System;
using System.Collections.Generic;
using Cities.Core.Ids;
using Cities.Core.Persistence;
using Unity.Collections;
using Unity.Mathematics;

namespace Cities.Simulation.Network
{
    /// <summary>
    /// Het wegennet: knooppunten, segmenten met een Bezier-hartlijn, lanes per segment en de verbindingen tussen
    /// lanes bij elk knooppunt. Alle soorten hebben stabiele ID's (E01-05) en staan in dichte NativeLists op hun
    /// slotnummer, zodat jobs (routeplanning, verkeer) ze direct kunnen lezen. Verbindingen worden afgeleid: elke
    /// wijziging aan een segment bouwt ze opnieuw op voor de knooppunten aan beide uiteinden. Alleen voor de main thread.
    /// </summary>
    public sealed class RoadNetwork : IDisposable
    {
        /// <summary>Zoveel segmenten kunnen hoogstens bij één knooppunt samenkomen.</summary>
        public const int MaxArms = 8;

        /// <summary>Zoveel lanes kan een segment hoogstens hebben.</summary>
        public const int MaxLanes = 15;

        /// <summary>Het uiteinde van een segment moet tot op deze afstand (m) op zijn knooppunt liggen.</summary>
        public const float EndTolerance = 0.05f;

        private IdAllocator<NodeKind> _nodeIds;
        private IdAllocator<SegmentKind> _segmentIds;
        private IdAllocator<LaneKind> _laneIds;
        private NativeList<RoadNode> _nodes;
        private NativeList<RoadSegment> _segments;
        private NativeList<Lane> _lanes;
        private NativeParallelMultiHashMap<ulong, LaneConnection> _connections;

        public RoadNetwork(int initialCapacity = 1024)
        {
            _nodeIds = new IdAllocator<NodeKind>(initialCapacity, Allocator.Persistent);
            _segmentIds = new IdAllocator<SegmentKind>(initialCapacity, Allocator.Persistent);
            _laneIds = new IdAllocator<LaneKind>(initialCapacity * 4, Allocator.Persistent);
            _nodes = new NativeList<RoadNode>(initialCapacity, Allocator.Persistent);
            _segments = new NativeList<RoadSegment>(initialCapacity, Allocator.Persistent);
            _lanes = new NativeList<Lane>(initialCapacity * 4, Allocator.Persistent);
            _connections = new NativeParallelMultiHashMap<ulong, LaneConnection>(initialCapacity * 8, Allocator.Persistent);
        }

        /// <summary>Na elke wijziging: het segment dat is toegevoegd of verwijderd.</summary>
        public event Action<EntityId<SegmentKind>> SegmentChanged;

        public int NodeCount => _nodeIds.AliveCount;

        public int SegmentCount => _segmentIds.AliveCount;

        public int LaneCount => _laneIds.AliveCount;

        public int ConnectionCount => _connections.Count();

        public bool IsDisposed { get; private set; }

        public EntityId<NodeKind> AddNode(float3 position)
        {
            var id = _nodeIds.Allocate();
            Store(ref _nodes, id.Index, new RoadNode(position, default));
            return id;
        }

        /// <summary>
        /// Legt een segment tussen twee bestaande knooppunten, met lanes volgens <paramref name="profile"/>, en
        /// verbindt de lanes bij beide knooppunten opnieuw.
        /// </summary>
        /// <param name="curve">De hartlijn; begint op <paramref name="start"/> en eindigt op <paramref name="end"/>.</param>
        /// <param name="speedLimit">Maximumsnelheid in m/s.</param>
        public EntityId<SegmentKind> AddSegment(EntityId<NodeKind> start, EntityId<NodeKind> end, Bezier3 curve, IReadOnlyList<LaneSpec> profile, float speedLimit)
        {
            ValidateSegment(start, end, curve, profile);

            var id = _segmentIds.Allocate();
            Store(ref _segments, id.Index, new RoadSegment(start, end, curve, speedLimit, AllocateLanes(id, curve, profile)));
            Attach(start, id);
            Attach(end, id);
            Reconnect(start);
            Reconnect(end);
            SegmentChanged?.Invoke(id);
            return id;
        }

        /// <summary>
        /// Geeft een bestaand segment een ander dwarsprofiel en maximumsnelheid (upgraden), met dezelfde ID, hartlijn en
        /// knooppunten. De lanes worden nieuw aangemaakt en bij beide knooppunten opnieuw verbonden.
        /// </summary>
        public void Retype(EntityId<SegmentKind> id, IReadOnlyList<LaneSpec> profile, float speedLimit)
        {
            var segment = GetSegment(id);
            ValidateProfile(profile);
            ReplaceSegment(id, segment.Start, segment.End, segment.Curve, profile, speedLimit, segment);
        }

        /// <summary>
        /// Keert de rijrichting van een eenrichtingsweg om: begin en eind wisselen, de hartlijn loopt de andere kant op en de
        /// lanes houden hun plek. Een weg met verkeer in beide richtingen heeft niets om te keren.
        /// </summary>
        public void Reverse(EntityId<SegmentKind> id)
        {
            var segment = GetSegment(id);
            if (!IsOneWay(segment))
            {
                throw new InvalidOperationException("Alleen een eenrichtingsweg kan omgekeerd worden.");
            }

            var profile = new List<LaneSpec>(segment.Lanes.Length);
            foreach (var lane in segment.Lanes)
            {
                var spec = _lanes[lane.Index].Spec;
                profile.Add(new LaneSpec(spec.Types, spec.Direction, -spec.Offset, spec.Width));
            }

            ReplaceSegment(id, segment.End, segment.Start, segment.Curve.Reversed, profile, segment.SpeedLimit, segment);
        }

        /// <summary><c>true</c> als het verkeer op dit segment maar één kant op rijdt en er minstens één rijstrook is.</summary>
        public bool IsOneWay(RoadSegment segment)
        {
            var forward = false;
            var backward = false;
            foreach (var lane in segment.Lanes)
            {
                var spec = _lanes[lane.Index].Spec;
                if ((spec.Types & LaneTypes.Vehicles) == LaneTypes.None)
                {
                    continue;
                }

                if (spec.Direction == LaneDirection.Both)
                {
                    return false;
                }

                forward |= spec.Direction == LaneDirection.Forward;
                backward |= spec.Direction == LaneDirection.Backward;
            }

            return forward != backward;
        }

        /// <summary>Verwijdert een segment met zijn lanes; een knooppunt waar daarna niets meer aan vastzit, verdwijnt ook.</summary>
        public void RemoveSegment(EntityId<SegmentKind> id)
        {
            var segment = GetSegment(id);
            foreach (var lane in segment.Lanes)
            {
                _connections.Remove(Key(lane, true));
                _connections.Remove(Key(lane, false));
                _laneIds.Release(lane);
            }

            _segmentIds.Release(id);
            foreach (var node in new[] { segment.Start, segment.End })
            {
                if (!_nodeIds.IsAlive(node))
                {
                    continue;
                }

                Detach(node, id);
                if (_nodes[node.Index].Segments.Length == 0)
                {
                    _nodeIds.Release(node);
                }
                else
                {
                    Reconnect(node);
                }
            }

            SegmentChanged?.Invoke(id);
        }

        public bool Contains(EntityId<NodeKind> id) => _nodeIds.IsAlive(id);

        public bool Contains(EntityId<SegmentKind> id) => _segmentIds.IsAlive(id);

        public bool Contains(EntityId<LaneKind> id) => _laneIds.IsAlive(id);

        public RoadNode GetNode(EntityId<NodeKind> id) => _nodeIds.IsAlive(id) ? _nodes[id.Index] : throw Missing(id);

        public RoadSegment GetSegment(EntityId<SegmentKind> id) => _segmentIds.IsAlive(id) ? _segments[id.Index] : throw Missing(id);

        public Lane GetLane(EntityId<LaneKind> id) => _laneIds.IsAlive(id) ? _lanes[id.Index] : throw Missing(id);

        /// <summary>Alle verbindingen vanaf het eind van <paramref name="lane"/> in zijn rijrichting.</summary>
        public void GetConnections(EntityId<LaneKind> lane, List<LaneConnection> results)
        {
            var direction = GetLane(lane).Spec.Direction;
            GetConnections(lane, direction != LaneDirection.Backward, results);
        }

        /// <summary>Verbindingen vanaf het eind- (<paramref name="atSegmentEnd"/>) of beginknooppunt van een lane; nodig voor stoepen.</summary>
        public void GetConnections(EntityId<LaneKind> lane, bool atSegmentEnd, List<LaneConnection> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            if (!_laneIds.IsAlive(lane) || !_connections.TryGetFirstValue(Key(lane, atSegmentEnd), out var connection, out var iterator))
            {
                return;
            }

            do
            {
                results.Add(connection);
            }
            while (_connections.TryGetNextValue(out connection, ref iterator));

            // De hashmap geeft geen vaste volgorde; sorteren houdt alles deterministisch.
            results.Sort((a, b) => a.To.CompareTo(b.To));
        }

        /// <summary>Alle levende knooppunten, op volgorde van ID.</summary>
        public IEnumerable<EntityId<NodeKind>> Nodes()
        {
            for (var i = 0; i < _nodeIds.SlotCount; i++)
            {
                var id = _nodeIds.GetAliveAt(i);
                if (id.IsValid)
                {
                    yield return id;
                }
            }
        }

        /// <summary>Alle levende segmenten, op volgorde van ID.</summary>
        public IEnumerable<EntityId<SegmentKind>> Segments()
        {
            for (var i = 0; i < _segmentIds.SlotCount; i++)
            {
                var id = _segmentIds.GetAliveAt(i);
                if (id.IsValid)
                {
                    yield return id;
                }
            }
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            _nodeIds.Dispose();
            _segmentIds.Dispose();
            _laneIds.Dispose();
            _nodes.Dispose();
            _segments.Dispose();
            _lanes.Dispose();
            _connections.Dispose();
            IsDisposed = true;
        }

        /// <summary>Schrijft ID-allocators en slots; verbindingen zijn afgeleid en worden bij laden opnieuw opgebouwd.</summary>
        internal void WriteTo(SaveWriter writer)
        {
            _nodeIds.WriteTo(writer);
            _segmentIds.WriteTo(writer);
            _laneIds.WriteTo(writer);
            writer.WriteShuffled<RoadNode>(_nodes.AsArray().AsReadOnlySpan());
            writer.WriteShuffled<RoadSegment>(_segments.AsArray().AsReadOnlySpan());
            writer.WriteShuffled<Lane>(_lanes.AsArray().AsReadOnlySpan());
        }

        /// <summary>Vervangt de inhoud door een gelezen staat en verbindt alle knooppunten opnieuw.</summary>
        internal void Restore(RoadNetworkState state)
        {
            _nodeIds.Restore(state.Nodes);
            _segmentIds.Restore(state.Segments);
            _laneIds.Restore(state.Lanes);
            Replace(ref _nodes, state.NodeSlots);
            Replace(ref _segments, state.SegmentSlots);
            Replace(ref _lanes, state.LaneSlots);
            _connections.Clear();
            foreach (var node in Nodes())
            {
                Reconnect(node);
            }

            foreach (var segment in Segments())
            {
                SegmentChanged?.Invoke(segment);
            }
        }

        private FixedList128Bytes<EntityId<LaneKind>> AllocateLanes(EntityId<SegmentKind> id, Bezier3 curve, IReadOnlyList<LaneSpec> profile)
        {
            var lanes = new FixedList128Bytes<EntityId<LaneKind>>();
            foreach (var spec in profile)
            {
                var lane = _laneIds.Allocate();
                var shifted = curve.Offset(spec.Offset);
                Store(ref _lanes, lane.Index, new Lane(id, spec, spec.Direction == LaneDirection.Backward ? shifted.Reversed : shifted));
                lanes.Add(lane);
            }

            return lanes;
        }

        /// <summary>Vervangt de lanes en de vorm van een bestaand segment en verbindt beide knooppunten opnieuw.</summary>
        private void ReplaceSegment(EntityId<SegmentKind> id, EntityId<NodeKind> start, EntityId<NodeKind> end, Bezier3 curve, IReadOnlyList<LaneSpec> profile, float speedLimit, RoadSegment old)
        {
            foreach (var lane in old.Lanes)
            {
                _connections.Remove(Key(lane, true));
                _connections.Remove(Key(lane, false));
                _laneIds.Release(lane);
            }

            Store(ref _segments, id.Index, new RoadSegment(start, end, curve, speedLimit, AllocateLanes(id, curve, profile)));
            Reconnect(start);
            Reconnect(end);
            SegmentChanged?.Invoke(id);
        }

        private static void ValidateProfile(IReadOnlyList<LaneSpec> profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (profile.Count == 0 || profile.Count > MaxLanes)
            {
                throw new ArgumentException($"Een segment heeft 1 tot {MaxLanes} lanes, niet {profile.Count}.", nameof(profile));
            }
        }

        private void ValidateSegment(EntityId<NodeKind> start, EntityId<NodeKind> end, Bezier3 curve, IReadOnlyList<LaneSpec> profile)
        {
            ValidateProfile(profile);
            if (start == end)
            {
                throw new ArgumentException("Een segment verbindt twee verschillende knooppunten.", nameof(end));
            }

            var from = GetNode(start);
            var to = GetNode(end);
            if (from.Segments.Length >= MaxArms || to.Segments.Length >= MaxArms)
            {
                throw new InvalidOperationException($"Bij een knooppunt komen hoogstens {MaxArms} segmenten samen.");
            }

            if (math.distance(curve.P0, from.Position) > EndTolerance || math.distance(curve.P3, to.Position) > EndTolerance)
            {
                throw new ArgumentException("De hartlijn moet op het begin- en eindknooppunt liggen.", nameof(curve));
            }
        }

        /// <summary>Bouwt de verbindingen bij een knooppunt opnieuw op uit de lanes van alle segmenten die er samenkomen.</summary>
        private void Reconnect(EntityId<NodeKind> node)
        {
            var segments = _nodes[node.Index].Segments;
            var arms = new List<NodeArm>(segments.Length);
            var lanes = new List<EntityId<LaneKind>>();
            foreach (var segmentId in segments)
            {
                var segment = _segments[segmentId.Index];
                var nodeIsEnd = segment.End == node;
                var heading = nodeIsEnd ? -segment.Curve.Direction(1f) : segment.Curve.Direction(0f);
                var armLanes = new List<ArmLane>(segment.Lanes.Length);
                foreach (var lane in segment.Lanes)
                {
                    _connections.Remove(Key(lane, nodeIsEnd));
                    armLanes.Add(new ArmLane(lanes.Count, _lanes[lane.Index].Spec));
                    lanes.Add(lane);
                }

                arms.Add(new NodeArm(heading.xz, nodeIsEnd, armLanes));
            }

            var armOf = new Dictionary<int, NodeArm>();
            foreach (var arm in arms)
            {
                foreach (var lane in arm.Lanes)
                {
                    armOf[lane.Id] = arm;
                }
            }

            foreach (var pair in LaneConnector.Connect(arms))
            {
                var fromArm = armOf[pair.From];
                var toArm = armOf[pair.To];
                var from = lanes[pair.From];
                var to = lanes[pair.To];
                var (exit, exitDirection) = Exit(_lanes[from.Index], fromArm.NodeIsEnd);
                var (entry, entryDirection) = Entry(_lanes[to.Index], toArm.NodeIsEnd);
                var curve = Bezier3.FromTangents(exit, exitDirection, entry, entryDirection);
                _connections.Add(Key(from, fromArm.NodeIsEnd), new LaneConnection(from, to, node, pair.Turn, curve));
            }
        }

        /// <summary>Waar en in welke richting het verkeer een lane verlaat bij het knooppunt aan dat uiteinde van het segment.</summary>
        private static (float3 Point, float3 Direction) Exit(Lane lane, bool atSegmentEnd)
        {
            if (lane.Spec.Direction != LaneDirection.Both)
            {
                return (lane.Curve.P3, lane.Curve.Direction(1f));
            }

            return atSegmentEnd ? (lane.Curve.P3, lane.Curve.Direction(1f)) : (lane.Curve.P0, -lane.Curve.Direction(0f));
        }

        /// <summary>Waar en in welke richting het verkeer een lane op komt vanaf het knooppunt aan dat uiteinde van het segment.</summary>
        private static (float3 Point, float3 Direction) Entry(Lane lane, bool atSegmentEnd)
        {
            if (lane.Spec.Direction != LaneDirection.Both)
            {
                return (lane.Curve.P0, lane.Curve.Direction(0f));
            }

            return atSegmentEnd ? (lane.Curve.P3, -lane.Curve.Direction(1f)) : (lane.Curve.P0, lane.Curve.Direction(0f));
        }

        private void Attach(EntityId<NodeKind> node, EntityId<SegmentKind> segment)
        {
            var current = _nodes[node.Index];
            var segments = current.Segments;
            segments.Add(segment);
            _nodes[node.Index] = new RoadNode(current.Position, segments);
        }

        private void Detach(EntityId<NodeKind> node, EntityId<SegmentKind> segment)
        {
            var current = _nodes[node.Index];
            var segments = current.Segments;
            segments.RemoveAt(segments.IndexOf(segment));
            _nodes[node.Index] = new RoadNode(current.Position, segments);
        }

        /// <summary>Sleutel voor de verbindingen vanaf een uiteinde van een lane: slotnummer en welk uiteinde.</summary>
        private static ulong Key(EntityId<LaneKind> lane, bool atSegmentEnd) => ((ulong)(uint)lane.Index << 1) | (atSegmentEnd ? 1UL : 0UL);

        private static void Store<T>(ref NativeList<T> list, int index, T value) where T : unmanaged
        {
            if (index >= list.Length)
            {
                list.Resize(index + 1, NativeArrayOptions.ClearMemory);
            }

            list[index] = value;
        }

        private static void Replace<T>(ref NativeList<T> list, T[] values) where T : unmanaged
        {
            list.Clear();
            list.ResizeUninitialized(values.Length);
            list.AsArray().CopyFrom(values);
        }

        private static KeyNotFoundException Missing<TKind>(EntityId<TKind> id) where TKind : struct, IEntityKind =>
            new KeyNotFoundException($"{id} bestaat niet (meer) in het wegennet.");
    }
}
