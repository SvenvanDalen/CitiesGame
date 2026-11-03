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
        private const string Group = "Wegennet";

        private readonly List<IDisposable> _debug = new List<IDisposable>();

        public RoadNetwork Network { get; private set; }

        /// <summary>De verkeersregels per kruispunt (E04-13), opgeslagen in sectie <c>TRFC</c>.</summary>
        public TrafficRules Rules { get; } = new TrafficRules();

        /// <summary>De parkeerplekken van de stad (E08-10), opgeslagen in sectie <c>PARK</c>.</summary>
        public Cities.Simulation.Vehicles.ParkingRegistry Parking { get; } = new Cities.Simulation.Vehicles.ParkingRegistry();

        private void Awake()
        {
            Network = new RoadNetwork();
            var registry = DebugRegistry.Shared;
            if (registry.Find(LaneGraphOverlay.OverlayName) == null)
            {
                _debug.Add(registry.AddOverlay(new LaneGraphOverlay(Network)));
                _debug.Add(registry.AddCounter(Group, "Knooppunten", () => Network.NodeCount));
                _debug.Add(registry.AddCounter(Group, "Segmenten", () => Network.SegmentCount));
                _debug.Add(registry.AddCounter(Group, "Lanes", () => Network.LaneCount));
                _debug.Add(registry.AddCounter(Group, "Verbindingen", () => Network.ConnectionCount));
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
