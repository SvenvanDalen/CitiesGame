using Cities.Core.Configuration;
using Cities.Gameplay.NewCity;
using Cities.Gameplay.Population;
using Cities.Gameplay.Tools;
using Cities.Gameplay.Zoning;
using Cities.Simulation.Economy;
using Cities.Simulation.Scheduling;
using Cities.Simulation.Timing;
using Cities.World.Buildings;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Economy
{
    /// <summary>
    /// Laat het geld draaien in de scene (E26-06): startkapitaal uit de gekozen moeilijkheid, wekelijkse belasting en kosten, en bouwen kost geld
    /// doordat de gereedschappen via de <see cref="ToolController"/> betalen.
    /// </summary>
    public sealed class EconomyHostBehaviour : MonoBehaviour
    {
        [SerializeField] private SimulationHostBehaviour _simulation;
        [SerializeField] private PopulationHostBehaviour _population;
        [SerializeField] private ToolsBehaviour _tools;
        [SerializeField] private BuildingSpawnerBehaviour _spawner;

        public CityEconomy Economy { get; private set; }

        private int Week => Ledger.WeekOf(_simulation.Clock.Calendar.DaysElapsed);

        private void Awake() => Economy = new CityEconomy((Difficulty)StartRequest.ChosenDifficulty);

        private void Start()
        {
            var controller = _tools.Controller;
            controller.CanAfford = Economy.CanAfford;
            controller.TryPay = cost => Economy.TryBuild(Week, cost);
            _simulation.Clock.Calendar.DayStarted += OnDay;
            if (_spawner != null)
            {
                _spawner.Placed += OnBuilding;
            }
        }

        private void OnDestroy()
        {
            if (_tools != null)
            {
                _tools.Controller.CanAfford = null;
                _tools.Controller.TryPay = null;
            }

            if (_simulation != null && _simulation.Clock != null)
            {
                _simulation.Clock.Calendar.DayStarted -= OnDay;
            }

            if (_spawner != null)
            {
                _spawner.Placed -= OnBuilding;
            }
        }


        private void OnBuilding(BuildingDefinition definition, GameObject building, float3 position) => Economy.AddUpkeep(definition.Spec.UpkeepPerMonth);

        private void OnDay(GameDate date)
        {
            var days = _simulation.Clock.Calendar.DaysElapsed;
            if (days > 0 && days % Ledger.DaysPerWeek == 0)
            {
                Economy.EndWeek(Ledger.WeekOf(days), _population.Population);
            }
        }
    }
}
