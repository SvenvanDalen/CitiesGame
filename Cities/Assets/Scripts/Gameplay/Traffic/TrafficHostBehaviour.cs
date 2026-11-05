using System.Collections.Generic;
using Cities.Gameplay.Population;
using Cities.Simulation.Network;
using Cities.Simulation.Scheduling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cities.Gameplay.Traffic
{
    /// <summary>
    /// Laat voertuigen over het wegennet rijden en tekent ze (E26-05): het aantal volgt de bevolking (of een minimum), elke simulatietick
    /// rijdt <see cref="RoadTraffic"/> een stap, en de auto's worden geïnstantieerd getekend met een eenvoudig model.
    /// </summary>
    public sealed class TrafficHostBehaviour : MonoBehaviour
    {
        [SerializeField] private SimulationHostBehaviour _simulation;
        [SerializeField] private RoadNetworkHostBehaviour _network;
        [SerializeField] private PopulationHostBehaviour _population;
        [SerializeField] private Mesh _mesh;
        [SerializeField] private Material _material;

        [Tooltip("Inwoners per voertuig.")]
        [SerializeField, Min(1)] private int _residentsPerCar = 6;
        [SerializeField, Min(0)] private int _minimumCars = 10;
        [SerializeField, Min(1)] private int _maximumCars = 3000;
        [SerializeField, Min(1)] private int _spawnPerTick = 2;

        [Tooltip("Moet gelijk zijn aan de simulatieklok.")]
        [SerializeField, Min(1)] private int _ticksPerSecond = 10;

        private readonly List<TrafficCar> _cars = new List<TrafficCar>();
        private readonly List<Matrix4x4> _matrices = new List<Matrix4x4>();
        private RoadTraffic _traffic;
        private float _tickSeconds;

        public RoadTraffic Traffic => _traffic;

        private void Start()
        {
            _traffic = new RoadTraffic(_network.Network, unchecked((int)_simulation.Random.Seed));
            _tickSeconds = 1f / Mathf.Max(1, _ticksPerSecond);
            _simulation.Clock.Ticked += OnTick;
        }

        private void OnDestroy()
        {
            if (_simulation != null && _simulation.Clock != null)
            {
                _simulation.Clock.Ticked -= OnTick;
            }
        }

        private void OnTick(long tick)
        {
            var people = _population != null ? _population.Population.Population : 0;
            var target = Mathf.Clamp(people / _residentsPerCar, _minimumCars, _maximumCars);
            for (var i = 0; i < _spawnPerTick && _traffic.Count < target; i++)
            {
                if (!_traffic.Spawn())
                {
                    break;
                }
            }

            if (_traffic.Count > target)
            {
                _traffic.Trim(target);
            }

            _traffic.Step(_tickSeconds);
        }

        private void LateUpdate()
        {
            if (_traffic == null || _mesh == null || _material == null)
            {
                return;
            }

            _traffic.Collect(_cars);
            var parameters = new RenderParams(_material) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false, worldBounds = new Bounds(Vector3.zero, Vector3.one * 20000f) };
            _matrices.Clear();
            foreach (var car in _cars)
            {
                var position = new Vector3(car.Position.x, car.Position.y + 0.75f, car.Position.z);
                _matrices.Add(Matrix4x4.TRS(position, Quaternion.Euler(0f, car.Yaw * Mathf.Rad2Deg, 0f), new Vector3(1.9f, 1.5f, RoadTraffic.CarLength)));
            }

            for (var offset = 0; offset < _matrices.Count; offset += 1000)
            {
                Graphics.RenderMeshInstanced(parameters, _mesh, 0, _matrices, Mathf.Min(1000, _matrices.Count - offset), offset);
            }
        }
    }
}
