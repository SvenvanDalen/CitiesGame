using System;
using Unity.Mathematics;

namespace Cities.Simulation.Network
{
    /// <summary>
    /// Vorm van een rotonde (E04-12): de armen liggen gelijkmatig op een cirkel en de ring loopt tegen de klok in
    /// (gezien van boven, rechts rijden). Elk stuk tussen twee armen is een cirkelboog als kubische Bezier.
    /// </summary>
    public static class RoundaboutGeometry
    {
        public const float MinRadius = 12f;
        public const float MaxRadius = 40f;
        public const int MinArms = 2;

        /// <summary>Waarom deze straal of dit aantal armen niet mag, of <c>null</c> als het mag.</summary>
        public static string Validate(float radius, int arms)
        {
            if (!(radius >= MinRadius && radius <= MaxRadius))
            {
                return $"De straal ligt tussen {MinRadius:0} en {MaxRadius:0} m";
            }

            return arms < MinArms || arms > RoadNetwork.MaxArms ? $"Een rotonde heeft {MinArms} tot {RoadNetwork.MaxArms} armen" : null;
        }

        /// <summary>Hoek van arm <paramref name="index"/>, in radialen vanaf de +X-as naar +Z (tegen de klok in).</summary>
        public static float Angle(int index, int arms, float startAngle) => startAngle + (index * math.PI * 2f / arms);

        public static float3 Point(float3 center, float radius, float angle) =>
            center + new float3(math.cos(angle) * radius, 0f, math.sin(angle) * radius);

        /// <summary>Boog van <paramref name="from"/> naar <paramref name="to"/> (radialen, <paramref name="to"/> groter), tegen de klok in.</summary>
        public static Bezier3 Arc(float3 center, float radius, float from, float to)
        {
            if (!(to > from))
            {
                throw new ArgumentException("De boog loopt tegen de klok in, dus het eind ligt na het begin.", nameof(to));
            }

            // Standaardbenadering van een cirkelboog met één kubische Bezier; nauwkeurig tot ongeveer een halve cirkel.
            var handle = 4f / 3f * math.tan((to - from) / 4f) * radius;
            var start = Point(center, radius, from);
            var end = Point(center, radius, to);
            return new Bezier3(start, start + (Tangent(from) * handle), end - (Tangent(to) * handle), end);
        }

        private static float3 Tangent(float angle) => new float3(-math.sin(angle), 0f, math.cos(angle));
    }
}
