using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;
using Unity.Mathematics;

namespace Cities.Simulation.Disasters
{
    /// <summary>Een schuilplaats of veilige zone (E15-10) met een beperkte capaciteit.</summary>
    public sealed class Shelter
    {
        public Shelter(int id, float2 position, int capacity, int occupants = 0)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Een schuilplaats heeft plek voor minstens één persoon.");
            }

            if (occupants < 0 || occupants > capacity)
            {
                throw new ArgumentOutOfRangeException(nameof(occupants), occupants, "Het aantal bewoners ligt tussen 0 en de capaciteit.");
            }

            Id = id;
            Position = position;
            Capacity = capacity;
            Occupants = occupants;
        }

        public int Id { get; }

        public float2 Position { get; }

        public int Capacity { get; }

        public int Occupants { get; internal set; }

        public int Free => Capacity - Occupants;
    }

    /// <summary>Iemand die weg moet: waar hij is en of hij een eigen auto heeft of op een bus moet wachten.</summary>
    public readonly struct Evacuee
    {
        public Evacuee(int id, float2 position, bool hasOwnTransport)
        {
            Id = id;
            Position = position;
            HasOwnTransport = hasOwnTransport;
        }

        public int Id { get; }

        public float2 Position { get; }

        public bool HasOwnTransport { get; }
    }

    /// <summary>De route van één geëvacueerde naar zijn schuilplaats.</summary>
    public readonly struct EvacuationRoute
    {
        public EvacuationRoute(int evacueeId, int shelterId, float distance, bool byBus)
        {
            EvacueeId = evacueeId;
            ShelterId = shelterId;
            Distance = distance;
            ByBus = byBus;
        }

        public int EvacueeId { get; }

        public int ShelterId { get; }

        /// <summary>Rechte afstand in meter van de plek van de evacué naar de schuilplaats.</summary>
        public float Distance { get; }

        public bool ByBus { get; }
    }

    /// <summary>Uitkomst van een evacuatie: wie waarheen gaat, wie nergens heen kan en hoeveel dagen de bussen nodig hebben.</summary>
    public sealed class EvacuationResult
    {
        internal EvacuationResult(IReadOnlyList<EvacuationRoute> routes, IReadOnlyList<int> unplaced, int busDays)
        {
            Routes = routes;
            Unplaced = unplaced;
            BusDays = busDays;
        }

        public IReadOnlyList<EvacuationRoute> Routes { get; }

        /// <summary>ID's van mensen voor wie geen schuilplaats met plek meer was.</summary>
        public IReadOnlyList<int> Unplaced { get; }

        /// <summary>Dagen die de evacuatiebussen nodig hebben om iedereen zonder eigen vervoer weg te brengen.</summary>
        public int BusDays { get; }

        public int Evacuated => Routes.Count;
    }

    /// <summary>
    /// Evacuatie (E15-10): schuilplaatsen met een capaciteit en evacuatiebussen. Elke evacué volgt de route naar de dichtstbijzijnde schuilplaats
    /// die nog plek heeft; wie een eigen auto heeft vertrekt meteen, de rest wacht op de bussen (<see cref="BusLiftPerDay"/>). Het toewijzen is
    /// deterministisch: mensen worden op id verwerkt en bij gelijke afstand wint de laagste schuilplaats-id.
    /// </summary>
    public sealed class EvacuationPlan
    {
        /// <summary>Plekken in één evacuatiebus.</summary>
        public const int BusCapacity = 40;

        /// <summary>Ritten die één bus per dag maakt.</summary>
        public const int TripsPerBusPerDay = 6;

        private readonly List<Shelter> _shelters = new List<Shelter>();
        private int _buses;

        public int NextId { get; private set; } = 1;

        public IReadOnlyList<Shelter> Shelters => _shelters;

        public int Buses
        {
            get => _buses;
            set => _buses = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "Het aantal bussen kan niet negatief zijn.");
        }

        /// <summary>Mensen die de bussen samen per dag wegbrengen.</summary>
        public int BusLiftPerDay => Buses * BusCapacity * TripsPerBusPerDay;

        public int TotalCapacity => _shelters.Sum(s => s.Capacity);

        public int FreePlaces => _shelters.Sum(s => s.Free);

        public Shelter AddShelter(float2 position, int capacity)
        {
            var shelter = new Shelter(NextId, position, capacity);
            NextId++;
            _shelters.Add(shelter);
            return shelter;
        }

        public bool RemoveShelter(int id) => _shelters.RemoveAll(s => s.Id == id) > 0;

        /// <summary>Wijst iedereen een schuilplaats toe en houdt de bezetting bij.</summary>
        public EvacuationResult Evacuate(IEnumerable<Evacuee> evacuees)
        {
            if (evacuees is null)
            {
                throw new ArgumentNullException(nameof(evacuees));
            }

            var routes = new List<EvacuationRoute>();
            var unplaced = new List<int>();
            var onBus = 0;
            foreach (var person in evacuees.OrderBy(e => e.Id))
            {
                var shelter = NearestWithRoom(person.Position);
                if (shelter == null)
                {
                    unplaced.Add(person.Id);
                    continue;
                }

                shelter.Occupants++;
                var byBus = !person.HasOwnTransport;
                onBus += byBus ? 1 : 0;
                routes.Add(new EvacuationRoute(person.Id, shelter.Id, math.distance(person.Position, shelter.Position), byBus));
            }

            return new EvacuationResult(routes, unplaced, BusDaysFor(onBus));
        }

        /// <summary>Iedereen mag terug naar huis: de schuilplaatsen lopen leeg.</summary>
        public void ClearOccupants()
        {
            foreach (var shelter in _shelters)
            {
                shelter.Occupants = 0;
            }
        }

        /// <summary>Dagen die de bussen nodig hebben voor dit aantal mensen; zonder bussen kan niemand per bus weg.</summary>
        public int BusDaysFor(int people)
        {
            if (people <= 0)
            {
                return 0;
            }

            return BusLiftPerDay == 0 ? int.MaxValue : (people + BusLiftPerDay - 1) / BusLiftPerDay;
        }

        internal void Restore(int nextId, int buses, IEnumerable<Shelter> shelters)
        {
            NextId = nextId;
            _buses = buses;
            _shelters.Clear();
            _shelters.AddRange(shelters);
        }

        private Shelter NearestWithRoom(float2 point) =>
            _shelters.Where(s => s.Free > 0).OrderBy(s => math.distance(point, s.Position)).ThenBy(s => s.Id).FirstOrDefault();
    }

    /// <summary>Sectie <c>EVAC</c>: de schuilplaatsen met hun bezetting en het aantal evacuatiebussen (E15-10).</summary>
    public sealed class EvacuationSection : ISaveSection
    {
        public const string SectionId = "EVAC";

        private const int MaxShelters = 100_000;
        private const int MaxBuses = 100_000;

        private readonly EvacuationPlan _plan;

        public EvacuationSection(EvacuationPlan plan)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_plan.NextId);
            writer.Write(_plan.Buses);
            writer.Write(_plan.Shelters.Count);
            foreach (var shelter in _plan.Shelters)
            {
                writer.Write(shelter.Id);
                writer.Write(shelter.Position.x);
                writer.Write(shelter.Position.y);
                writer.Write(shelter.Capacity);
                writer.Write(shelter.Occupants);
            }
        }

        public Action Read(SaveReader reader, int version)
        {
            var nextId = reader.ReadInt32();
            var buses = reader.ReadInt32();
            var count = reader.ReadInt32();
            if (nextId < 1 || buses < 0 || buses > MaxBuses || count < 0 || count > MaxShelters)
            {
                throw reader.Corrupt($"onmogelijke evacuatiegegevens: volgnummer {nextId}, bussen {buses}, schuilplaatsen {count}");
            }

            var shelters = new List<Shelter>(count);
            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadInt32();
                var position = new float2(reader.ReadSingle(), reader.ReadSingle());
                var capacity = reader.ReadInt32();
                var occupants = reader.ReadInt32();
                var valid = id >= 1 && id < nextId && capacity >= 1 && occupants >= 0 && occupants <= capacity;
                if (!valid)
                {
                    throw reader.Corrupt($"onmogelijke schuilplaats {id}");
                }

                shelters.Add(new Shelter(id, position, capacity, occupants));
            }

            reader.ExpectEnd();
            return () => _plan.Restore(nextId, buses, shelters);
        }
    }
}
