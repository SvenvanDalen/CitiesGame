using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Simulation.Economy;
using Cities.Simulation.Policies;

namespace Cities.Presentation.UI
{
    public enum PolicyToggleResult
    {
        Unknown,
        Activated,
        Deactivated,
        Locked,
        CannotAfford,
    }

    /// <summary>Eén regel in de beleidslijst: wat het kost, of het geldt en waarom het niet kan.</summary>
    public readonly struct PolicyEntry
    {
        public PolicyEntry(string id, string name, PolicySpec spec, int citizens, bool active, bool canAfford)
        {
            Id = id;
            Name = name;
            ActivationCost = spec.ActivationCost;
            MonthlyCost = spec.MonthlyCostFor(citizens);
            Active = active;
            UnlockPopulation = spec.UnlockPopulation;
            Locked = citizens < spec.UnlockPopulation;
            CanAfford = canAfford;
        }

        public string Id { get; }

        public string Name { get; }

        public long ActivationCost { get; }

        /// <summary>Maandkosten voor het gebied waarin het beleid geldt (bij inwonerafhankelijke kosten het huidige aantal inwoners).</summary>
        public long MonthlyCost { get; }

        public bool Active { get; }

        public int UnlockPopulation { get; }

        /// <summary>Te weinig inwoners om dit beleid te ontgrendelen.</summary>
        public bool Locked { get; }

        public bool CanAfford { get; }

        /// <summary>Of een klik het beleid kan invoeren; intrekken kan altijd.</summary>
        public bool CanToggle => Active || (!Locked && CanAfford);
    }

    /// <summary>
    /// Het beleidspaneel (E18-06): de lijst met stads- of districtsbeleid boven <see cref="PolicyBook"/> en <see cref="Ledger"/>. Invoeren boekt de
    /// eenmalige kosten, intrekken is gratis; beleid dat nog niet ontgrendeld is of niet betaalbaar, wordt geweigerd met de reden.
    /// </summary>
    public sealed class PolicyPanelModel
    {
        private readonly PolicyBook _book;
        private readonly IReadOnlyDictionary<string, PolicySpec> _specs;
        private readonly IReadOnlyDictionary<string, string> _names;
        private readonly Ledger _ledger;
        private readonly Func<int> _week;
        private readonly Func<int, int> _population;

        /// <param name="names">Weergavenaam per beleids-id; ontbreekt die, dan wordt het id getoond.</param>
        /// <param name="population">Inwoners per district; bij <see cref="PolicyBook.CityWide"/> van de hele stad.</param>
        public PolicyPanelModel(
            PolicyBook book,
            IReadOnlyDictionary<string, PolicySpec> specs,
            IReadOnlyDictionary<string, string> names,
            Ledger ledger,
            Func<int> week,
            Func<int, int> population)
        {
            _book = book ?? throw new ArgumentNullException(nameof(book));
            _specs = specs ?? throw new ArgumentNullException(nameof(specs));
            _names = names ?? throw new ArgumentNullException(nameof(names));
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _week = week ?? throw new ArgumentNullException(nameof(week));
            _population = population ?? throw new ArgumentNullException(nameof(population));
        }

        public event Action Changed;

        /// <summary>Weergavenaam per effectsleutel voor de tooltip (E13-07); ontbreekt die, dan wordt de sleutel getoond.</summary>
        public IReadOnlyDictionary<string, string> EffectNames { get; set; }

        /// <summary>Euro per dag die een systeem nu verwerkt voor een effectsleutel, of <c>null</c>; voor de dagbijdrage in de tooltip.</summary>
        public Func<string, long?> DailyBaseline { get; set; }

        /// <summary>Maandkosten van al het ingevoerde beleid.</summary>
        public long MonthlyCost => _book.MonthlyCost(_population);

        public int ActiveCount => _book.Count;

        /// <summary>De lijst voor stadsbeleid (<paramref name="district"/> is <see cref="PolicyBook.CityWide"/>) of voor één district, op naam gesorteerd.</summary>
        public IReadOnlyList<PolicyEntry> Entries(int district = PolicyBook.CityWide)
        {
            var scope = district == PolicyBook.CityWide ? PolicyScope.City : PolicyScope.District;
            var citizens = _population(district);
            return _specs
                .Where(pair => pair.Value.Scope == scope)
                .Select(pair => Entry(pair.Key, pair.Value, district, citizens))
                .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(entry => entry.Id, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>Voert het beleid in of trekt het in; het resultaat zegt wat er gebeurde of waarom niet.</summary>
        public PolicyToggleResult Toggle(string id, int district = PolicyBook.CityWide)
        {
            if (!_book.IsKnown(id) || !IsValidDistrict(id, district))
            {
                return PolicyToggleResult.Unknown;
            }

            if (_book.IsActive(id, district))
            {
                _book.Deactivate(id, district);
                Changed?.Invoke();
                return PolicyToggleResult.Deactivated;
            }

            var spec = _specs[id];
            if (_population(district) < spec.UnlockPopulation)
            {
                return PolicyToggleResult.Locked;
            }

            if (!_ledger.TrySpend(_week(), LedgerCategory.Other, spec.ActivationCost))
            {
                return PolicyToggleResult.CannotAfford;
            }

            _book.Activate(id, district);
            Changed?.Invoke();
            return PolicyToggleResult.Activated;
        }

        /// <summary>De tooltip met kosten, effecten en dagelijkse bijdrage (E13-07); leeg bij onbekend beleid.</summary>
        public string TooltipFor(string id, int district = PolicyBook.CityWide) =>
            _book.IsKnown(id) ? PolicyTooltip.Build(NameOf(id), _specs[id], _population(district), EffectNames, DailyBaseline) : string.Empty;

        public string NameOf(string id) => id != null && _names.TryGetValue(id, out var name) ? name : id;

        private bool IsValidDistrict(string id, int district) =>
            district >= 0 && (district == PolicyBook.CityWide) == (_specs[id].Scope == PolicyScope.City);

        private PolicyEntry Entry(string id, PolicySpec spec, int district, int citizens) =>
            new PolicyEntry(
                id,
                NameOf(id),
                spec,
                citizens,
                _book.IsActive(id, district),
                _ledger.CanAfford(spec.ActivationCost));
    }
}
