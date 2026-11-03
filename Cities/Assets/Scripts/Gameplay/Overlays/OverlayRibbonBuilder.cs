using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Cities.Gameplay.Overlays
{
    /// <summary>
    /// Zet de waarden van een gebouw- of segmentoverlay om in mesh-gegevens: per waarde een vlak lint (vier hoekpunten, twee
    /// driehoeken) in de kleur van de legenda. Waarden zonder gegevens worden overgeslagen.
    /// </summary>
    public static class OverlayRibbonBuilder
    {
        /// <summary>Lint boven de ondergrond, zodat het niet in het wegdek of het terrein verdwijnt.</summary>
        public const float DefaultLift = 0.5f;

        /// <summary>Hoekpunten per lint.</summary>
        public const int VerticesPerRibbon = 4;

        public static int Build(IReadOnlyList<OverlayEntityValue> values, OverlayLegend legend, float lift, List<float3> vertices, List<uint> colors, List<int> indices)
        {
            if (values is null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            if (legend is null)
            {
                throw new ArgumentNullException(nameof(legend));
            }

            vertices.Clear();
            colors.Clear();
            indices.Clear();
            var ribbons = 0;
            var up = new float3(0f, lift, 0f);
            for (var i = 0; i < values.Count; i++)
            {
                var entity = values[i];
                var color = legend.ColorOf(entity.Value);
                if (color.A == 0)
                {
                    continue;
                }

                var half = math.max(entity.Width, 0.1f) * 0.5f;
                var along = entity.To.xz - entity.From.xz;
                var length = math.length(along);
                var forward = length > 1e-4f ? along / length : new float2(1f, 0f);
                var side = new float2(-forward.y, forward.x) * half;
                var extend = length > 1e-4f ? float2.zero : forward * half;
                var packed = color.Pack();
                var start = vertices.Count;
                vertices.Add(new float3(entity.From.x - side.x - extend.x, entity.From.y, entity.From.z - side.y - extend.y) + up);
                vertices.Add(new float3(entity.From.x + side.x - extend.x, entity.From.y, entity.From.z + side.y - extend.y) + up);
                vertices.Add(new float3(entity.To.x + side.x + extend.x, entity.To.y, entity.To.z + side.y + extend.y) + up);
                vertices.Add(new float3(entity.To.x - side.x + extend.x, entity.To.y, entity.To.z - side.y + extend.y) + up);
                for (var v = 0; v < VerticesPerRibbon; v++)
                {
                    colors.Add(packed);
                }

                indices.Add(start);
                indices.Add(start + 1);
                indices.Add(start + 2);
                indices.Add(start);
                indices.Add(start + 2);
                indices.Add(start + 3);
                ribbons++;
            }

            return ribbons;
        }
    }
}
