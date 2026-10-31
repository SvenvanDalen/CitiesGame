using System.Collections.Generic;
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

        [Header("Bouwfase (E05-06)")]
        [Tooltip("Vermenigvuldiger op de bouwtijd van het gebouwtype: 1 is de waarde uit de definitie, 0,5 bouwt twee keer zo snel, 0 meteen klaar.")]
        [SerializeField, Min(0f)] private float _constructionTimeScale = 1f;
        [Tooltip("Materiaal van de steiger; leeg gebruikt een grijs URP Lit-materiaal.")]
        [SerializeField] private Material _scaffoldMaterial;

        private sealed class Site
        {
            public Transform Building;
            public GameObject Scaffold;
            public long Start;
            public long Duration;
        }

        // De kosten van groeigebouwen betaalt de ontwikkelaar (ConstructionCost is daar 0); de duur komt uit de definitie.
        private readonly List<Site> _sites = new List<Site>();
        private BuildingSpawner _spawner;

        private void Start()
        {
            var buildings = _catalog.Definitions.OfType<BuildingDefinition>();
            _spawner = new BuildingSpawner(_world.Map, _zones.Service, _placement.Occupancy, buildings, Demand, Place, (ulong)_seed);
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

        private void OnTick(long tick)
        {
            _spawner.Grow(_attemptsPerTick);
            for (var i = _sites.Count - 1; i >= 0; i--)
            {
                var site = _sites[i];
                var progress = ConstructionSchedule.Progress(site.Start, site.Duration, tick);
                // Het gebouw rijst op uit de grond, binnen de steiger.
                site.Building.localScale = new Vector3(1f, Mathf.Max(progress, 0.02f), 1f);
                if (progress >= 1f)
                {
                    Destroy(site.Scaffold);
                    _sites.RemoveAt(i);
                }
            }
        }

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
            var building = Instantiate(definition.Prefab, position, Quaternion.Euler(0f, footprint.YawDegrees, 0f), transform);
            var calendar = _simulation.Clock.Calendar;
            var duration = ConstructionSchedule.DurationTicks(definition.Spec.ConstructionDays, calendar.TicksPerDay, _constructionTimeScale);
            if (duration <= 0)
            {
                return;
            }

            var scaffold = BuildScaffold(building, footprint);
            building.transform.localScale = new Vector3(1f, 0.02f, 1f);
            _sites.Add(new Site { Building = building.transform, Scaffold = scaffold, Start = _simulation.Clock.Tick, Duration = duration });
        }

        private GameObject BuildScaffold(GameObject building, Footprint footprint)
        {
            var bounds = building.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
            var cell = _world.Map.Dimensions.CellSize;
            var go = new GameObject("Steiger");
            go.transform.SetParent(building.transform.parent, false);
            go.transform.position = building.transform.position;
            go.AddComponent<MeshFilter>().sharedMesh = ScaffoldingMesh.Build(ScaffoldingLayout.Boxes(footprint.SizeX * cell, footprint.SizeZ * cell, bounds.size.y));
            go.AddComponent<MeshRenderer>().sharedMaterial = _scaffoldMaterial != null ? _scaffoldMaterial : ScaffoldingMesh.DefaultMaterial();
            return go;
        }
    }
}
