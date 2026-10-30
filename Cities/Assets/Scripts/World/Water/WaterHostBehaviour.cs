using Cities.Core.Diagnostics;
using Cities.Simulation.Scheduling;
using Cities.World.Grid;
using UnityEngine;

namespace Cities.World.Water
{
    /// <summary>
    /// Eigenaar van de <see cref="WaterSimulation"/> in een scene: maakt hem na het terrein, hangt het watersysteem aan
    /// de scheduler en laat de rivier instromen waar het terrein aan de rand het laagst ligt.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-800)]
    public sealed class WaterHostBehaviour : MonoBehaviour
    {
        [SerializeField] private SimulationHostBehaviour _simulationHost;
        [SerializeField] private WorldMapHostBehaviour _world;

        [Tooltip("Randen van de kaart waar de zee ligt.")]
        [SerializeField] private SeaEdges _sea = SeaEdges.West;

        [Tooltip("Hoogte van de zee in meters.")]
        [SerializeField] private float _seaLevel;

        [Tooltip("Debiet van de rivier die aan de zuidrand de kaart op stroomt, in m³/s; 0 is geen rivier.")]
        [SerializeField] private float _riverDischarge = 40f;

        private System.IDisposable _counter;

        public WaterSimulation Water { get; private set; }

        private void Awake()
        {
            var settings = WaterSettings.Default;
            settings.Sea = _sea;
            settings.SeaLevel = _seaLevel;
            Water = new WaterSimulation(_world.Map, settings);
            _simulationHost.Scheduler.Register(new WaterSystem(Water));
            _counter = DebugRegistry.Shared.AddCounter("Water", "Volume (m³)", () => (long)Water.Volume);
            if (_riverDischarge > 0f)
            {
                var x = LowestOnSouthEdge(_world.Map);
                Water.SetSource(x, 0f, _riverDischarge);
            }
        }

        private void OnDestroy()
        {
            _counter?.Dispose();
            Water?.Dispose();
            Water = null;
        }

        /// <summary>De rivier ligt in de laagste vallei: de plek op de zuidrand met de laagste terreinhoogte.</summary>
        private static float LowestOnSouthEdge(WorldMap map)
        {
            var layout = map.Height.Layout;
            var best = 0;
            for (var x = 1; x < layout.Width; x++)
            {
                if (map.Height[x, 0] < map.Height[best, 0])
                {
                    best = x;
                }
            }

            return best * layout.Spacing;
        }
    }
}
