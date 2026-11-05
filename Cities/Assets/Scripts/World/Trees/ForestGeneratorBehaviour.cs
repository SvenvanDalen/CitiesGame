using Cities.Core.Diagnostics;
using Cities.Simulation.Scheduling;
using Cities.World.Grid;
using Cities.World.Terrain;
using UnityEngine;

namespace Cities.World.Trees
{
    /// <summary>Plant bij het starten bossen op de kaart, na het terrein en de grondbedekking.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class ForestGeneratorBehaviour : MonoBehaviour
    {
        [SerializeField] private WorldMapHostBehaviour _world;
        [SerializeField] private ForestSettings _settings = ForestSettings.Default;

        [SerializeField] private bool _useWorldSeed = true;

        private void Start()
        {
            if (_useWorldSeed)
            {
                var host = FindAnyObjectByType<SimulationHostBehaviour>();
                if (host != null && host.Random != null)
                {
                    _settings.Seed = WorldSeed.Forest(host.Random.Seed);
                }
            }

            ForestGenerator.Generate(_world.Map, _settings);
            StartupProgress.Report("Bossen");
        }
    }
}
