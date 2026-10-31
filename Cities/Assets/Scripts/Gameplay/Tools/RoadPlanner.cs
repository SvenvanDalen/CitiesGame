using System.Globalization;
using Cities.Simulation.Network;
using Unity.Mathematics;

namespace Cities.Gameplay.Tools
{
    /// <summary>Hoe de <see cref="RoadTool"/> klikken omzet in segmenten.</summary>
    public enum RoadDrawMode
    {
        /// <summary>Twee klikken: een rechte weg.</summary>
        Straight,

        /// <summary>Drie klikken: begin, buigpunt en eind; de weg buigt in een vloeiende boog.</summary>
        Curve,

        /// <summary>Elke klik legt een rechte weg vanaf het vorige punt, tot Escape.</summary>
        Free,
    }

    /// <summary>De rekenkundige kant van het tekenen van wegen: snappen op hoek en lengte en bochten.</summary>
    public static class RoadPlanner
    {
        /// <summary>Kortste weg die nog een segment mag zijn.</summary>
        public const float MinLength = 8f;

        /// <summary>Langste segment; langere wegen bestaan uit meer segmenten.</summary>
        public const float MaxLength = 1000f;

        /// <summary>
        /// Zet <paramref name="point"/> op een veelvoud van <paramref name="angleStep"/> graden (ten opzichte van
        /// <paramref name="referenceYaw"/>) en <paramref name="lengthStep"/> meter vanaf <paramref name="start"/>; een stap van 0 snapt niet.
        /// De hoogte blijft die van <paramref name="point"/>.
        /// </summary>
        public static float3 Snap(float3 start, float3 point, float angleStep, float lengthStep, float referenceYaw = 0f)
        {
            var offset = point.xz - start.xz;
            var length = math.length(offset);
            if (length < 1e-4f || (angleStep <= 0f && lengthStep <= 0f))
            {
                return point;
            }

            var yaw = Yaw(new float3(offset.x, 0f, offset.y));
            if (angleStep > 0f)
            {
                yaw = referenceYaw + (math.round((yaw - referenceYaw) / angleStep) * angleStep);
            }

            if (lengthStep > 0f)
            {
                length = math.max(lengthStep, math.round(length / lengthStep) * lengthStep);
            }

            var radians = math.radians(yaw);
            var direction = new float2(math.sin(radians), math.cos(radians));
            var snapped = start.xz + (direction * length);
            return new float3(snapped.x, point.y, snapped.y);
        }

        /// <summary>Zet <paramref name="point"/> op de dichtstbijzijnde rasterlijnen van <paramref name="step"/> meter; een stap van 0 snapt niet. De hoogte blijft.</summary>
        public static float3 SnapToGrid(float3 point, float step) =>
            step <= 0f ? point : new float3(math.round(point.x / step) * step, point.y, math.round(point.z / step) * step);

        /// <summary>Lengte, koers en hoogteverschil van een weg voor in het voorbeeld, bijvoorbeeld "42.0 m · 90° · +1.5 m (3.6 %)".</summary>
        public static string Describe(Bezier3 curve)
        {
            var length = curve.Length();
            var rise = curve.P3.y - curve.P0.y;
            var grade = length > 0f ? rise / length * 100f : 0f;
            return string.Format(CultureInfo.InvariantCulture, "{0:0.0} m · {1:0}° · {2:+0.0;-0.0;0.0} m ({3:0.0} %)", length, Yaw(curve.Direction(1f)), rise, math.abs(grade));
        }

        /// <summary>Koers in graden van noord (+Z) met de klok mee, tussen 0 en 360.</summary>
        public static float Yaw(float3 direction)
        {
            var degrees = math.degrees(math.atan2(direction.x, direction.z));
            return degrees < 0f ? degrees + 360f : degrees;
        }

        /// <summary>Kubische Bézier die dezelfde boog volgt als de kwadratische met buigpunt <paramref name="control"/>.</summary>
        public static Bezier3 Quadratic(float3 start, float3 control, float3 end) =>
            new Bezier3(start, start + (2f / 3f * (control - start)), end + (2f / 3f * (control - end)), end);

        /// <summary>Horizontale afstand van <paramref name="point"/> tot de hartlijn, benaderd met <paramref name="steps"/> rechte stukjes.</summary>
        public static float DistanceTo(Bezier3 curve, float3 point, int steps = 32)
        {
            var best = float.MaxValue;
            var previous = curve.P0.xz;
            for (var i = 1; i <= steps; i++)
            {
                var next = curve.Position(i / (float)steps).xz;
                var edge = next - previous;
                var along = math.lengthsq(edge) > 1e-8f ? math.saturate(math.dot(point.xz - previous, edge) / math.lengthsq(edge)) : 0f;
                best = math.min(best, math.distance(point.xz, previous + (edge * along)));
                previous = next;
            }

            return best;
        }

        /// <summary>Waarom een weg met deze hartlijn niet mag, of <c>null</c>.</summary>
        public static string Check(Bezier3 curve)
        {
            var length = curve.Length();
            if (length < MinLength)
            {
                return "Te kort";
            }

            return length > MaxLength ? "Te lang" : null;
        }
    }
}
