using Cities.Gameplay.Zoning;
using Cities.Simulation.Scheduling;
using Cities.Simulation.Timing;
using Cities.World.Buildings;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Population
{
    /// <summary>
    /// Sluit inwoners, banen en het vraagmodel aan op de scene (E26-04): elk gegroeid gebouw telt mee, elke speeldag rekent
    /// <see cref="CityPopulation"/>, en de gebouwspawner gebruikt de berekende vraag in plaats van vaste waarden.
    /// </summary>
    public sealed class PopulationHostBehaviour : MonoBehaviour
    {
        [SerializeField] private SimulationHostBehaviour _simulation;
        [SerializeField] private BuildingSpawnerBehaviour _spawner;

        public CityPopulation Population { get; } = new CityPopulation();

        private void Start()
        {
            if (_spawner != null)
            {
                _spawner.Placed += OnPlaced;
                _spawner.DemandSource = Population.DemandFor;
            }

            _simulation.Clock.Calendar.DayStarted += OnDay;
        }

        private void OnDestroy()
        {
            if (_spawner != null)
            {
                _spawner.Placed -= OnPlaced;
                _spawner.DemandSource = null;
            }

            if (_simulation != null && _simulation.Clock != null)
            {
                _simulation.Clock.Calendar.DayStarted -= OnDay;
            }
        }

        private void OnPlaced(BuildingDefinition definition, GameObject building, float3 position)
        {
            var spec = definition.Spec;
            Population.AddBuilding(spec.Households, spec.Workplaces, spec.ShopCapacity);
        }

        private void OnDay(GameDate date) => Population.Daily();
    }
}
