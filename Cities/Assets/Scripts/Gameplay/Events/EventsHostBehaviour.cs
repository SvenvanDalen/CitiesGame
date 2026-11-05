using Cities.Gameplay.Economy;
using Cities.Gameplay.Population;
using Cities.Simulation.Economy;
using Cities.Simulation.Scheduling;
using Cities.Simulation.Timing;
using Cities.World.Grid;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Events
{
    /// <summary>
    /// Rampen, mijlpalen en meldingen in de scene (E26-12): elke dag loopt <see cref="CityEvents"/> mee met de klok.
    /// </summary>
    public sealed class EventsHostBehaviour : MonoBehaviour
    {
        [SerializeField] private SimulationHostBehaviour _simulation;
        [SerializeField] private EconomyHostBehaviour _economy;
        [SerializeField] private PopulationHostBehaviour _population;
        [SerializeField] private WorldMapHostBehaviour _world;

        public CityEvents Events { get; private set; }

        private void Awake() => Events = new CityEvents(_simulation.Random.Seed);

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
            var days = (int)_simulation.Clock.Calendar.DaysElapsed;
            var dimensions = _world.Map.Dimensions;
            var area = new float2(dimensions.CellsX * dimensions.CellSize, dimensions.CellsZ * dimensions.CellSize);
            Events.Daily(days, Ledger.WeekOf(days), _population.Population.Population, _economy.Economy.Ledger, area);
        }
    }
}
