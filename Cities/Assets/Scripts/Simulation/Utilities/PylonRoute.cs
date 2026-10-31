using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Cities.Simulation.Utilities
{
    /// <summary>
    /// Plant een hoogspanningslijn (E06-04): pylonen staan alleen op vaste grond en overbruggen water en bergen tot de maximale overspanning.
    /// </summary>
    public static class PylonRoute
    {
        public const float DefaultMaxSpan = 250f;
        private const int Samples = 25;

        /// <summary>
        /// Geeft de pylonen van <paramref name="from"/> tot en met <paramref name="to"/>, of een lege lijst als de lijn niet te leggen is. Elke stap kiest
        /// het verste staanbare punt op de rechte lijn binnen <paramref name="maxSpan"/>; <paramref name="canStand"/> is false op water en steile grond.
        /// </summary>
        // shortcut: de lijn blijft recht tussen begin en eind (geen omweg om een meer); een routezoeker als een rechte lijn te vaak faalt.
        public static IReadOnlyList<float2> Plan(float2 from, float2 to, float maxSpan, Func<float2, bool> canStand)
        {
            if (maxSpan <= 0f || float.IsNaN(maxSpan))
            {
                throw new ArgumentOutOfRangeException(nameof(maxSpan), maxSpan, "De overspanning moet groter dan nul zijn.");
            }

            if (canStand == null)
            {
                throw new ArgumentNullException(nameof(canStand));
            }

            if (!canStand(from) || !canStand(to))
            {
                return Array.Empty<float2>();
            }

            var route = new List<float2> { from };
            var at = from;
            while (math.distance(at, to) > maxSpan)
            {
                var next = FarthestStandable(at, math.normalize(to - from), maxSpan, canStand);
                if (next == null)
                {
                    return Array.Empty<float2>();
                }

                at = next.Value;
                route.Add(at);
            }

            route.Add(to);
            return route;
        }

        private static float2? FarthestStandable(float2 at, float2 direction, float maxSpan, Func<float2, bool> canStand)
        {
            for (var i = Samples; i >= 1; i--)
            {
                var candidate = at + (direction * (maxSpan * i / Samples));
                if (canStand(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>Wat een transformator doorgeeft: de last tot zijn vermogen, min de verliezen.</summary>
        public static float TransformerOutput(float load, float rating, float efficiency = 0.98f)
        {
            if (load < 0f || rating < 0f || efficiency < 0f || efficiency > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(load), "Last en vermogen zijn nul of meer, het rendement ligt tussen 0 en 1.");
            }

            return math.min(load, rating) * efficiency;
        }
    }
}
