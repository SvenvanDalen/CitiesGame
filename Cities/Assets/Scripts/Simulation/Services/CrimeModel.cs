using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Randomness;
using Unity.Mathematics;

namespace Cities.Simulation.Services
{
    /// <summary>
    /// De gevangenis (E10-03): een vaste capaciteit aan cellen. Een volle gevangenis neemt niemand meer op, dus daders lopen vrij rond.
    /// </summary>
    public sealed class Prison
    {
        public Prison(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Een gevangenis heeft minstens één cel nodig.");
            }

            Capacity = capacity;
        }

        public int Capacity { get; }

        public int Inmates { get; private set; }

        public bool IsFull => Inmates >= Capacity;

        public bool Admit()
        {
            if (IsFull)
            {
                return false;
            }

            Inmates++;
            return true;
        }

        public void Release(int count) => Inmates = math.max(0, Inmates - math.max(0, count));
    }

    /// <summary>
    /// Criminaliteit per wijk (E10-03), bovenop de dekking en uitruk van <see cref="ServiceDispatcher"/> (politiebureau, patrouilles).
    /// Elke wijk heeft een criminaliteitswaarde (0 tot 1) die per dag naar een doel groeit: de druk van de wijk (bijvoorbeeld armoede),
    /// gehalveerd onder een politiebureau en verhoogd als de gevangenis vol zit. Hoe hoger de waarde, hoe vaker een misdrijf wordt gemeld.
    /// Een gemeld misdrijf dat een patrouille afhandelt, leidt tot een arrestatie; blijft het te lang liggen, dan stijgt de criminaliteit.
    /// </summary>
    public sealed class CrimeModel
    {
        /// <summary>Kans per dag op een misdrijf in een wijk met criminaliteitswaarde 1.</summary>
        public const float MaxCrimeChancePerDay = 0.5f;

        /// <summary>Factor op het doel als een politiebureau de wijk dekt.</summary>
        public const float CoveredFactor = 0.5f;

        /// <summary>Factor op het doel als de gevangenis vol is.</summary>
        public const float FullPrisonFactor = 1.5f;

        /// <summary>Deel van het verschil met het doel dat de waarde per dag opschuift.</summary>
        public const float AdjustPerDay = 0.1f;

        /// <summary>Dagen waarna een niet-afgehandeld misdrijf als onopgelost geldt.</summary>
        public const int UnsolvedAfterDays = 3;

        /// <summary>Toename van de criminaliteitswaarde door één onopgelost misdrijf.</summary>
        public const float UnsolvedPenalty = 0.05f;

        private sealed class District
        {
            public float2 Position { get; set; }
            public float Pressure { get; set; }
            public float Crime { get; set; }
        }

        private sealed class Case
        {
            public int District { get; set; }
            public Incident Incident { get; set; }
            public int Days { get; set; }
        }

        private readonly ServiceDispatcher _dispatcher;
        private readonly Prison _prison;
        private readonly Dictionary<int, District> _districts = new Dictionary<int, District>();
        private readonly List<Case> _cases = new List<Case>();

        public CrimeModel(ServiceDispatcher dispatcher, Prison prison)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _prison = prison ?? throw new ArgumentNullException(nameof(prison));
        }

        /// <summary>Aantal arrestaties tot nu toe.</summary>
        public int Arrests { get; private set; }

        /// <summary>Aantal misdrijven dat is blijven liggen.</summary>
        public int Unsolved { get; private set; }

        /// <summary>Aantal daders dat vrijuit ging omdat de gevangenis vol zat.</summary>
        public int Escaped { get; private set; }

        public void Add(int id, float2 position, float pressure)
        {
            if (pressure < 0f || pressure > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(pressure), pressure, "De druk ligt tussen 0 en 1.");
            }

            if (_districts.ContainsKey(id))
            {
                throw new ArgumentException($"Wijk {id} bestaat al.", nameof(id));
            }

            _districts[id] = new District { Position = position, Pressure = pressure, Crime = pressure };
        }

        public float CrimeOf(int id) => Get(id).Crime;

        /// <summary>De waarde waar de criminaliteit van een wijk naartoe groeit.</summary>
        public float Target(int id)
        {
            var district = Get(id);
            var covered = _dispatcher.IsCovered(ServiceKind.Police, district.Position);
            return math.saturate(district.Pressure * (covered ? CoveredFactor : 1f) * (_prison.IsFull ? FullPrisonFactor : 1f));
        }

        /// <summary>
        /// Eén dag: de waarde schuift naar het doel, misdrijven worden gemeld, afgehandelde misdrijven worden arrestaties en
        /// te lang openstaande misdrijven tellen als onopgelost. De tijd van de patrouilles loopt via <see cref="ServiceDispatcher.Tick"/>.
        /// </summary>
        public void Step(ref DeterministicRandom random)
        {
            Settle();
            foreach (var pair in _districts)
            {
                pair.Value.Crime = math.saturate(math.lerp(pair.Value.Crime, Target(pair.Key), AdjustPerDay));
            }

            var rng = random; // een lambda kan de ref-parameter niet vangen
            var offended = _districts.Where(p => rng.NextFloat() < p.Value.Crime * MaxCrimeChancePerDay).Select(p => p.Key).ToArray();
            random = rng;
            foreach (var id in offended)
            {
                _cases.Add(new Case { District = id, Incident = _dispatcher.Report(ServiceKind.Police, _districts[id].Position) });
            }
        }

        private void Settle()
        {
            foreach (var done in _cases.Where(c => !_dispatcher.Open.Contains(c.Incident)).ToArray())
            {
                _cases.Remove(done);
                if (_prison.Admit())
                {
                    Arrests++;
                }
                else
                {
                    Escaped++;
                }
            }

            foreach (var open in _cases)
            {
                open.Days++;
            }

            foreach (var old in _cases.Where(c => c.Days >= UnsolvedAfterDays).ToArray())
            {
                _cases.Remove(old);
                _dispatcher.Cancel(old.Incident);
                Unsolved++;
                var district = _districts[old.District];
                district.Crime = math.saturate(district.Crime + UnsolvedPenalty);
            }
        }

        private District Get(int id) =>
            _districts.TryGetValue(id, out var district) ? district : throw new ArgumentException($"Onbekende wijk {id}.", nameof(id));
    }
}
