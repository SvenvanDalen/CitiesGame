using System.Collections.Generic;
using Cities.Gameplay.Economy;
using Cities.Gameplay.Population;
using Cities.Gameplay.Tools;
using Cities.Gameplay.Utilities;
using Cities.Gameplay.Zoning;
using Cities.Simulation.Problems;
using Cities.Simulation.Scheduling;
using Cities.World.Buildings;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Services
{
    /// <summary>De probleemiconen zoals de diensten ze nodig hebben; de presentatielaag implementeert dit (Gameplay mag niet naar Presentation verwijzen).</summary>
    public interface IProblemIcons
    {
        ProblemTracker Tracker { get; }

        void SetRoof(int building, float3 roof);

        void ClearBuilding(int building);
    }

    /// <summary>
    /// Brandweer, politie, zorg en onderwijs in de scene (E26-07): plaatsbaar via de werkbalk, met dekking, probleemiconen boven gebouwen
    /// (geen stroom, water, riool, afval; misdaad zonder politie) en een dienstenscore die migratie beïnvloedt.
    /// </summary>
    public sealed class ServicesHostBehaviour : MonoBehaviour
    {
        [System.Serializable]
        public struct ServicePlacement
        {
            public CityService Kind;
            public PrefabPlacementBehaviour Placement;
        }

        [SerializeField] private SimulationHostBehaviour _simulation;
        [SerializeField] private BuildingSpawnerBehaviour _spawner;
        [SerializeField] private UtilitiesHostBehaviour _utilities;
        [SerializeField] private EconomyHostBehaviour _economy;
        [SerializeField] private PopulationHostBehaviour _population;
        [Tooltip("Een component dat IProblemIcons implementeert (ProblemIconsBehaviour).")]
        [SerializeField] private MonoBehaviour _problemsSource;

        private IProblemIcons _problems;
        [SerializeField] private ServicePlacement[] _services = System.Array.Empty<ServicePlacement>();

        [Tooltip("Hoeveel simulatieticks tussen het bijwerken van problemen en dekking.")]
        [SerializeField, Min(1)] private int _interval = 30;

        [Tooltip("Misdaadicoontjes verschijnen pas vanaf zoveel inwoners.")]
        [SerializeField, Min(0)] private int _crimeFromPopulation = 50;

        private sealed class Tracked
        {
            public int Id;
            public GameObject Object;
            public float2 Position;
        }

        private readonly Dictionary<GameObject, (CityService Kind, float2 Position, Cities.Simulation.Services.ServiceStation Station)> _placedServices =
            new Dictionary<GameObject, (CityService, float2, Cities.Simulation.Services.ServiceStation)>();
        private readonly List<Tracked> _buildings = new List<Tracked>();
        private readonly List<float2> _points = new List<float2>();
        private int _nextId;

        public CityServices Services { get; } = new CityServices();

        private void Start()
        {
            _problems = _problemsSource as IProblemIcons;
            foreach (var service in _services)
            {
                if (service.Placement != null)
                {
                    var kind = service.Kind;
                    service.Placement.Tool.RangeMeters = CityServices.RadiusOf(kind);
                    service.Placement.Placed += (go, position) => _placedServices[go] = (kind, new float2(position.x, position.z), OnService(kind, position));
                    service.Placement.Removed += (go, position) => OnServiceRemoved(go);
                }
            }

            if (_spawner != null)
            {
                _spawner.Placed += OnBuilding;
            }

            _simulation.Clock.Ticked += OnTick;
        }

        private void OnDestroy()
        {
            if (_spawner != null)
            {
                _spawner.Placed -= OnBuilding;
            }

            if (_simulation != null && _simulation.Clock != null)
            {
                _simulation.Clock.Ticked -= OnTick;
            }
        }

        /// <summary>Zet een opgeslagen dienst terug in het model (E26-14); het gebouwobject zelf wordt niet teruggezet.</summary>
        public void Restore(CityService kind, float2 position) => OnService(kind, new float3(position.x, 0f, position.y));

        private Cities.Simulation.Services.ServiceStation OnService(CityService kind, float3 position)
        {
            var station = Services.Place(kind, new float2(position.x, position.z));
            if (station != null)
            {
                _economy.Economy.AddStation(station);
            }

            _economy.Economy.AddUpkeep(CityServices.UpkeepPerMonthOf(kind));
            return station;
        }

        /// <summary>De plaatsing van een dienst is ongedaan gemaakt (E01-12): dekking, station en onderhoud gaan weer weg.</summary>
        private void OnServiceRemoved(GameObject placed)
        {
            if (!_placedServices.TryGetValue(placed, out var entry))
            {
                return;
            }

            _placedServices.Remove(placed);
            Services.Remove(entry.Kind, entry.Position);
            if (entry.Station != null)
            {
                _economy.Economy.RemoveStation(entry.Station);
            }

            _economy.Economy.AddUpkeep(-CityServices.UpkeepPerMonthOf(entry.Kind));
        }

        private void OnBuilding(BuildingDefinition definition, GameObject building, float3 position)
        {
            var tracked = new Tracked { Id = _nextId++, Object = building, Position = new float2(position.x, position.z) };
            _buildings.Add(tracked);
            _points.Add(tracked.Position);
            var top = position.y + 10f;
            var renderer = building.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                top = renderer.bounds.max.y;
            }

            _problems.SetRoof(tracked.Id, new float3(position.x, top, position.z));
        }

        private void OnTick(long tick)
        {
            if (tick % _interval != 0)
            {
                return;
            }

            var tracker = _problems.Tracker;
            var crime = _population.Population.Population >= _crimeFromPopulation;
            for (var i = _buildings.Count - 1; i >= 0; i--)
            {
                var building = _buildings[i];
                if (building.Object == null)
                {
                    _problems.ClearBuilding(building.Id);
                    _buildings.RemoveAt(i);
                    _points.RemoveAt(i);
                    continue;
                }

                var status = _utilities.StatusOf(building.Object);
                tracker.Report(building.Id, BuildingProblem.NoPower, status.Power < 0.999f);
                tracker.Report(building.Id, BuildingProblem.NoWater, status.Water < 0.999f);
                tracker.Report(building.Id, BuildingProblem.NoSewage, status.Sewage < 0.999f);
                tracker.Report(building.Id, BuildingProblem.GarbageFull, status.Waste < 0.999f);
                tracker.Report(building.Id, BuildingProblem.Crime, crime && !Services.IsCovered(CityService.Police, building.Position));
            }

            _population.Population.ServicesAppeal = Services.AverageCoverage(_points);
        }
    }
}
