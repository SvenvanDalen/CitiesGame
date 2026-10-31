using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Configuration;
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
        [SerializeField] private DefinitionCatalog _catalog;
        [Tooltip("Optioneel: de geplaatste gebouwen, voor de inspectietool.")]
        [SerializeField] private PrefabPlacementBehaviour _placement;
        [SerializeField] private string _defaultType = "road.two-lane";
        [SerializeField] private bool _selectOnStart;

        private readonly List<IDisposable> _debug = new List<IDisposable>();

        public RoadTool Tool { get; private set; }

        public DemolishTool Demolish { get; private set; }

        /// <summary>Wisselt het wegtype van een bestaand segment naar het gekozen type.</summary>
        public RoadEditTool Upgrade { get; private set; }

        /// <summary>Keert de rijrichting van een eenrichtingsweg om.</summary>
        public RoadEditTool Reverse { get; private set; }

        /// <summary>De sim-inspector voor wegen en (met <c>_placement</c>) gebouwen.</summary>
        public InspectTool Inspect { get; private set; }

        /// <summary>De wegtypes uit de catalogus, op ID gesorteerd.</summary>
        public IReadOnlyList<RoadTypeDefinition> Types { get; private set; } = new List<RoadTypeDefinition>();

        /// <summary>Na de <see cref="RoadNetworkHostBehaviour"/> en de <see cref="WorldMapHostBehaviour"/> en vóór de UI.</summary>
        private void Awake()
        {
            var overlay = new RoadPreviewOverlay();
            Tool = new RoadTool(_network.Network, _world.Map, overlay);
            Demolish = new DemolishTool(_network.Network, overlay);
            Upgrade = new RoadEditTool(RoadEdit.Upgrade, _network.Network, _world.Map, Tool, overlay);
            Reverse = new RoadEditTool(RoadEdit.Reverse, _network.Network, _world.Map, Tool, overlay);
            Inspect = new InspectTool(
                new RoadInspection(_network.Network),
                _placement != null ? new BuildingInspection(_placement.Occupancy, _world.Map.Dimensions) : null);
            _debug.Add(DebugRegistry.Shared.AddOverlay(overlay));
            _tools.Controller.ActiveChanged += OnActiveChanged;
            if (_catalog != null)
            {
                Types = _catalog.Definitions.OfType<RoadTypeDefinition>().ToList();
                var start = Types.FirstOrDefault(t => t.Id == _defaultType) ?? Types.FirstOrDefault();
                if (start != null)
                {
                    Tool.Use(start.Id, start.Spec);
                }
            }
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
            DebugRegistry.Shared.SetEnabled(RoadPreviewOverlay.OverlayName, ReferenceEquals(active, Tool) || ReferenceEquals(active, Demolish) || ReferenceEquals(active, Upgrade) || ReferenceEquals(active, Reverse));
    }
}
