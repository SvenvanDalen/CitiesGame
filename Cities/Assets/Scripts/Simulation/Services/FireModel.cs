using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Randomness;
using Unity.Mathematics;

namespace Cities.Simulation.Services
{
    public enum FireState : byte
    {
        Safe,
        Burning,
        BurnedDown,
    }

    /// <summary>
    /// Brand in gebouwen (E10-02), bovenop de dekking en uitruk van <see cref="ServiceDispatcher"/>. Elk gebouw heeft een brandbaarheid (0 tot 1)
    /// en daarmee een brandgevaar per dag (<see cref="Risk"/>): het is half zo groot als een kazerne het gebouw dekt. Een brand begint door
    /// brandstichting (<see cref="Ignite"/> of toeval in <see cref="Step"/>), meldt zich bij de brandweer en springt over op brandbare buren
    /// binnen <see cref="SpreadRadius"/>. De brandweer blust hem; wordt hij niet op tijd geblust, dan brandt het gebouw af.
    /// </summary>
    public sealed class FireModel
    {
        /// <summary>Kans per dag dat een volledig brandbaar, niet gedekt gebouw spontaan vlam vat.</summary>
        public const float BaseRiskPerDay = 0.0005f;

        /// <summary>Factor op het brandgevaar als een brandweerkazerne het gebouw dekt.</summary>
        public const float CoveredRiskFactor = 0.5f;

        /// <summary>Afstand in meter waarover een brand op een buur kan overslaan.</summary>
        public const float SpreadRadius = 24f;

        /// <summary>Kans per dag, per brandende buur, dat een volledig brandbaar gebouw meebrandt.</summary>
        public const float SpreadChance = 0.3f;

        /// <summary>Aantal dagen brand waarna een gebouw is afgebrand.</summary>
        public const int BurnDays = 3;

        private sealed class Entry
        {
            public float2 Position { get; set; }
            public float Flammability { get; set; }
            public FireState State { get; set; }
            public Incident Incident { get; set; }
            public int Days { get; set; }
        }

        private readonly ServiceDispatcher _dispatcher;
        private readonly Dictionary<int, Entry> _buildings = new Dictionary<int, Entry>();

        public FireModel(ServiceDispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public void Add(int id, float2 position, float flammability)
        {
            if (flammability < 0f || flammability > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(flammability), flammability, "De brandbaarheid ligt tussen 0 en 1.");
            }

            if (_buildings.ContainsKey(id))
            {
                throw new ArgumentException($"Gebouw {id} bestaat al.", nameof(id));
            }

            _buildings[id] = new Entry { Position = position, Flammability = flammability };
        }

        public FireState StateOf(int id) => Get(id).State;

        /// <summary>Brandgevaar per dag; nul voor een gebouw dat al brandt of is afgebrand.</summary>
        public float Risk(int id)
        {
            var entry = Get(id);
            if (entry.State != FireState.Safe)
            {
                return 0f;
            }

            var covered = _dispatcher.IsCovered(ServiceKind.Fire, entry.Position);
            return BaseRiskPerDay * entry.Flammability * (covered ? CoveredRiskFactor : 1f);
        }

        /// <summary>Steekt het gebouw in brand en meldt dat bij de brandweer; false als het al brandt of is afgebrand.</summary>
        public bool Ignite(int id)
        {
            var entry = Get(id);
            if (entry.State != FireState.Safe)
            {
                return false;
            }

            entry.State = FireState.Burning;
            entry.Days = 0;
            entry.Incident = _dispatcher.Report(ServiceKind.Fire, entry.Position);
            return true;
        }

        /// <summary>
        /// Eén dag: geblusste branden zijn uit, branden die te lang duren branden het gebouw af, en veilige gebouwen vatten vlam door
        /// brandgevaar of een brandende buur. Geeft de gebouwen die vandaag zijn afgebrand. De tijd van de brandweer loopt via
        /// <see cref="ServiceDispatcher.Tick"/>, die de aanroeper zelf bijhoudt.
        /// </summary>
        public IReadOnlyList<int> Step(ref DeterministicRandom random)
        {
            foreach (var put in Burning().Where(p => !_dispatcher.Open.Contains(p.Value.Incident)).ToArray())
            {
                put.Value.State = FireState.Safe;
            }

            var burning = Burning().ToArray();
            var burnedDown = new List<int>();
            foreach (var pair in burning)
            {
                pair.Value.Days++;
                if (pair.Value.Days >= BurnDays)
                {
                    pair.Value.State = FireState.BurnedDown;
                    _dispatcher.Cancel(pair.Value.Incident);
                    burnedDown.Add(pair.Key);
                }
            }

            var rng = random; // een lambda kan de ref-parameter niet vangen
            var sparks = _buildings.Where(p => p.Value.State == FireState.Safe).Select(p => p.Key).ToArray()
                .Where(id => rng.NextFloat() < Risk(id) + SpreadTo(_buildings[id], burning)).ToArray();
            random = rng;
            foreach (var id in sparks)
            {
                Ignite(id);
            }

            return burnedDown;
        }

        private IEnumerable<KeyValuePair<int, Entry>> Burning() => _buildings.Where(p => p.Value.State == FireState.Burning);

        private static float SpreadTo(Entry target, KeyValuePair<int, Entry>[] burning) =>
            burning.Count(p => math.lengthsq(p.Value.Position - target.Position) <= SpreadRadius * SpreadRadius) * SpreadChance * target.Flammability;

        private Entry Get(int id) =>
            _buildings.TryGetValue(id, out var entry) ? entry : throw new ArgumentException($"Onbekend gebouw {id}.", nameof(id));
    }
}
