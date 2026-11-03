using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Cities.Simulation.Vehicles
{
    public enum ParkingKind : byte
    {
        /// <summary>Plekken langs de weg van één segment (zie <c>ParkingSlots</c>).</summary>
        Street = 0,

        /// <summary>Parkeerplekken bij een gebouw.</summary>
        Building = 1,

        /// <summary>Een parkeergarage.</summary>
        Garage = 2,
    }

    /// <summary>Een groep parkeerplekken op één plaats (E08-10): capaciteit en hoeveel er bezet zijn.</summary>
    public sealed class ParkingLot
    {
        internal ParkingLot(int id, ParkingKind kind, ulong owner, float3 position, int capacity, int occupied)
        {
            Id = id;
            Kind = kind;
            Owner = owner;
            Position = position;
            Capacity = capacity;
            Occupied = occupied;
        }

        public int Id { get; }

        public ParkingKind Kind { get; }

        /// <summary>Het segment of gebouw waar de plekken bij horen (verpakt ID).</summary>
        public ulong Owner { get; }

        public float3 Position { get; }

        public int Capacity { get; internal set; }

        public int Occupied { get; internal set; }

        public int Free => Math.Max(0, Capacity - Occupied);
    }

    /// <summary>
    /// Alle parkeerplekken van de stad (E08-10). Een lot krijgt een stabiel ID (volgnummer); een lot dat verdwijnt (gesloopt gebouw of
    /// segment) wordt gesloten met <see cref="Close"/> zodat ID's in de save blijven kloppen. Bezetting is een teller: welk voertuig waar
    /// staat is aan de voertuig-agent, het lot weet alleen hoeveel er staan.
    /// </summary>
    public sealed class ParkingRegistry
    {
        private readonly List<ParkingLot> _lots = new List<ParkingLot>();

        public IReadOnlyList<ParkingLot> Lots => _lots;

        public int Capacity
        {
            get
            {
                var total = 0;
                foreach (var lot in _lots)
                {
                    total += lot.Capacity;
                }

                return total;
            }
        }

        public int Occupied
        {
            get
            {
                var total = 0;
                foreach (var lot in _lots)
                {
                    total += lot.Occupied;
                }

                return total;
            }
        }

        public ParkingLot Add(ParkingKind kind, ulong owner, float3 position, int capacity)
        {
            if (!Enum.IsDefined(typeof(ParkingKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Onbekend soort parkeerplek.");
            }

            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "De capaciteit kan niet negatief zijn.");
            }

            var lot = new ParkingLot(_lots.Count, kind, owner, position, capacity, 0);
            _lots.Add(lot);
            return lot;
        }

        /// <summary>Sluit alle lots van deze eigenaar: er kan niet meer geparkeerd worden. Wie er staat, rijdt weg via <see cref="Leave"/>.</summary>
        public void Close(ParkingKind kind, ulong owner)
        {
            foreach (var lot in _lots)
            {
                if (lot.Kind == kind && lot.Owner == owner)
                {
                    lot.Capacity = 0;
                }
            }
        }

        /// <summary>Parkeert op het lot; <c>false</c> als het vol of onbekend is.</summary>
        public bool TryPark(int lotId)
        {
            if (lotId < 0 || lotId >= _lots.Count || _lots[lotId].Free == 0)
            {
                return false;
            }

            _lots[lotId].Occupied++;
            return true;
        }

        public void Leave(int lotId)
        {
            if (lotId >= 0 && lotId < _lots.Count && _lots[lotId].Occupied > 0)
            {
                _lots[lotId].Occupied--;
            }
        }

        /// <summary>
        /// Het dichtstbijzijnde lot met een vrije plek binnen <paramref name="radius"/> meter van <paramref name="position"/>, of <c>null</c>.
        /// Bij gelijke afstand wint het laagste ID, zodat de uitkomst deterministisch is.
        /// </summary>
        public ParkingLot FindNearestFree(float3 position, float radius)
        {
            ParkingLot best = null;
            var bestDistance = float.MaxValue;
            foreach (var lot in _lots)
            {
                if (lot.Free == 0)
                {
                    continue;
                }

                var distance = math.distance(lot.Position, position);
                if (distance <= radius && distance < bestDistance)
                {
                    best = lot;
                    bestDistance = distance;
                }
            }

            return best;
        }

        internal void Restore(IEnumerable<(ParkingKind Kind, ulong Owner, float3 Position, int Capacity, int Occupied)> lots)
        {
            _lots.Clear();
            foreach (var l in lots)
            {
                _lots.Add(new ParkingLot(_lots.Count, l.Kind, l.Owner, l.Position, l.Capacity, l.Occupied));
            }
        }
    }

    /// <summary>
    /// De parkeerzoektocht van één voertuig (E08-10). Het zoekt een vrije plek rond de bestemming; is alles binnen de straal vol, dan rijdt het
    /// een rondje (<see cref="Rounds"/>) en zoekt daarna verder weg. Na <see cref="MaxRounds"/> rondjes geeft het op (<see cref="GaveUp"/>).
    /// </summary>
    public struct ParkingSearch
    {
        public const float StartRadius = 150f;
        public const float RadiusGrowth = 150f;
        public const int MaxRounds = 5;

        public float Radius { get; private set; }

        public int Rounds { get; private set; }

        public bool GaveUp => Rounds >= MaxRounds;

        /// <summary>Probeert te parkeren; geeft het lot (nu bezet) of <c>null</c>, en telt dan een rondje erbij.</summary>
        public ParkingLot TryPark(ParkingRegistry registry, float3 destination)
        {
            if (registry is null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            if (GaveUp)
            {
                return null;
            }

            var radius = Radius > 0f ? Radius : StartRadius;
            var lot = registry.FindNearestFree(destination, radius);
            if (lot != null && registry.TryPark(lot.Id))
            {
                return lot;
            }

            Radius = radius + RadiusGrowth;
            Rounds++;
            return null;
        }
    }
}
