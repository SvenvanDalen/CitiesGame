using System;
using System.Collections.Generic;
using System.Linq;

namespace Cities.Simulation.Policies
{
    /// <summary>
    /// De ingevoerde beleidsmaatregelen van de stad (E13-03). Een maatregel geldt stadsbreed (district <see cref="CityWide"/>) of in
    /// één district, afhankelijk van zijn <see cref="PolicyScope"/>. Alle regels komen uit de <see cref="PolicySpec"/>-data: kosten,
    /// maandkosten en effecten. Het geld zelf boekt de aanroeper; <see cref="Activate"/> geeft de eenmalige kosten terug.
    /// </summary>
    public sealed class PolicyBook
    {
        /// <summary>District-nummer voor stadsbreed beleid; echte districten beginnen bij 1.</summary>
        public const int CityWide = 0;

        private readonly IReadOnlyDictionary<string, PolicySpec> _specs;
        private readonly HashSet<(string Id, int District)> _active = new HashSet<(string, int)>();

        public PolicyBook(IReadOnlyDictionary<string, PolicySpec> specs)
        {
            _specs = specs ?? throw new ArgumentNullException(nameof(specs));
        }

        public int Count => _active.Count;

        /// <summary>De ingevoerde maatregelen, gesorteerd zodat het opslaan altijd dezelfde bytes geeft.</summary>
        public IEnumerable<(string Id, int District)> Active => _active.OrderBy(a => a.Id, StringComparer.Ordinal).ThenBy(a => a.District);

        public bool IsKnown(string id) => id != null && _specs.ContainsKey(id);

        public bool IsActive(string id, int district = CityWide) => _active.Contains((id, district));

        /// <summary>Voert de maatregel in en geeft de eenmalige kosten terug.</summary>
        public long Activate(string id, int district = CityWide)
        {
            var spec = SpecFor(id, district);
            if (!_active.Add((id, district)))
            {
                throw new InvalidOperationException($"Beleid '{id}' geldt al in {Describe(district)}.");
            }

            return spec.ActivationCost;
        }

        public bool Deactivate(string id, int district = CityWide) => _active.Remove((id, district));

        /// <summary>
        /// De samengestelde factor op <paramref name="key"/> voor een district: alle stadsbrede maatregelen en die van dat district
        /// vermenigvuldigd. Zonder effect is het 1.
        /// </summary>
        public float Multiplier(string key, int district = CityWide)
        {
            var factor = 1f;
            foreach (var id in _active.Where(a => a.District == CityWide || a.District == district).Select(a => a.Id))
            {
                factor *= EffectsOf(id).Where(e => e.Key == key).Aggregate(1f, (f, e) => f * e.Multiplier);
            }

            return factor;
        }

        /// <summary>
        /// Maandkosten van al het beleid. <paramref name="population"/> geeft het aantal inwoners van een district (bij
        /// <see cref="CityWide"/> van de hele stad).
        /// </summary>
        public long MonthlyCost(Func<int, int> population)
        {
            if (population is null)
            {
                throw new ArgumentNullException(nameof(population));
            }

            return _active.Sum(a => _specs[a.Id].MonthlyCostFor(population(a.District)));
        }

        /// <summary>Controleert een lijst zonder iets te veranderen; gooit een <see cref="ArgumentException"/> bij onbekend of verkeerd beleid.</summary>
        public void Check(IEnumerable<(string Id, int District)> policies)
        {
            foreach (var (id, district) in policies)
            {
                SpecFor(id, district);
            }
        }

        /// <summary>Vervangt het beleid door een eerder opgeslagen lijst; klopt er iets niet, dan verandert er niets.</summary>
        public void Restore(IEnumerable<(string Id, int District)> policies)
        {
            var list = policies.ToList();
            Check(list);
            _active.Clear();
            _active.UnionWith(list);
        }

        private PolicySpec SpecFor(string id, int district)
        {
            if (!IsKnown(id))
            {
                throw new ArgumentException($"Onbekend beleid '{id}'.", nameof(id));
            }

            var spec = _specs[id];
            var cityWide = district == CityWide;
            if (district < 0 || cityWide != (spec.Scope == PolicyScope.City))
            {
                throw new ArgumentOutOfRangeException(nameof(district), district, $"Beleid '{id}' geldt {(spec.Scope == PolicyScope.City ? "stadsbreed" : "per district")}.");
            }

            return spec;
        }

        private PolicyEffect[] EffectsOf(string id) => _specs[id].Effects ?? Array.Empty<PolicyEffect>();

        private static string Describe(int district) => district == CityWide ? "de stad" : $"district {district}";
    }
}
