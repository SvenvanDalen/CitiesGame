using System.Linq;
using System.Collections.Generic;
using Cities.Gameplay.Economy;
using Cities.Gameplay.Population;
using Cities.Gameplay.Tools;
using Cities.Simulation.Economy;
using Cities.Simulation.Scheduling;
using Cities.Simulation.Timing;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cities.Gameplay.Transit
{
    /// <summary>
    /// Openbaar vervoer in de scene (E26-08): haltes plaatsen met de werkbalk, lijnen beheren in het OV-paneel, bussen rijden en worden
    /// getekend, ticketinkomsten gaan naar het saldo.
    /// </summary>
    public sealed class TransitHostBehaviour : MonoBehaviour
    {
        [SerializeField] private SimulationHostBehaviour _simulation;
        [SerializeField] private EconomyHostBehaviour _economy;
        [SerializeField] private PopulationHostBehaviour _population;
        [SerializeField] private PrefabPlacementBehaviour _stopPlacement;
        [SerializeField] private Mesh _mesh;
        [SerializeField] private Material _material;

        [Tooltip("Moet gelijk zijn aan de simulatieklok.")]
        [SerializeField, Min(1)] private int _ticksPerSecond = 10;

        private readonly List<Matrix4x4> _matrices = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _stops = new List<Matrix4x4>();
        private readonly List<int> _stopIds = new List<int>();
        private readonly System.Collections.Generic.Dictionary<GameObject, int> _placedStops = new System.Collections.Generic.Dictionary<GameObject, int>();

        public CityTransit Transit { get; } = new CityTransit();

        private void Start()
        {
            if (_stopPlacement != null)
            {
                _stopPlacement.Placed += OnStopPlaced;
                _stopPlacement.Removed += OnStopRemoved;
                _stopPlacement.CanRemove = go => !_placedStops.TryGetValue(go, out var stop) || !OnAnyLine(stop);
            }

            _simulation.Clock.Ticked += OnTick;
            _simulation.Clock.Calendar.DayStarted += OnDay;
        }

        private void OnDestroy()
        {
            if (_stopPlacement != null)
            {
                _stopPlacement.Placed -= OnStopPlaced;
                _stopPlacement.Removed -= OnStopRemoved;
            }

            if (_simulation != null && _simulation.Clock != null)
            {
                _simulation.Clock.Ticked -= OnTick;
                _simulation.Clock.Calendar.DayStarted -= OnDay;
            }
        }

        private void OnStopPlaced(GameObject placed, float3 position) =>
            _placedStops[placed] = RestoreStop(new float2(position.x, position.z), position.y);

        private bool OnAnyLine(int stop) => Transit.Network.Lines.Any(l => l.Stops.Contains(stop));

        /// <summary>De plaatsing van een halte is ongedaan gemaakt (E01-12): de halte en haar weergave gaan weg.</summary>
        private void OnStopRemoved(GameObject placed, float3 position)
        {
            if (_placedStops.TryGetValue(placed, out var stop) && Transit.Network.TryRemoveStop(stop))
            {
                _placedStops.Remove(placed);
                var index = _stopIds.IndexOf(stop);
                if (index >= 0)
                {
                    _stopIds.RemoveAt(index);
                    _stops.RemoveAt(index);
                }
            }
        }

        /// <summary>Voegt een halte toe, ook bij het laden van een save (E26-14); geeft het halte-id terug.</summary>
        public int RestoreStop(float2 position, float height = 0f)
        {
            var id = Transit.AddStop(position);
            _stops.Add(Matrix4x4.TRS(new Vector3(position.x, height + 2f, position.y), Quaternion.identity, new Vector3(2f, 4f, 2f)));
            _stopIds.Add(id);
            return id;
        }

        private void OnTick(long tick) => Transit.Fleet.Tick(1f / Mathf.Max(1, _ticksPerSecond));

        private void OnDay(GameDate date)
        {
            Transit.Daily(_population.Population.Population);
            var days = _simulation.Clock.Calendar.DaysElapsed;
            if (days > 0 && days % Ledger.DaysPerWeek == 0)
            {
                Transit.Finance.Settle(Ledger.WeekOf(days), _economy.Economy.Ledger);
            }
        }

        private void LateUpdate()
        {
            if (_mesh == null || _material == null)
            {
                return;
            }

            var parameters = new RenderParams(_material) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false, worldBounds = new Bounds(Vector3.zero, Vector3.one * 20000f) };
            if (_stops.Count > 0)
            {
                Graphics.RenderMeshInstanced(parameters, _mesh, 0, _stops);
            }

            _matrices.Clear();
            foreach (var bus in Transit.Fleet.Buses)
            {
                var p = Transit.PositionOf(bus);
                _matrices.Add(Matrix4x4.TRS(new Vector3(p.x, 2f, p.y), Quaternion.identity, new Vector3(2.6f, 3f, 11f)));
            }

            if (_matrices.Count > 0)
            {
                Graphics.RenderMeshInstanced(parameters, _mesh, 0, _matrices);
            }
        }
    }
}
