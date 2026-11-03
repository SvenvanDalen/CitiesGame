using System;
using Cities.Simulation.Network;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Gameplay.Tools
{
    /// <summary>
    /// Plaatst een rotonde (E04-12) op open terrein: een ring met 2 tot 8 aansluitpunten waar de speler daarna wegen
    /// op aanlegt. Eén klik legt de ring aan; draaien verschuift de aansluitpunten in stappen van 15 graden.
    /// </summary>
    public sealed class RoundaboutTool : ITool
    {
        public const float RotateStepDegrees = 15f;

        private readonly RoadNetwork _network;
        private readonly RoadPreviewOverlay _overlay;
        private float _angle;
        private float3 _center;

        public RoundaboutTool(RoadNetwork network, RoadPreviewOverlay overlay)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
        }

        public string Name => "Rotonde";

        public GameObject Ghost => null;

        public bool RepeatsWhileHeld => false;

        public float Radius { get; set; } = 20f;

        public int Arms { get; set; } = 4;

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
            var reason = Roundabout.CanBuild(_network, _center, Radius, Arms);
            _overlay.ShowMarker(_center, reason == null);
            return new ToolPreview(_center, 0f, new float2(Radius * 2f), reason, $"Rotonde met {Arms} armen · straal {Radius:0} m · klik om te plaatsen");
        }

        public void Apply(in ToolPreview preview)
        {
            if (preview.IsValid)
            {
                Roundabout.Build(_network, _center, Radius, Arms, _angle);
            }
        }

        public bool CancelStep() => false;
    }
}
