using System;
using Cities.Core.Diagnostics;
using Cities.Simulation.Network;
using Unity.Mathematics;

namespace Cities.Gameplay.Tools
{
    /// <summary>Overlay <c>Aanleg</c>: de weg die de <see cref="RoadTool"/> zou leggen, groen als het mag en rood als niet.</summary>
    public sealed class RoadPreviewOverlay : IDebugOverlay
    {
        public const string OverlayName = "Aanleg";

        private const int Steps = 24;
        private const float Lift = 0.3f;
        private const float Cross = 3f;

        private bool _hasCurve;
        private Bezier3 _curve;
        private bool _hasMarker;
        private float3 _marker;
        private bool _valid;

        public string Name => OverlayName;

        public void Clear()
        {
            _hasCurve = false;
            _hasMarker = false;
        }

        public void ShowMarker(float3 position, bool valid)
        {
            _hasCurve = false;
            _hasMarker = true;
            _marker = position;
            _valid = valid;
        }

        public void ShowCurve(Bezier3 curve, bool valid)
        {
            _hasCurve = true;
            _curve = curve;
            _hasMarker = true;
            _marker = curve.P3;
            _valid = valid;
        }

        public void Collect(DebugGeometry geometry, in DebugView view)
        {
            if (geometry is null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            var color = _valid ? DebugColor.Good : DebugColor.Bad;
            var lift = new float3(0f, Lift, 0f);
            if (_hasCurve)
            {
                var previous = _curve.Position(0f);
                for (var i = 1; i <= Steps; i++)
                {
                    var next = _curve.Position(i / (float)Steps);
                    geometry.AddLine(previous + lift, next + lift, color);
                    previous = next;
                }
            }

            if (_hasMarker)
            {
                geometry.AddLine(_marker + lift - new float3(Cross, 0f, 0f), _marker + lift + new float3(Cross, 0f, 0f), color);
                geometry.AddLine(_marker + lift - new float3(0f, 0f, Cross), _marker + lift + new float3(0f, 0f, Cross), color);
            }
        }
    }
}
