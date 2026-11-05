using System;
using System.Collections.Generic;
using Cities.Simulation.Citizens;
using Cities.Simulation.Demand;
using Cities.World.Grid;

namespace Cities.Gameplay.Population
{
    /// <summary>
    /// Inwoners, werk en vraag van de hele stad (E26-04). Gebouwen leveren woningen, banen en winkelcapaciteit; elke speeldag berekent
    /// <see cref="DemandModel"/> de vraag, bepaalt <see cref="Migration"/> hoeveel mensen komen of gaan, en vullen of legen de woningen.
    /// De aantrekkelijkheid van landwaarde, diensten en belasting stelt de rest van het spel in via de Appeal-eigenschappen.
    /// </summary>
    public sealed class CityPopulation
    {
        /// <summary>Aandeel van de bevolking dat kan werken.</summary>
        public const float WorkforceShare = 0.6f;

        /// <summary>Wat een inwoner per dag aan winkels wil uitgeven, in euro.</summary>
        public const float SpendingPerPerson = 20f;

        /// <summary>
        /// Versnelling op de dagelijkse migratie van <see cref="Migration"/> (spelbalans, E26-15): een nieuwe stad groeit anders met één
        /// inwoner per dag, wat in een speelsessie van een uur te traag is.
        /// </summary>
        public const int MigrationBoost = 25;

        public const int PeoplePerHousehold = 3;

        private sealed class Home
        {
            public int Capacity;
            public int Residents;
        }

        private readonly Dictionary<int, Home> _homes = new Dictionary<int, Home>();
        private readonly Dictionary<int, int> _jobs = new Dictionary<int, int>();
        private readonly Dictionary<int, long> _shops = new Dictionary<int, long>();
        private int _next;
        private Demand _demand;

        public int Population { get; private set; }

        public int HomeCapacity { get; private set; }

        public int Jobs { get; private set; }

        public long ShopCapacity { get; private set; }

        /// <summary>Werkenden, begrensd door banen en beroepsbevolking.</summary>
        public int Employed => Math.Min(Jobs, Workforce);

        public int Workforce => (int)(Population * WorkforceShare);

        public int Unemployed => Math.Max(0, Workforce - Jobs);

        public int OpenJobs => Math.Max(0, Jobs - Workforce);

        public int VacantHomes => Math.Max(0, HomeCapacity - Population);

        /// <summary>De gladgestreken vraag, -1 tot 1 per soort.</summary>
        public Demand Demand => _demand;

        /// <summary>Aantrekkelijkheid van landwaarde, diensten en belasting (0 tot 1); standaard neutraal.</summary>
        public float LandValueAppeal { get; set; } = 0.5f;

        public float ServicesAppeal { get; set; } = 0.5f;

        public float TaxAppeal { get; set; } = 0.5f;

        /// <summary>Voegt een gebouw toe en geeft de handle terug.</summary>
        public int AddBuilding(int households, int workplaces, long shopCapacity)
        {
            var handle = _next++;
            if (households > 0)
            {
                _homes[handle] = new Home { Capacity = households * PeoplePerHousehold };
                HomeCapacity += households * PeoplePerHousehold;
            }

            if (workplaces > 0)
            {
                _jobs[handle] = workplaces;
                Jobs += workplaces;
            }

            if (shopCapacity > 0)
            {
                _shops[handle] = shopCapacity;
                ShopCapacity += shopCapacity;
            }

            return handle;
        }

        public void RemoveBuilding(int handle)
        {
            if (_homes.TryGetValue(handle, out var home))
            {
                HomeCapacity -= home.Capacity;
                Population -= home.Residents;
                _homes.Remove(handle);
            }

            if (_jobs.TryGetValue(handle, out var jobs))
            {
                Jobs -= jobs;
                _jobs.Remove(handle);
            }

            if (_shops.TryGetValue(handle, out var shop))
            {
                ShopCapacity -= shop;
                _shops.Remove(handle);
            }
        }

        public int ResidentsOf(int handle) => _homes.TryGetValue(handle, out var home) ? home.Residents : 0;

        /// <summary>Eén speeldag: vraag bijwerken, mensen laten komen of gaan.</summary>
        public void Daily()
        {
            var inputs = new DemandInputs(
                homes: HomeCapacity,
                vacantHomes: VacantHomes,
                unemployed: Unemployed,
                openJobs: OpenJobs,
                potentialSpending: Population * SpendingPerPerson,
                shopCapacity: ShopCapacity);
            _demand = DemandModel.Approach(_demand, DemandModel.Compute(inputs), rate: 0.5f, deltaSeconds: 1f);

            var jobsAppeal = Workforce == 0 ? (Jobs > 0 ? 1f : 0f) : Math.Min(1f, 0.5f + ((OpenJobs - Unemployed) / (float)Math.Max(1, Workforce)));
            var appeal = new CityAppeal(jobsAppeal, LandValueAppeal, ServicesAppeal, TaxAppeal);
            var net = Migration.DailyNet(appeal, Population, VacantHomes) * MigrationBoost;
            if (net > 0)
            {
                MoveIn(Math.Min(net, VacantHomes));
            }
            else if (net < 0)
            {
                MoveOut(-net);
            }
        }

        /// <summary>De vraag voor een zonetype, voor de gebouwspawner: wonen, winkels, of werk voor industrie en kantoren.</summary>
        public float DemandFor(ZoneType type)
        {
            switch (type)
            {
                case ZoneType.Residential: return _demand.Residential;
                case ZoneType.Commercial: return _demand.Commerce;
                default: return _demand.Work;
            }
        }

        private void MoveIn(int people)
        {
            foreach (var home in _homes.Values)
            {
                if (people <= 0)
                {
                    break;
                }

                var take = Math.Min(people, home.Capacity - home.Residents);
                home.Residents += take;
                Population += take;
                people -= take;
            }
        }

        private void MoveOut(int people)
        {
            foreach (var home in _homes.Values)
            {
                if (people <= 0)
                {
                    break;
                }

                var take = Math.Min(people, home.Residents);
                home.Residents -= take;
                Population -= take;
                people -= take;
            }
        }
    }
}
