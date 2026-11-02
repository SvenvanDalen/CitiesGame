using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Randomness;
using Cities.Simulation.Fields;
using Unity.Mathematics;

namespace Cities.Simulation.Services
{
    public enum PatientState : byte
    {
        Healthy,
        Sick,
        Dead,
    }

    /// <summary>
    /// Gezondheidszorg (E10-04), bovenop de dekking en uitruk van <see cref="ServiceDispatcher"/>. Een burger wordt per dag ziek met een kans die met
    /// de leeftijd oploopt (<see cref="SickChance"/>). Een zieke meldt zich bij de ambulance van een kliniek of ziekenhuis; is de melding
    /// afgehandeld, dan is hij behandeld en weer gezond. Wordt hij <see cref="UntreatedDays"/> dagen niet opgehaald, dan overlijdt hij.
    /// Kliniek en ziekenhuis verschillen alleen in bereik, aantal ambulances en kosten (<see cref="Clinic"/>, <see cref="Hospital"/>).
    /// </summary>
    public sealed class HealthModel
    {
        /// <summary>Kans per dag dat een pasgeborene ziek wordt; die loopt met de leeftijd op.</summary>
        public const float BaseSickPerDay = 0.002f;

        /// <summary>Aantal dagen zonder behandeling waarna een zieke overlijdt.</summary>
        public const int UntreatedDays = 5;

        private sealed class Entry
        {
            public float2 Position { get; set; }
            public int Age { get; set; }
            public PatientState State { get; set; }
            public Incident Incident { get; set; }
            public int Days { get; set; }
        }

        private readonly ServiceDispatcher _dispatcher;
        private readonly Dictionary<int, Entry> _citizens = new Dictionary<int, Entry>();

        public HealthModel(ServiceDispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        /// <summary>Luchtvervuiling (0 tot 1) op een plek (E12-03); leeg betekent schone lucht. Vervuiling verhoogt de kans op ziekte.</summary>
        public Func<float2, float> PollutionAt { get; set; }

        public static ServiceStation Clinic(float2 position) => new ServiceStation(ServiceKind.Health, position, 400f, 1, 20000);

        public static ServiceStation Hospital(float2 position) => new ServiceStation(ServiceKind.Health, position, 900f, 4, 120000);

        /// <summary>Kans per dag dat een burger van deze leeftijd ziek wordt.</summary>
        public static float SickChance(int age) => BaseSickPerDay * (1f + (math.max(age, 0) / 30f));

        public void Add(int id, float2 position, int age)
        {
            if (_citizens.ContainsKey(id))
            {
                throw new ArgumentException($"Burger {id} bestaat al.", nameof(id));
            }

            _citizens[id] = new Entry { Position = position, Age = age };
        }

        public PatientState StateOf(int id) => Get(id).State;

        /// <summary>Maakt een burger ziek en meldt hem bij de ambulance; false als hij al ziek of overleden is.</summary>
        public bool Infect(int id)
        {
            var entry = Get(id);
            if (entry.State != PatientState.Healthy)
            {
                return false;
            }

            entry.State = PatientState.Sick;
            entry.Days = 0;
            entry.Incident = _dispatcher.Report(ServiceKind.Health, entry.Position);
            return true;
        }

        /// <summary>
        /// Eén dag: behandelde zieken zijn gezond, zieken die te lang wachten overlijden, en gezonden worden bij toeval ziek. Geeft de burgers
        /// die vandaag zijn overleden. De tijd van de ambulance loopt via <see cref="ServiceDispatcher.Tick"/>, die de aanroeper bijhoudt.
        /// </summary>
        public IReadOnlyList<int> Step(ref DeterministicRandom random)
        {
            foreach (var cured in Sick().Where(p => !_dispatcher.Open.Contains(p.Value.Incident)).ToArray())
            {
                cured.Value.State = PatientState.Healthy;
            }

            var died = new List<int>();
            foreach (var waiting in Sick().ToArray())
            {
                waiting.Value.Days++;
                if (waiting.Value.Days >= UntreatedDays)
                {
                    waiting.Value.State = PatientState.Dead;
                    _dispatcher.Cancel(waiting.Value.Incident);
                    died.Add(waiting.Key);
                }
            }

            var rng = random; // een lambda kan de ref-parameter niet vangen
            var fallen = _citizens.Where(p => p.Value.State == PatientState.Healthy)
                .Where(p => rng.NextFloat() < SickChance(p.Value.Age) * AirPollution.HealthFactor(PollutionAt?.Invoke(p.Value.Position) ?? 0f))
                .Select(p => p.Key).ToArray();
            random = rng;
            foreach (var id in fallen)
            {
                Infect(id);
            }

            return died;
        }

        private IEnumerable<KeyValuePair<int, Entry>> Sick() => _citizens.Where(p => p.Value.State == PatientState.Sick);

        private Entry Get(int id) =>
            _citizens.TryGetValue(id, out var entry) ? entry : throw new ArgumentException($"Onbekende burger {id}.", nameof(id));
    }
}
