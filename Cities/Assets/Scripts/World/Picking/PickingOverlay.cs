using System;
using System.Globalization;
using Cities.Core.Diagnostics;
using Unity.Mathematics;

namespace Cities.World.Picking
{
    /// <summary>
    /// Debug-overlay "Picking": een kruis op het aangewezen object met soort en ID, en een klein kruis op het
    /// terreinpunt eronder.
    /// </summary>
    public sealed class PickingOverlay : IDebugOverlay
    {
        public const string OverlayName = "Picking";

        private readonly Func<PickResult> _current;

        public PickingOverlay(Func<PickResult> current) => _current = current ?? throw new ArgumentNullException(nameof(current));

        public string Name => OverlayName;

        public void Collect(DebugGeometry geometry, in DebugView view)
        {
            if (geometry == null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            var result = _current();
            if (result.HasGround && result.Hit.Kind != PickKind.Terrain)
            {
                AddCross(geometry, result.Ground.Point, Size(view, result.Ground.Point) * 0.5f, DebugColor.Neutral);
            }

            if (!result.Hit.IsHit)
            {
                return;
            }

            var hit = result.Hit;
            var size = Size(view, hit.Point);
            var color = ColorOf(hit.Kind);
            AddCross(geometry, hit.Point, size, color);
            geometry.AddLabel(hit.Point + new float3(0f, size * 1.5f, 0f), Describe(hit), color);
        }

        /// <summary>Nederlandse naam voor de soort, zoals in de interface.</summary>
        public static string NameOf(PickKind kind)
        {
            switch (kind)
            {
                case PickKind.Terrain: return "Terrein";
                case PickKind.Building: return "Gebouw";
                case PickKind.Segment: return "Weg";
                case PickKind.Node: return "Knooppunt";
                case PickKind.Vehicle: return "Voertuig";
                case PickKind.Citizen: return "Burger";
                default: return "Niets";
            }
        }

        /// <summary>"Gebouw 12 (v3)", of voor terrein de positie en hoogte.</summary>
        public static string Describe(PickHit hit)
        {
            if (hit.Kind == PickKind.Terrain)
            {
                return string.Format(CultureInfo.InvariantCulture, "Terrein {0:0}, {1:0} op {2:0.0} m", hit.Point.x, hit.Point.z, hit.Point.y);
            }

            var index = (uint)hit.Id;
            var generation = (uint)(hit.Id >> 32);
            return hit.IsHit ? $"{NameOf(hit.Kind)} {index} (v{generation})" : NameOf(hit.Kind);
        }

        private static DebugColor ColorOf(PickKind kind)
        {
            switch (kind)
            {
                case PickKind.Building: return DebugColor.Info;
                case PickKind.Segment:
                case PickKind.Node: return DebugColor.Accent;
                case PickKind.Vehicle: return DebugColor.Warning;
                case PickKind.Citizen: return DebugColor.Good;
                default: return DebugColor.Neutral;
            }
        }

        /// <summary>Groter op afstand, zodat het kruis op het scherm ongeveer even groot blijft.</summary>
        private static float Size(in DebugView view, float3 point) => math.max(0.5f, math.distance(view.CameraPosition, point) * 0.015f);

        private static void AddCross(DebugGeometry geometry, float3 point, float size, DebugColor color)
        {
            geometry.AddLine(point - new float3(size, 0f, 0f), point + new float3(size, 0f, 0f), color);
            geometry.AddLine(point - new float3(0f, 0f, size), point + new float3(0f, 0f, size), color);
            geometry.AddLine(point, point + new float3(0f, size * 1.5f, 0f), color);
        }
    }
}
