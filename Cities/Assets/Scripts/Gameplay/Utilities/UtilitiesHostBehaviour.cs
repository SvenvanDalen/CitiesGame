using System.Collections.Generic;
using Cities.Gameplay.Tools;
using Cities.Gameplay.Zoning;
using Cities.Simulation.Scheduling;
using Cities.Simulation.Utilities;
using Cities.World.Buildings;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Utilities
{
    /// <summary>
    /// Sluit stroom, water, riool en afval aan op de scene (E26-03): vier <see cref="UtilitySystem"/>s in de scheduler, gebouwen uit de spawner
    /// krijgen een aansluiting, centrales en pompen komen van plaatsingstools uit de werkbalk. Gebouwen zonder volledige aansluiting kleuren rood.
    /// </summary>
    public sealed class UtilitiesHostBehaviour : MonoBehaviour
    {
        [System.Serializable]
        public struct PlantPlacement
        {
            public PlantKind Kind;
            public PrefabPlacementBehaviour Placement;
        }

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private SimulationHostBehaviour _simulation;
        [SerializeField] private BuildingSpawnerBehaviour _spawner;
        [SerializeField] private PlantPlacement[] _plants = System.Array.Empty<PlantPlacement>();

        [Tooltip("Hoeveel simulatieticks tussen het bijwerken van de aansluitkleur.")]
        [SerializeField, Min(1)] private int _tintInterval = 30;

        private readonly Dictionary<GameObject, int> _handles = new Dictionary<GameObject, int>();
        private readonly List<(int Handle, Renderer[] Renderers)> _buildings = new List<(int, Renderer[])>();
        private MaterialPropertyBlock _block;

        public CityUtilities Utilities { get; } = new CityUtilities();

        /// <summary>Aansluitstatus van het gebouw dat bij dit object hoort; volledig aangesloten als het onbekend is.</summary>
        public UtilityStatus StatusOf(GameObject building)
        {
            return building != null && _handles.TryGetValue(building, out var handle) ? Utilities.StatusOf(handle) : new UtilityStatus(1f, 1f, 1f, 1f);
        }

        private void Start()
        {
            var scheduler = _simulation.Scheduler;
            scheduler.Register(new UtilitySystem("Stroom", Utilities.Power));
            scheduler.Register(new UtilitySystem("Water", Utilities.Water));
            scheduler.Register(new UtilitySystem("Riool", Utilities.Sewage));
            scheduler.Register(new UtilitySystem("Afval", Utilities.Waste));
            _block = new MaterialPropertyBlock();
            if (_spawner != null)
            {
                _spawner.Placed += OnBuildingPlaced;
            }

            foreach (var plant in _plants)
            {
                if (plant.Placement != null)
                {
                    var kind = plant.Kind;
                    plant.Placement.Placed += (go, position) => Utilities.AddPlant(kind, new float2(position.x, position.z));
                }
            }

            _simulation.Clock.Ticked += OnTick;
        }

        private void OnDestroy()
        {
            if (_spawner != null)
            {
                _spawner.Placed -= OnBuildingPlaced;
            }

            if (_simulation != null && _simulation.Clock != null)
            {
                _simulation.Clock.Ticked -= OnTick;
            }
        }

        private void OnBuildingPlaced(BuildingDefinition definition, GameObject building, float3 position)
        {
            var spec = definition.Spec;
            var handle = Utilities.AddBuilding(new float2(position.x, position.z), spec.Households, spec.Workplaces);
            _buildings.Add((handle, building.GetComponentsInChildren<Renderer>()));
            _handles[building] = handle;
        }

        private void OnTick(long tick)
        {
            if (tick % _tintInterval != 0)
            {
                return;
            }

            for (var i = _buildings.Count - 1; i >= 0; i--)
            {
                var (handle, renderers) = _buildings[i];
                if (renderers.Length == 0 || renderers[0] == null)
                {
                    Utilities.Remove(handle);
                    _buildings.RemoveAt(i);
                    continue;
                }

                var connected = Utilities.StatusOf(handle).IsConnected;
                foreach (var renderer in renderers)
                {
                    if (connected)
                    {
                        renderer.SetPropertyBlock(null);
                    }
                    else
                    {
                        _block.SetColor(BaseColor, new Color(0.85f, 0.3f, 0.3f));
                        renderer.SetPropertyBlock(_block);
                    }
                }
            }
        }
    }
}
