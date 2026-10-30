using System;
using System.Collections.Generic;
using Cities.Core.Diagnostics;
using UnityEngine;

namespace Cities.Simulation.Network
{
    /// <summary>Eigenaar van het <see cref="RoadNetwork"/> in een scene: maakt het bij het starten, toont de lanegraaf en ruimt op.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class RoadNetworkHostBehaviour : MonoBehaviour
    {
        private readonly List<IDisposable> _debug = new List<IDisposable>();

        public RoadNetwork Network { get; private set; }

        private void Awake()
        {
            Network = new RoadNetwork();
            var registry = DebugRegistry.Shared;
            if (registry.Find(LaneGraphOverlay.OverlayName) == null)
            {
                _debug.Add(registry.AddOverlay(new LaneGraphOverlay(Network)));
                _debug.Add(registry.AddCounter("Wegennet", "Knooppunten", () => Network.NodeCount));
                _debug.Add(registry.AddCounter("Wegennet", "Segmenten", () => Network.SegmentCount));
                _debug.Add(registry.AddCounter("Wegennet", "Lanes", () => Network.LaneCount));
                _debug.Add(registry.AddCounter("Wegennet", "Verbindingen", () => Network.ConnectionCount));
            }
        }

        private void OnDestroy()
        {
            foreach (var registration in _debug)
            {
                registration.Dispose();
            }

            _debug.Clear();
            Network?.Dispose();
            Network = null;
        }
    }
}
