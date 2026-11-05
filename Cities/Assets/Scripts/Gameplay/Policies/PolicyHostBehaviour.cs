using System.Collections.Generic;
using Cities.Core.Configuration;
using Cities.Gameplay.Economy;
using Cities.Gameplay.Population;
using Cities.Gameplay.Tools;
using Cities.Simulation.Policies;
using Cities.Simulation.Scheduling;
using Cities.Simulation.Timing;
using Cities.World.Grid;
using UnityEngine;

namespace Cities.Gameplay.Policies
{
    /// <summary>
    /// Beleid en districten in de scene (E26-10): bouwt het beleidsboek uit de definitiecatalogus, rekent de kosten wekelijks af en
    /// levert het model voor het beleidspaneel en het districtpenseel.
    /// </summary>
    public sealed class PolicyHostBehaviour : MonoBehaviour
    {
        [SerializeField] private SimulationHostBehaviour _simulation;
        [SerializeField] private EconomyHostBehaviour _economy;
        [SerializeField] private PopulationHostBehaviour _population;
        [SerializeField] private WorldMapHostBehaviour _world;
        [SerializeField] private ToolsBehaviour _tools;
        [SerializeField] private DefinitionCatalog _catalog;

        public CityPolicies Policies { get; private set; }

        public IReadOnlyDictionary<string, string> Names { get; private set; }

        public Cities.Simulation.Economy.Ledger Ledger => _economy.Economy.Ledger;

        public int Week => Cities.Simulation.Economy.Ledger.WeekOf(_simulation.Clock.Calendar.DaysElapsed);

        public int CityPopulation => _population.Population.Population;

        public DistrictTool Brush { get; private set; }

        public ToolController Controller => _tools.Controller;

        private void Awake()
        {
            var specs = new Dictionary<string, PolicySpec>();
            var names = new Dictionary<string, string>();
            if (_catalog != null)
            {
                foreach (var definition in _catalog.Definitions)
                {
                    if (definition is PolicyDefinition policy)
                    {
                        specs[policy.Id] = policy.Spec;
                        names[policy.Id] = policy.DisplayName;
                    }
                }
            }

            Policies = new CityPolicies(specs, _world.Map.Dimensions);
            Brush = new DistrictTool(Policies.Districts);
            Names = names;
        }

        private void Start() => _simulation.Clock.Calendar.DayStarted += OnDay;

        private void OnDestroy()
        {
            if (_simulation != null && _simulation.Clock != null)
            {
                _simulation.Clock.Calendar.DayStarted -= OnDay;
            }
        }

        private void OnDay(GameDate date)
        {
            var days = _simulation.Clock.Calendar.DaysElapsed;
            if (days > 0 && days % Cities.Simulation.Economy.Ledger.DaysPerWeek == 0)
            {
                Policies.ChargeWeek(_economy.Economy.Ledger, Cities.Simulation.Economy.Ledger.WeekOf(days), _population.Population.Population);
            }
        }
    }
}
