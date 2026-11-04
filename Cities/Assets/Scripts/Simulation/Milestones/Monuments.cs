using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;

namespace Cities.Simulation.Milestones
{
    /// <summary>Wat de stad nu kan laten zien aan een monument dat ontgrendeld wil worden (E16-04).</summary>
    public readonly struct CityConditions
    {
        public CityConditions(int population, float services, float happiness)
        {
            Population = population;
            Services = services;
            Happiness = happiness;
        }

        public int Population { get; }

        /// <summary>Dekking van diensten, 0 (geen) tot 1 (volledig).</summary>
        public float Services { get; }

        /// <summary>Gemiddeld geluk van de inwoners, 0 tot 100.</summary>
        public float Happiness { get; }
    }

    /// <summary>Het voordeel dat een monument de stad geeft: een factor op een sleutel, net als bij beleid (bijvoorbeeld <c>tourism.visitors</c>).</summary>
    public readonly struct MonumentBonus
    {
        public MonumentBonus(string key, float multiplier)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Een bonus heeft een sleutel nodig.", nameof(key));
            }

            if (!(multiplier >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(multiplier), multiplier, "De factor kan niet negatief zijn.");
            }

            Key = key;
            Multiplier = multiplier;
        }

        public string Key { get; }

        public float Multiplier { get; }
    }

    /// <summary>Een uniek gebouw of monument (E16-04): voorwaarden aan de stad om het te ontgrendelen en de bonus die het geeft zodra het staat.</summary>
    public sealed class MonumentSpec
    {
        public MonumentSpec(string id, string name, int population, float services, float happiness, long cost, IReadOnlyList<MonumentBonus> bonuses)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("ID ontbreekt", nameof(id));
            }

            if (population < 0 || cost < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(population), "Bevolking en kosten kunnen niet negatief zijn.");
            }

            if (!(services >= 0f && services <= 1f) || !(happiness >= 0f && happiness <= 100f))
            {
                throw new ArgumentOutOfRangeException(nameof(services), "Diensten liggen tussen 0 en 1, geluk tussen 0 en 100.");
            }

            Id = id;
            Name = name ?? id;
            Population = population;
            Services = services;
            Happiness = happiness;
            Cost = cost;
            Bonuses = bonuses ?? Array.Empty<MonumentBonus>();
        }

        /// <summary>Stabiel ID (<c>monument.naam</c>), komt in de save.</summary>
        public string Id { get; }

        public string Name { get; }

        public int Population { get; }

        public float Services { get; }

        public float Happiness { get; }

        public long Cost { get; }

        public IReadOnlyList<MonumentBonus> Bonuses { get; }

        public bool IsMetBy(CityConditions city) =>
            city.Population >= Population && city.Services >= Services && city.Happiness >= Happiness;

        /// <summary>De voorwaarden waaraan de stad nog niet voldoet, in leesbare tekst; leeg als alles klopt.</summary>
        public IReadOnlyList<string> Missing(CityConditions city)
        {
            var missing = new List<string>();
            if (city.Population < Population)
            {
                missing.Add($"nog {Population - city.Population:N0} inwoners nodig");
            }

            if (city.Services < Services)
            {
                missing.Add($"diensten op {Services * 100f:0}% (nu {city.Services * 100f:0}%)");
            }

            if (city.Happiness < Happiness)
            {
                missing.Add($"geluk op {Happiness:0} (nu {city.Happiness:0})");
            }

            return missing;
        }
    }

    /// <summary>De standaardmonumenten (E16-04), oplopend in eisen.</summary>
    public static class MonumentCatalog
    {
        public static readonly IReadOnlyList<MonumentSpec> All = new[]
        {
            new MonumentSpec("monument.cathedral", "Kathedraal", 10_000, 0.5f, 55f, 2_000_000, new[] { new MonumentBonus("tourism.visitors", 1.1f) }),
            new MonumentSpec("monument.stadium", "Stadion", 25_000, 0.6f, 60f, 5_000_000, new[] { new MonumentBonus("events.attendance", 1.25f), new MonumentBonus("happiness.city", 1.02f) }),
            new MonumentSpec("monument.opera", "Operagebouw", 50_000, 0.7f, 65f, 8_000_000, new[] { new MonumentBonus("tourism.visitors", 1.15f), new MonumentBonus("tax.income", 1.02f) }),
            new MonumentSpec("monument.exhibition", "Wereldtentoonstelling", 100_000, 0.75f, 65f, 15_000_000, new[] { new MonumentBonus("tourism.visitors", 1.2f), new MonumentBonus("trade.income", 1.1f) }),
            new MonumentSpec("monument.landmark-tower", "Icoon-toren", 250_000, 0.8f, 70f, 30_000_000, new[] { new MonumentBonus("tourism.visitors", 1.3f), new MonumentBonus("happiness.city", 1.03f) }),
        };

        public static MonumentSpec Find(string id) => All.FirstOrDefault(m => m.Id == id);
    }

    /// <summary>Waarom een monument niet gebouwd kan worden.</summary>
    public enum MonumentBuildResult
    {
        Built,
        Unknown,
        Locked,
        AlreadyBuilt,
    }

    /// <summary>
    /// Houdt bij welke monumenten beschikbaar zijn gekomen en welke er staan (E16-04). Een monument komt beschikbaar zodra de stad aan zijn
    /// voorwaarden (bevolking, diensten, geluk) voldoet en blijft dat, ook als de stad daarna terugvalt. Elk monument staat hooguit één keer;
    /// de bonussen van de gebouwde monumenten vermenigvuldigen zich in <see cref="Multiplier"/>.
    /// </summary>
    public sealed class MonumentTracker
    {
        private readonly IReadOnlyList<MonumentSpec> _catalog;
        private readonly HashSet<string> _available = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _built = new HashSet<string>(StringComparer.Ordinal);

        public MonumentTracker()
            : this(MonumentCatalog.All)
        {
        }

        public MonumentTracker(IReadOnlyList<MonumentSpec> catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public IReadOnlyList<MonumentSpec> Catalog => _catalog;

        public IEnumerable<string> Available => _available.OrderBy(a => a, StringComparer.Ordinal);

        public IEnumerable<string> Built => _built.OrderBy(a => a, StringComparer.Ordinal);

        public bool IsAvailable(string id) => _available.Contains(id);

        public bool IsBuilt(string id) => _built.Contains(id);

        /// <summary>Controleert de stad tegen alle monumenten en geeft de nieuw beschikbare terug.</summary>
        public IReadOnlyList<MonumentSpec> Update(CityConditions city)
        {
            var fresh = new List<MonumentSpec>();
            foreach (var spec in _catalog)
            {
                if (!_available.Contains(spec.Id) && spec.IsMetBy(city))
                {
                    _available.Add(spec.Id);
                    fresh.Add(spec);
                }
            }

            return fresh;
        }

        /// <summary>Zet een monument neer; de aanroeper boekt de kosten (<see cref="MonumentSpec.Cost"/>) bij <see cref="MonumentBuildResult.Built"/>.</summary>
        public MonumentBuildResult Build(string id)
        {
            if (Find(id) == null)
            {
                return MonumentBuildResult.Unknown;
            }

            if (_built.Contains(id))
            {
                return MonumentBuildResult.AlreadyBuilt;
            }

            if (!_available.Contains(id))
            {
                return MonumentBuildResult.Locked;
            }

            _built.Add(id);
            return MonumentBuildResult.Built;
        }

        /// <summary>Het monument is gesloopt of verloren; het blijft beschikbaar en kan opnieuw gebouwd worden.</summary>
        public bool Remove(string id) => _built.Remove(id);

        /// <summary>De samengestelde bonusfactor op een sleutel van alle gebouwde monumenten; 1 zonder effect.</summary>
        public float Multiplier(string key) =>
            _catalog.Where(m => _built.Contains(m.Id)).SelectMany(m => m.Bonuses).Where(b => b.Key == key).Aggregate(1f, (f, b) => f * b.Multiplier);

        internal void Restore(IEnumerable<string> available, IEnumerable<string> built)
        {
            _available.Clear();
            _available.UnionWith(available);
            _built.Clear();
            _built.UnionWith(built);
        }

        private MonumentSpec Find(string id) => id == null ? null : _catalog.FirstOrDefault(m => m.Id == id);
    }

    /// <summary>Sectie <c>MONU</c>: welke monumenten beschikbaar zijn en welke er staan (E16-04).</summary>
    public sealed class MonumentSection : ISaveSection
    {
        public const string SectionId = "MONU";

        private readonly MonumentTracker _tracker;

        public MonumentSection(MonumentTracker tracker)
        {
            _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            WriteIds(writer, _tracker.Available.ToList());
            WriteIds(writer, _tracker.Built.ToList());
        }

        public Action Read(SaveReader reader, int version)
        {
            var available = ReadIds(reader);
            var built = ReadIds(reader);
            reader.ExpectEnd();
            if (built.Any(id => !available.Contains(id)))
            {
                throw reader.Corrupt("een gebouwd monument was nooit beschikbaar");
            }

            return () => _tracker.Restore(available, built);
        }

        private static void WriteIds(SaveWriter writer, IReadOnlyList<string> ids)
        {
            writer.Write(ids.Count);
            foreach (var id in ids)
            {
                writer.Write(id);
            }
        }

        private List<string> ReadIds(SaveReader reader)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > _tracker.Catalog.Count)
            {
                throw reader.Corrupt($"onmogelijk aantal monumenten {count}");
            }

            var ids = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadString();
                if (_tracker.Catalog.All(m => m.Id != id) || ids.Contains(id))
                {
                    throw reader.Corrupt($"onbekend of dubbel monument '{id}'");
                }

                ids.Add(id);
            }

            return ids;
        }
    }
}
