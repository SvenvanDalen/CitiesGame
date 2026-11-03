using System;
using System.Collections.Generic;
using System.Linq;

namespace Cities.Simulation.TouristSites
{
    /// <summary>
    /// De toeristische gebouwen die in de stad staan (E14-08), op volgorde van ID zodat opslaan en rekenen deterministisch zijn.
    /// </summary>
    public sealed class TouristSiteRegistry
    {
        public const int MaxSites = 100_000;

        private readonly SortedDictionary<int, TouristSite> _sites = new SortedDictionary<int, TouristSite>();

        public int NextId { get; private set; } = 1;

        public int Count => _sites.Count;

        public IEnumerable<TouristSite> Sites => _sites.Values;

        public int TotalBeds => _sites.Values.Sum(s => s.Profile.Beds);

        public int Add(string definitionId, TouristSiteProfile profile, int x, int y, TouristSiteConditions conditions)
        {
            if (_sites.Count >= MaxSites)
            {
                throw new InvalidOperationException("Het maximum aantal toeristische gebouwen is bereikt.");
            }

            var site = new TouristSite(NextId, definitionId, profile, x, y, conditions);
            _sites.Add(site.Id, site);
            NextId++;
            return site.Id;
        }

        public bool Remove(int id) => _sites.Remove(id);

        public bool TryGet(int id, out TouristSite site) => _sites.TryGetValue(id, out site);

        /// <summary>Werkt de omgeving van een gebouw bij (vervuiling, lawaai, weer); onbekend ID geeft false.</summary>
        public bool SetConditions(int id, TouristSiteConditions conditions)
        {
            if (!_sites.TryGetValue(id, out var site))
            {
                return false;
            }

            _sites[id] = site.WithConditions(conditions);
            return true;
        }

        public TouristAppealResult CityAppeal() => TouristAppeal.ForCity(_sites.Values);

        public TouristAppealResult AppealAt(int x, int y, float cellSize) => TouristAppeal.At(_sites.Values, x, y, cellSize);

        internal void Restore(IEnumerable<TouristSite> sites, int nextId)
        {
            _sites.Clear();
            foreach (var site in sites)
            {
                _sites.Add(site.Id, site);
            }

            NextId = nextId;
        }
    }
}
