using System.Linq;
using Cities.Gameplay.Overlays;
using Cities.Gameplay.Population;
using Cities.Gameplay.Services;
using Cities.Gameplay.Transit;
using Cities.Gameplay.Zoning;
using Cities.Simulation.Scheduling;
using Cities.Simulation.Timing;
using Cities.World.Buildings;
using Cities.World.Grid;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Environment
{
    /// <summary>
    /// Milieu en landwaarde in de scene (E26-09): houdt woningen, industrie en haltes bij, schrijft dagelijks de landwaarde in de kaartlaag,
    /// stuurt de aantrekkelijkheid van de stad voor de bevolking en levert de bronnen voor de milieu-overlays.
    /// </summary>
    public sealed class EnvironmentHostBehaviour : MonoBehaviour
    {
        [SerializeField] private SimulationHostBehaviour _simulation;
        [SerializeField] private BuildingSpawnerBehaviour _spawner;
        [SerializeField] private WorldMapHostBehaviour _world;
        [SerializeField] private PopulationHostBehaviour _population;
        [SerializeField] private ServicesHostBehaviour _services;
        [SerializeField] private TransitHostBehaviour _transit;

        public CityEnvironment Environment { get; } = new CityEnvironment();

        /// <summary>Bronnen voor <see cref="EnvironmentOverlays"/>: lucht en geluid.</summary>
        public EnvironmentSources Sources { get; private set; }

        private void Awake()
        {
            Sources = new EnvironmentSources
            {
                AirConcentration = (x, z) => Environment.AirAt(CellCenter(x, z)) * 100f,
                NoiseDecibel = (x, z) => Environment.NoiseAt(CellCenter(x, z)),
            };
        }

        private void Start()
        {
            if (_spawner != null)
            {
                _spawner.Placed += OnPlaced;
            }

            _simulation.Clock.Calendar.DayStarted += OnDay;
        }

        private void OnDestroy()
        {
            if (_spawner != null)
            {
                _spawner.Placed -= OnPlaced;
            }

            if (_simulation != null && _simulation.Clock != null)
            {
                _simulation.Clock.Calendar.DayStarted -= OnDay;
            }
        }

        private float2 CellCenter(int x, int z)
        {
            var size = _world != null && _world.Map != null ? _world.Map.Dimensions.CellSize : 1f;
            return new float2((x + 0.5f) * size, (z + 0.5f) * size);
        }

        private void OnPlaced(BuildingDefinition definition, GameObject building, float3 position)
        {
            var point = new float2(position.x, position.z);
            switch (definition.Spec.Zone)
            {
                case ZoneType.Residential:
                    Environment.AddHome(point);
                    break;
                case ZoneType.Industrial:
                    Environment.AddIndustry(point);
                    break;
            }
        }

        private void OnDay(GameDate date)
        {
            var services = _services != null ? _services.Services : null;
            if (_transit != null)
            {
                var network = _transit.Transit.Network;
                Environment.SetStops(network.Stops.Select(s => s.Value));
            }

            if (_population != null)
            {
                _population.Population.LandValueAppeal = Environment.HomeAppeal(services);
            }

            WriteLandValue(services);
        }

        private void WriteLandValue(CityServices services)
        {
            if (_world == null || _world.Map == null)
            {
                return;
            }

            var map = _world.Map;
            var size = map.Dimensions.CellSize * WorldMap.CoarseCellsPerSample;
            foreach (var point in Environment.Homes.Concat(Environment.Industry))
            {
                var cx = (int)(point.x / size);
                var cz = (int)(point.y / size);
                for (var dz = -2; dz <= 2; dz++)
                {
                    for (var dx = -2; dx <= 2; dx++)
                    {
                        var x = cx + dx;
                        var z = cz + dz;
                        if (x < 0 || z < 0 || x >= map.LandValue.Layout.Width || z >= map.LandValue.Layout.Depth)
                        {
                            continue;
                        }

                        map.LandValue[x, z] = Environment.LandValueAt(new float2((x + 0.5f) * size, (z + 0.5f) * size), services) * 500f;
                    }
                }
            }
        }
    }
}
