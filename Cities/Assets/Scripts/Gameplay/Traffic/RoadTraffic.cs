using System;
using System.Collections.Generic;
using Cities.Core.Ids;
using Cities.Simulation.Network;
using Cities.Simulation.Vehicles;
using Unity.Mathematics;

namespace Cities.Gameplay.Traffic
{
    /// <summary>Een voertuig dat op het wegennet rijdt, met zijn plek in de wereld.</summary>
    public readonly struct TrafficCar
    {
        public TrafficCar(uint id, float3 position, float yaw, float speed)
        {
            Id = id;
            Position = position;
            Yaw = yaw;
            Speed = speed;
        }

        public uint Id { get; }

        public float3 Position { get; }

        /// <summary>Kijkrichting in radialen om de Y-as.</summary>
        public float Yaw { get; }

        public float Speed { get; }
    }

    /// <summary>
    /// Voertuigen op het wegennet (E26-05). Elk voertuig rijdt over een segment in één richting met <see cref="LaneTraffic"/> (IDM); aan het
    /// eind kiest het een volgend segment bij de knoop, of keert om op een doodlopende weg. Is er op het volgende segment geen ruimte, dan blijft
    /// het wachten en loopt de rij vol: zo ontstaan files bij drukte. De bezetting per segment volgt <see cref="Congestion"/>.
    /// </summary>
    public sealed class RoadTraffic
    {
        public const float CarLength = 4.5f;

        private sealed class Lane
        {
            public readonly List<VehicleAgent> Agents = new List<VehicleAgent>();
            public readonly List<uint> Ids = new List<uint>();
            public bool HasPlan;
            public EntityId<SegmentKind> PlanSegment;
            public bool PlanForward;
        }

        private readonly RoadNetwork _network;
        private uint _state;
        private readonly Dictionary<(EntityId<SegmentKind>, bool), Lane> _lanes = new Dictionary<(EntityId<SegmentKind>, bool), Lane>();
        private readonly List<(EntityId<SegmentKind> Segment, bool Forward)> _keys = new List<(EntityId<SegmentKind>, bool)>();
        private readonly List<(uint Id, VehicleAgent Agent, EntityId<SegmentKind> Segment, bool Forward)> _moves = new List<(uint, VehicleAgent, EntityId<SegmentKind>, bool)>();
        private readonly Dictionary<(EntityId<SegmentKind>, bool), (EntityId<SegmentKind>, bool)> _plans = new Dictionary<(EntityId<SegmentKind>, bool), (EntityId<SegmentKind>, bool)>();
        private uint _nextId = 1;

        public RoadTraffic(RoadNetwork network, int seed)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _state = unchecked((uint)seed) | 1u;
        }

        public int Count { get; private set; }

        /// <summary>Gemiddelde bezetting (0 tot 1) van de segmenten waarop voertuigen rijden.</summary>
        public float AverageOccupancy { get; private set; }

        /// <summary>Aandeel voertuigen dat (bijna) stilstaat.</summary>
        public float StandstillShare { get; private set; }

        // Xorshift: de keuzes hoeven alleen gespreid en herhaalbaar te zijn, niet onvoorspelbaar.
        private int Next(int bound)
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return (int)(_state % (uint)bound);
        }

        /// <summary>Zet een voertuig op een willekeurig segment; <c>false</c> als er geen weg is of geen plek.</summary>
        public bool Spawn()
        {
            var segments = new List<EntityId<SegmentKind>>(_network.Segments());
            if (segments.Count == 0)
            {
                return false;
            }

            var segment = segments[Next(segments.Count)];
            return Enter(segment, Next(2) == 0, _nextId++, new VehicleAgent(), spawn: true);
        }

        /// <summary>Verwijdert voertuigen tot er hoogstens <paramref name="target"/> over zijn.</summary>
        public void Trim(int target)
        {
            foreach (var lane in _lanes.Values)
            {
                while (Count > target && lane.Agents.Count > 0)
                {
                    lane.Agents.RemoveAt(lane.Agents.Count - 1);
                    lane.Ids.RemoveAt(lane.Ids.Count - 1);
                    Count--;
                }
            }
        }

        public void Step(float dt)
        {
            if (!(dt > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(dt));
            }

            _keys.Clear();
            foreach (var key in _lanes.Keys)
            {
                _keys.Add((key.Item1, key.Item2));
            }

            _moves.Clear();
            var occupancy = 0f;
            var lanesWithCars = 0;
            var stopped = 0;
            foreach (var (segment, forward) in _keys)
            {
                var lane = _lanes[(segment, forward)];
                if (lane.Agents.Count == 0)
                {
                    continue;
                }

                if (!_network.Contains(segment))
                {
                    Count -= lane.Agents.Count;
                    lane.Agents.Clear();
                    lane.Ids.Clear();
                    continue;
                }

                var length = _network.GetSegment(segment).Length;
                var blocked = LeaderWaitsAtEnd(lane, segment, forward, length);
                LaneTraffic.Step(lane.Agents, length, blocked, dt);
                occupancy += Congestion.Occupancy(lane.Agents, length);
                lanesWithCars++;
                foreach (var agent in lane.Agents)
                {
                    if (agent.Speed < 0.5f)
                    {
                        stopped++;
                    }
                }

                // Het voorste voertuig is voorbij het einde: naar het volgende segment (als dat ruimte heeft).
                if (lane.Agents[0].Position >= length && !blocked)
                {
                    _moves.Add((lane.Ids[0], lane.Agents[0], segment, forward));
                    lane.Agents.RemoveAt(0);
                    lane.Ids.RemoveAt(0);
                    _plans[(segment, forward)] = (lane.PlanSegment, lane.PlanForward);
                    lane.HasPlan = false;
                }
            }

            AverageOccupancy = lanesWithCars == 0 ? 0f : occupancy / lanesWithCars;
            StandstillShare = Count == 0 ? 0f : (float)stopped / Count;
            foreach (var (id, agent, segment, forward) in _moves)
            {
                var (next, nextForward) = _plans[(segment, forward)];
                var speed = agent.Speed;
                Count--;
                var placed = Enter(next, nextForward, id, new VehicleAgent { Speed = speed }, spawn: false);
                if (!placed)
                {
                    Enter(segment, forward, id, new VehicleAgent { Speed = 0f }, spawn: false);
                }
            }
        }

        /// <summary>De voertuigen met hun plek in de wereld.</summary>
        public void Collect(List<TrafficCar> cars)
        {
            cars.Clear();
            foreach (var pair in _lanes)
            {
                var (segment, forward) = pair.Key;
                if (!_network.Contains(segment))
                {
                    continue;
                }

                var road = _network.GetSegment(segment);
                var curve = forward ? road.Curve : road.Curve.Reversed;
                var lane = pair.Value;
                for (var i = 0; i < lane.Agents.Count; i++)
                {
                    var t = road.Length > 0f ? math.saturate(lane.Agents[i].Position / road.Length) : 0f;
                    var direction = curve.Direction(t);
                    var point = curve.Position(t) + (Bezier3.Right(direction) * 1.8f);
                    cars.Add(new TrafficCar(lane.Ids[i], point, math.atan2(direction.x, direction.z), lane.Agents[i].Speed));
                }
            }
        }

        private bool LeaderWaitsAtEnd(Lane lane, EntityId<SegmentKind> segment, bool forward, float length)
        {
            var leader = lane.Agents[0];
            if (leader.Position + leader.Speed < length - 1f)
            {
                return false;
            }

            if (!lane.HasPlan)
            {
                lane.PlanSegment = ChooseNext(segment, forward, out var planForward);
                lane.PlanForward = planForward;
                lane.HasPlan = true;
            }

            if (!_lanes.TryGetValue((lane.PlanSegment, lane.PlanForward), out var target) || target.Agents.Count == 0)
            {
                return false;
            }

            var last = target.Agents[target.Agents.Count - 1];
            return last.Position - last.Length < last.Driver.MinGap + CarLength;
        }

        private bool Enter(EntityId<SegmentKind> segment, bool forward, uint id, VehicleAgent agent, bool spawn)
        {
            var road = _network.GetSegment(segment);
            if (!_lanes.TryGetValue((segment, forward), out var lane))
            {
                lane = new Lane();
                _lanes[(segment, forward)] = lane;
            }

            if (lane.Agents.Count > 0)
            {
                var last = lane.Agents[lane.Agents.Count - 1];
                if (last.Position - last.Length < CarLength + 2f)
                {
                    return false;
                }
            }

            var spec = new VehicleSpec { Category = VehicleCategory.PassengerCar, MaxSpeedKmh = 50f, AccelerationMs2 = 2f, BrakingMs2 = 3f };
            var limit = Math.Max(5f, road.SpeedLimit);
            agent.Driver = Idm.For(spec, limit);
            agent.Length = CarLength;
            agent.Position = 0f;
            if (spawn)
            {
                agent.Speed = 0f;
            }

            lane.Agents.Add(agent);
            lane.Ids.Add(id);
            Count++;
            return true;
        }

        private EntityId<SegmentKind> ChooseNext(EntityId<SegmentKind> segment, bool forward, out bool nextForward)
        {
            var road = _network.GetSegment(segment);
            var node = forward ? road.End : road.Start;
            var choices = new List<EntityId<SegmentKind>>();
            foreach (var option in _network.GetNode(node).Segments)
            {
                if (!option.Equals(segment))
                {
                    choices.Add(option);
                }
            }

            if (choices.Count == 0)
            {
                nextForward = !forward;
                return segment;
            }

            var chosen = choices[Next(choices.Count)];
            nextForward = _network.GetSegment(chosen).Start.Equals(node);
            return chosen;
        }
    }
}
