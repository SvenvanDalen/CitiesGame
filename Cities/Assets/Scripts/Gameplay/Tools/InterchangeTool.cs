using System;
using Cities.Simulation.Network;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Tools
{
    /// <summary>
    /// Plaatst een snelwegknooppunt (E04-11) op open terrein: een snelweg met een kruisende weg en de ramps van de gekozen preset. Eén klik legt
    /// alles aan; draaien verdraait het geheel in stappen van 15 graden.
    /// </summary>
    public sealed class InterchangeTool : ITool
    {
        public const float RotateStepDegrees = 15f;

        private readonly RoadNetwork _network;
        private readonly RoadPreviewOverlay _overlay;
        private float _angle;
        private float3 _center;

        public InterchangeTool(RoadNetwork network, RoadPreviewOverlay overlay)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
        }

        public string Name => "Knooppunt";

        public GameObject Ghost => null;

        public bool RepeatsWhileHeld => false;

        public InterchangeKind Kind { get; set; } = InterchangeKind.Cloverleaf;

        public void Activate() => _overlay.Clear();

        public void Deactivate() => _overlay.Clear();

        public ToolPreview UpdatePreview(in ToolInput input)
        {
            if (!input.Pick.HasGround)
            {
                _overlay.Clear();
                return ToolPreview.Hidden;
            }

            _angle += math.radians(input.RotateSteps * RotateStepDegrees);
            _center = input.Pick.Ground.Point;
            var reason = Interchange.CanBuild(_network, _center);
            _overlay.ShowMarker(_center, reason == null);
            var preset = InterchangeGeometry.Presets[(int)Kind];
            return new ToolPreview(_center, 0f, new float2(InterchangeGeometry.Footprint * 2f), reason, $"{preset.Name} · {preset.Description} · klik om te plaatsen");
        }

        public void Apply(in ToolPreview preview)
        {
            if (preview.IsValid)
            {
                Interchange.Build(_network, Kind, _center, _angle);
            }
        }

        public bool CancelStep() => false;
    }
}
