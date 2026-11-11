using System;
using System.Collections.Generic;
using Cities.Core.Randomness;
using Cities.Simulation.Services;
using Unity.Mathematics;

namespace Cities.Simulation.Vehicles
{
    /// <summary>Een ongeluk (E08-15): het wegvak staat stil tot het is opgeruimd.</summary>
    public sealed class Accident
    {
        internal Accident(int id, ulong segment, float2 position, float remaining)
        {
            Id = id;
            Segment = segment;
            Position = position;
            Remaining = remaining;
        }

        public int Id { get; }

        public ulong Segment { get; }

        public float2 Position { get; }

        public float Remaining { get; internal set; }

        /// <summary>De melding bij de hulpdiensten, of null als die uit staan.</summary>
        public Incident Responder { get; internal set; }
    }

    /// <summary>
    /// Ongelukken (E08-15), standaard uit. Op drukke wegvakken is er een kleine kans op een aanrijding: het vak staat dan stil tot het is
    /// opgeruimd. Met hulpdiensten gaat dat dubbel zo snel zodra een voertuig ter plaatse is. De kans volgt uit de seed, het wegvak
    /// en de tijdstap, en blijft laag zodat de moeilijkheid niet stijgt.
    /// </summary>
    public sealed class AccidentModel
    {
        /// <summary>Kans per controle bij een volle weg (bezetting 1).</summary>
        public const float ChanceAtFullOccupancy = 0.002f;

        /// <summary>Onder deze bezetting gebeurt er niets.</summary>
        public const float MinOccupancy = 0.3f;

        public const float ClearSeconds = 90f;

        public const int MaxActive = 8;

        private readonly List<Accident> _active = new List<Accident>();
        private readonly ServiceDispatcher _dispatcher;
        private int _nextId = 1;

        public AccidentModel(ulong seed, ServiceDispatcher dispatcher = null)
        {
            Seed = seed;
            _dispatcher = dispatcher;
        }

        public bool Enabled { get; set; }

        public ulong Seed { get; internal set; }

        public int NextId => _nextId;

        public IReadOnlyList<Accident> Active => _active;

        /// <summary>Is dit wegvak nu geblokkeerd?</summary>
        public bool IsBlocked(ulong segment)
        {
            foreach (var accident in _active)
            {
                if (accident.Segment == segment)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Snelheidsfactor voor het vak: 0 bij een ongeluk, anders 1.</summary>
        public float SpeedFactor(ulong segment) => IsBlocked(segment) ? 0f : 1f;

        /// <summary>Eén controle voor een wegvak; geeft het nieuwe ongeluk of null.</summary>
        public Accident Check(ulong segment, float2 position, float occupancy, ulong step)
        {
            if (!Enabled || _active.Count >= MaxActive || occupancy < MinOccupancy || IsBlocked(segment))
            {
                return null;
            }

            var chance = ChanceAtFullOccupancy * math.saturate(occupancy);
            var roll = (SeedHash.Combine(Seed, segment, step) >> 40) / (float)(1 << 24);
            if (roll >= chance)
            {
                return null;
            }

            var accident = new Accident(_nextId++, segment, position, ClearSeconds);
            if (_dispatcher != null)
            {
                accident.Responder = _dispatcher.Report(ServiceKind.Police, position);
            }

            _active.Add(accident);
            return accident;
        }

        /// <summary>Laat tijd verstrijken; geeft de ongelukken die nu zijn opgeruimd.</summary>
        public IReadOnlyList<Accident> Tick(float seconds)
        {
            var cleared = new List<Accident>();
            foreach (var accident in _active)
            {
                var helped = accident.Responder != null && accident.Responder.Handler != null;
                accident.Remaining -= helped ? seconds * 2f : seconds;
                if (accident.Remaining <= 0f)
                {
                    cleared.Add(accident);
                }
            }

            foreach (var accident in cleared)
            {
                _active.Remove(accident);
            }

            return cleared;
        }

        internal void Restore(int nextId, List<Accident> accidents)
        {
            _nextId = nextId;
            _active.Clear();
            _active.AddRange(accidents);
        }
    }
}
