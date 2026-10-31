using System.Linq;
using Cities.Core.Configuration;
using Cities.Gameplay.Tools;
using Cities.Simulation.Scheduling;
using Cities.World.Buildings;
using Cities.World.Grid;
using Cities.World.Trees;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Zoning
{
    /// <summary>
    /// Laat gebouwen groeien in de zones van de scene: per simulatietick een paar pogingen van de <see cref="BuildingSpawner"/>,
    /// dus de stad groeit niet tijdens pauze en sneller op hogere snelheid.
    /// </summary>
    public sealed class BuildingSpawnerBehaviour : MonoBehaviour
    {
        [SerializeField] private SimulationHostBehaviour _simulation;
        [SerializeField] private WorldMapHostBehaviour _world;
        [SerializeField] private ZoneToolBehaviour _zones;

        [Tooltip("De plaatsing waarvan de bezetting gedeeld wordt, zodat handmatige en gegroeide gebouwen elkaar niet overlappen.")]
        [SerializeField] private PrefabPlacementBehaviour _placement;
        [SerializeField] private DefinitionCatalog _catalog;
        [SerializeField, Min(1)] private int _attemptsPerTick = 4;
        [SerializeField] private int _seed = 1;

        // shortcut: vaste vraag tot er inwoners, banen en inkomsten zijn die het DemandModel (E05-03) voeden.
        [Header("Vraag (0 tot 1) tot het vraagmodel gevoed wordt")]
        [SerializeField, Range(0f, 1f)] private float _residentialDemand = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _commercialDemand = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _workDemand = 0.5f;

        private BuildingSpawner _spawner;

        private void Start()
        {
            var buildings = _catalog.Definitions.OfType<BuildingDefinition>();
            _spawner = new BuildingSpawner(_world.Map, _zones.Service, _placement.Occupancy, buildings, Demand, Place, _seed);
            _simulation.Clock.Ticked += OnTick;
        }

        private void OnDestroy()
        {
            if (_simulation != null && _simulation.Clock != null)
            {
                _simulation.Clock.Ticked -= OnTick;
            }

            _spawner?.Detach();
        }

        private void OnTick(long tick) => _spawner.Grow(_attemptsPerTick);

        private float Demand(ZoneType type)
        {
            switch (type)
            {
                case ZoneType.Residential:
                    return _residentialDemand;
                case ZoneType.Commercial:
                    return _commercialDemand;
                default:
                    return _workDemand;
            }
        }

        private void Place(BuildingDefinition definition, Footprint footprint, float3 position)
        {
            _placement.Occupancy.Add(footprint);
            TreeBrush.ClearForBuilding(_world.Map, new RectInt(footprint.Origin.X, footprint.Origin.Z, footprint.SizeX, footprint.SizeZ));
            Instantiate(definition.Prefab, position, Quaternion.Euler(0f, footprint.YawDegrees, 0f), transform);
        }
    }
}
