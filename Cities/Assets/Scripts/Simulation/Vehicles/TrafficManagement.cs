using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Ids;
using Cities.Core.Persistence;
using Cities.Simulation.Network;

namespace Cities.Simulation.Vehicles
{
    /// <summary>Wat de speler per segment heeft ingesteld voor het verkeersbeheer (E08-14).</summary>
    public readonly struct SegmentPolicy : IEquatable<SegmentPolicy>
    {
        public SegmentPolicy(int tollCents, bool truckBan, bool priorityLane, float speedLimit)
        {
            TollCents = tollCents;
            TruckBan = truckBan;
            PriorityLane = priorityLane;
            SpeedLimit = speedLimit;
        }

        /// <summary>Tol in centen per passage voor betalend verkeer; 0 is gratis.</summary>
        public int TollCents { get; }

        /// <summary>Vrachtverbod: vrachtwagens en bestelbusjes mogen dit segment niet op.</summary>
        public bool TruckBan { get; }

        /// <summary>Busbaan: alleen OV en nooddiensten mogen dit segment op.</summary>
        public bool PriorityLane { get; }

        /// <summary>Eigen snelheidslimiet in m/s, of 0 als de limiet van de weg geldt.</summary>
        public float SpeedLimit { get; }

        public bool IsDefault => TollCents == 0 && !TruckBan && !PriorityLane && SpeedLimit <= 0f;

        public bool Equals(SegmentPolicy other) =>
            TollCents == other.TollCents && TruckBan == other.TruckBan && PriorityLane == other.PriorityLane && SpeedLimit.Equals(other.SpeedLimit);

        public override bool Equals(object obj) => obj is SegmentPolicy other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(TollCents, TruckBan, PriorityLane, SpeedLimit);
    }

    /// <summary>
    /// Verkeersbeheer (E08-14): tol, vrachtverbod, prioriteit voor OV en nooddiensten en snelheidslimieten per segment. Alleen
    /// afwijkende instellingen worden bewaard; sectie <c>TMGT</c>. OV (bus, tram) en nooddiensten (<see cref="VehicleCategory.Service"/>)
    /// krijgen voorrang: ze zijn tolvrij, mogen op de busbaan en rijden een deel boven de limiet.
    /// </summary>
    public sealed class TrafficManagement
    {
        /// <summary>Nooddiensten mogen zoveel keer de limiet rijden.</summary>
        public const float EmergencySpeedFactor = 1.25f;

        private readonly SortedDictionary<ulong, SegmentPolicy> _policies = new SortedDictionary<ulong, SegmentPolicy>();

        public int Count => _policies.Count;

        public SegmentPolicy Get(EntityId<SegmentKind> segment) =>
            _policies.TryGetValue(segment.Packed, out var policy) ? policy : default;

        public void SetToll(EntityId<SegmentKind> segment, int cents)
        {
            if (cents < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cents), cents, "Tol kan niet negatief zijn.");
            }

            var p = Get(segment);
            Set(segment, new SegmentPolicy(cents, p.TruckBan, p.PriorityLane, p.SpeedLimit));
        }

        public void SetTruckBan(EntityId<SegmentKind> segment, bool ban)
        {
            var p = Get(segment);
            Set(segment, new SegmentPolicy(p.TollCents, ban, p.PriorityLane, p.SpeedLimit));
        }

        public void SetPriorityLane(EntityId<SegmentKind> segment, bool priority)
        {
            var p = Get(segment);
            Set(segment, new SegmentPolicy(p.TollCents, p.TruckBan, priority, p.SpeedLimit));
        }

        /// <summary>Zet een eigen snelheidslimiet in m/s; 0 haalt hem weg.</summary>
        public void SetSpeedLimit(EntityId<SegmentKind> segment, float limit)
        {
            if (float.IsNaN(limit) || limit < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limiet moet 0 of positief zijn.");
            }

            var p = Get(segment);
            Set(segment, new SegmentPolicy(p.TollCents, p.TruckBan, p.PriorityLane, limit));
        }

        /// <summary>OV en nooddiensten krijgen voorrang.</summary>
        public static bool HasPriority(VehicleCategory category) =>
            category == VehicleCategory.Bus || category == VehicleCategory.Tram || category == VehicleCategory.Service;

        /// <summary>Mag dit voertuig het segment op (vrachtverbod, busbaan)?</summary>
        public bool Allows(EntityId<SegmentKind> segment, VehicleCategory category)
        {
            var p = Get(segment);
            if (p.PriorityLane && !HasPriority(category))
            {
                return false;
            }

            return !(p.TruckBan && (category == VehicleCategory.Truck || category == VehicleCategory.Van));
        }

        /// <summary>De tol in centen die dit voertuig betaalt; voorrangsvoertuigen betalen niet.</summary>
        public int TollFor(EntityId<SegmentKind> segment, VehicleCategory category) =>
            HasPriority(category) ? 0 : Get(segment).TollCents;

        /// <summary>De geldende snelheidslimiet (m/s): eigen instelling of die van de weg, voor nooddiensten verhoogd.</summary>
        public float EffectiveSpeedLimit(EntityId<SegmentKind> segment, float roadLimit, VehicleCategory category)
        {
            var own = Get(segment).SpeedLimit;
            var limit = own > 0f ? own : roadLimit;
            return category == VehicleCategory.Service ? limit * EmergencySpeedFactor : limit;
        }

        /// <summary>Wist de instellingen van segmenten waarvoor <paramref name="alive"/> <c>false</c> geeft.</summary>
        public void Prune(Func<EntityId<SegmentKind>, bool> alive)
        {
            foreach (var key in _policies.Keys.Where(k => !alive(EntityId<SegmentKind>.FromPacked(k))).ToList())
            {
                _policies.Remove(key);
            }
        }

        internal IEnumerable<KeyValuePair<ulong, SegmentPolicy>> Entries() => _policies;

        internal void Restore(IEnumerable<KeyValuePair<ulong, SegmentPolicy>> entries)
        {
            _policies.Clear();
            foreach (var e in entries)
            {
                _policies[e.Key] = e.Value;
            }
        }

        private void Set(EntityId<SegmentKind> segment, SegmentPolicy policy)
        {
            if (policy.IsDefault)
            {
                _policies.Remove(segment.Packed);
            }
            else
            {
                _policies[segment.Packed] = policy;
            }
        }
    }

    /// <summary>Sectie <c>TMGT</c>: de verkeersbeheerinstellingen per segment.</summary>
    public sealed class TrafficManagementSection : ISaveSection
    {
        public const string SectionId = "TMGT";

        private const int MaxEntries = 4_000_000;

        private readonly TrafficManagement _management;
        private readonly Func<EntityId<SegmentKind>, bool> _segmentExists;

        public TrafficManagementSection(TrafficManagement management, Func<EntityId<SegmentKind>, bool> segmentExists = null)
        {
            _management = management ?? throw new ArgumentNullException(nameof(management));
            _segmentExists = segmentExists;
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            if (_segmentExists != null)
            {
                _management.Prune(_segmentExists);
            }

            writer.Write(_management.Count);
            foreach (var entry in _management.Entries())
            {
                writer.Write((long)entry.Key);
                writer.Write(entry.Value.TollCents);
                writer.Write(entry.Value.TruckBan);
                writer.Write(entry.Value.PriorityLane);
                writer.Write(entry.Value.SpeedLimit);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > MaxEntries)
            {
                throw reader.Corrupt($"onmogelijk aantal instellingen ({count})");
            }

            var entries = new List<KeyValuePair<ulong, SegmentPolicy>>(count);
            for (var i = 0; i < count; i++)
            {
                var key = (ulong)reader.ReadInt64();
                var toll = reader.ReadInt32();
                var ban = reader.ReadBool();
                var lane = reader.ReadBool();
                var speed = reader.ReadSingle();
                if (toll < 0 || float.IsNaN(speed) || speed < 0f)
                {
                    throw reader.Corrupt("ongeldige instelling");
                }

                entries.Add(new KeyValuePair<ulong, SegmentPolicy>(key, new SegmentPolicy(toll, ban, lane, speed)));
            }

            reader.ExpectEnd();
            return () => _management.Restore(entries);
        }
    }
}
