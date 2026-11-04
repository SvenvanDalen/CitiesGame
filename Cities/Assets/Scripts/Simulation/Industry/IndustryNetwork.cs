using System;
using System.Collections.Generic;
using System.Linq;

namespace Cities.Simulation.Industry
{
    /// <summary>Wat alle winningen samen in een week deden, per grondstof.</summary>
    public sealed class IndustryWeek
    {
        public IndustryWeek(IReadOnlyDictionary<string, float> extracted, float pollution, float workers)
        {
            Extracted = extracted;
            Pollution = pollution;
            Workers = workers;
        }

        /// <summary>Opbrengst per grondstof, voor <c>ChainInput.Extracted</c>.</summary>
        public IReadOnlyDictionary<string, float> Extracted { get; }

        public float Pollution { get; }

        public float Workers { get; }

        public float Of(string good) => Extracted.TryGetValue(good ?? string.Empty, out var v) ? v : 0f;
    }

    /// <summary>Alle winningen van de stad (E14-02), gesorteerd op id zodat de volgorde deterministisch is.</summary>
    public sealed class IndustryNetwork
    {
        public const int MaxSites = 100000;

        private readonly SortedDictionary<int, ExtractionSite> _sites = new SortedDictionary<int, ExtractionSite>();

        public IEnumerable<ExtractionSite> Sites => _sites.Values;

        public int Count => _sites.Count;

        public ExtractionSite Add(ExtractionSite site)
        {
            if (site is null)
            {
                throw new ArgumentNullException(nameof(site));
            }

            if (_sites.ContainsKey(site.Id))
            {
                throw new InvalidOperationException("Deze winning bestaat al: " + site.Id);
            }

            if (_sites.Count >= MaxSites)
            {
                throw new InvalidOperationException("Te veel winningen.");
            }

            _sites.Add(site.Id, site);
            return site;
        }

        public bool Remove(int id) => _sites.Remove(id);

        public ExtractionSite Get(int id) =>
            _sites.TryGetValue(id, out var site) ? site : throw new KeyNotFoundException("Onbekende winning: " + id);

        public bool Contains(int id) => _sites.ContainsKey(id);

        public int CountOf(IndustryKind kind) => _sites.Values.Count(s => s.Kind == kind);

        public IndustryWeek Step(int week) => Step(week, null);

        /// <summary>
        /// Een week met een beleidsfactor per winning (E13-06, zie <see cref="IndustryPolicy"/>): 0 laat de winning stilliggen (geen
        /// opbrengst, geen uitputting, geen werknemers), een andere factor schaalt opbrengst en vervuiling.
        /// </summary>
        public IndustryWeek Step(int week, Func<ExtractionSite, float> factorOf)
        {
            var extracted = new SortedDictionary<string, float>(StringComparer.Ordinal);
            var pollution = 0f;
            var workers = 0f;
            foreach (var site in _sites.Values)
            {
                var factor = factorOf?.Invoke(site) ?? 1f;
                if (!(factor > 0f))
                {
                    continue;
                }

                var result = site.Step(week);
                extracted.TryGetValue(site.Profile.Good, out var sofar);
                extracted[site.Profile.Good] = sofar + (result.Output * factor);
                pollution += result.Pollution * factor;
                workers += site.Depleted ? 0f : site.Workers;
            }

            return new IndustryWeek(extracted, pollution, workers);
        }

        internal void Replace(IEnumerable<ExtractionSite> sites)
        {
            _sites.Clear();
            foreach (var s in sites)
            {
                _sites.Add(s.Id, s);
            }
        }
    }
}
