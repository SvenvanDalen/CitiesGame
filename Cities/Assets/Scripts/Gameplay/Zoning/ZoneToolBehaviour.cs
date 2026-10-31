using Cities.Core.Diagnostics;
using Cities.Gameplay.Tools;
using Cities.Simulation.Network;
using Cities.World.Grid;
using UnityEngine;

namespace Cities.Gameplay.Zoning
{
    /// <summary>Maakt de <see cref="ZoneService"/> en de <see cref="ZoneTool"/> voor het wegennet en de kaart van de scene.</summary>
    [DefaultExecutionOrder(-300)]
    public sealed class ZoneToolBehaviour : MonoBehaviour
    {
        [SerializeField] private ToolsBehaviour _tools;
        [SerializeField] private RoadNetworkHostBehaviour _network;
        [SerializeField] private WorldMapHostBehaviour _world;

        private System.IDisposable _debug;

        public ZoneService Service { get; private set; }

        public ZoneTool Tool { get; private set; }

        /// <summary>Na de <see cref="RoadNetworkHostBehaviour"/> en de <see cref="WorldMapHostBehaviour"/> en vóór de UI.</summary>
        private void Awake()
        {
            var overlay = new ZonePreviewOverlay(_world.Map);
            Service = new ZoneService(_world.Map, _network.Network);
            Tool = new ZoneTool(Service, _world.Map, overlay);
            _debug = DebugRegistry.Shared.AddOverlay(overlay);
            _tools.Controller.ActiveChanged += OnActiveChanged;
        }

        private void OnDestroy()
        {
            _tools.Controller.ActiveChanged -= OnActiveChanged;
            _debug?.Dispose();
        }

        /// <summary>Het voorbeeld hoort alleen te staan zolang de zonetool actief is.</summary>
        private void OnActiveChanged(ITool active) => DebugRegistry.Shared.SetEnabled(ZonePreviewOverlay.OverlayName, ReferenceEquals(active, Tool));
    }
}
