using System;
using System.Collections.Generic;
using Cities.Core.Diagnostics;
using Cities.Simulation.Network;
using Cities.World.Grid;
using UnityEngine;

namespace Cities.Gameplay.Tools
{
    /// <summary>Maakt de <see cref="RoadTool"/> voor het wegennet en de kaart van de scene en toont de weg die erbij hoort als overlay <c>Aanleg</c>.</summary>
    [DefaultExecutionOrder(-300)]
    public sealed class RoadToolBehaviour : MonoBehaviour
    {
        [SerializeField] private ToolsBehaviour _tools;
        [SerializeField] private RoadNetworkHostBehaviour _network;
        [SerializeField] private WorldMapHostBehaviour _world;
        [SerializeField] private bool _selectOnStart;

        private readonly List<IDisposable> _debug = new List<IDisposable>();

        public RoadTool Tool { get; private set; }

        /// <summary>Na de <see cref="RoadNetworkHostBehaviour"/> en de <see cref="WorldMapHostBehaviour"/> en vóór de UI.</summary>
        private void Awake()
        {
            var overlay = new RoadPreviewOverlay();
            Tool = new RoadTool(_network.Network, _world.Map, overlay);
            _debug.Add(DebugRegistry.Shared.AddOverlay(overlay));
            _tools.Controller.ActiveChanged += OnActiveChanged;
        }

        private void Start()
        {
            if (_selectOnStart)
            {
                _tools.Controller.Select(Tool);
            }
        }

        private void OnDestroy()
        {
            _tools.Controller.ActiveChanged -= OnActiveChanged;
            foreach (var registration in _debug)
            {
                registration.Dispose();
            }

            _debug.Clear();
        }

        /// <summary>Het voorbeeld hoort alleen te staan zolang de wegtool actief is.</summary>
        private void OnActiveChanged(ITool active) =>
            DebugRegistry.Shared.SetEnabled(RoadPreviewOverlay.OverlayName, ReferenceEquals(active, Tool));
    }
}
