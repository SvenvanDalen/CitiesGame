using System;
using Cities.World.Grid;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.World.Terrain
{
    /// <summary>Wat het terreinpenseel doet.</summary>
    public enum TerraformMode : byte
    {
        Raise,
        Lower,

        /// <summary>Naar de hoogte waar de streek begon.</summary>
        Flatten,

        /// <summary>Naar het gemiddelde van de buren, tegen scherpe randen.</summary>
        Smooth,

        /// <summary>Naar een vooraf gekozen hoogte.</summary>
        SetHeight,
    }

    /// <summary>
    /// Verandert de hoogte van de hoekpunten onder een rond penseel. Het penseel werkt het sterkst in het midden en
    /// loopt zacht af naar de rand; per aanroep verschuift een hoekpunt hoogstens <c>step</c> meter.
    /// </summary>
    public static class TerrainBrush
    {
        /// <summary>Diepste punt: ruimte voor havens en meren onder zeeniveau.</summary>
        public const float MinHeight = -40f;

        public const float MaxHeight = 1000f;

        /// <summary>Past het penseel toe en geeft de gewijzigde hoekpunten (max exclusief), of een lege rect.</summary>
        /// <param name="center">Midden in wereldmeters (x, z).</param>
        /// <param name="step">Meters die het midden deze keer hoogstens verschuift, bijvoorbeeld sterkte × deltaTime.</param>
        /// <param name="target">Doelhoogte voor <see cref="TerraformMode.Flatten"/> en <see cref="TerraformMode.SetHeight"/>.</param>
        public static RectInt Apply(WorldMap map, TerraformMode mode, float2 center, float radius, float step, float target = 0f)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var spacing = map.Dimensions.CellSize;
            var corners = CornersUnder(map, center, radius);
            if (radius <= 0f || step <= 0f || corners.width <= 0 || corners.height <= 0)
            {
                return default;
            }

            // Ook buiten gladstrijken: een kopie van hoogstens 67 × 67 hoekpunten kost niets.
            var source = Snapshot(map, corners);
            ref var heights = ref map.Height;
            for (var z = corners.yMin; z < corners.yMax; z++)
            {
                for (var x = corners.xMin; x < corners.xMax; x++)
                {
                    var amount = step * Falloff(math.distance(new float2(x, z) * spacing, center), radius);
                    if (amount <= 0f)
                    {
                        continue;
                    }

                    var height = heights[x, z];
                    var goal = mode switch
                    {
                        TerraformMode.Raise => height + amount,
                        TerraformMode.Lower => height - amount,
                        TerraformMode.Smooth => source.Average(x, z),
                        _ => target,
                    };
                    heights[x, z] = math.clamp(height + math.clamp(goal - height, -amount, amount), MinHeight, MaxHeight);
                }
            }

            return corners;
        }

        /// <summary>Gewicht van 1 in het midden tot 0 op de rand, zonder knik.</summary>
        public static float Falloff(float distance, float radius)
        {
            var t = math.saturate(distance / radius);
            var inner = 1f - (t * t);
            return inner * inner;
        }

        /// <summary>Hoekpunten binnen het vierkant om het penseel, beperkt tot de kaart.</summary>
        public static RectInt CornersUnder(WorldMap map, float2 center, float radius)
        {
            var spacing = map.Dimensions.CellSize;
            return Clip(map, (int2)math.ceil((center - radius) / spacing), (int2)math.floor((center + radius) / spacing) + 1);
        }

        /// <summary>Doorsnede met de kaart; <see cref="RectInt.ClampToBounds"/> verkleint niet aan de min-kant.</summary>
        private static RectInt Clip(WorldMap map, int2 min, int2 max)
        {
            min = math.max(min, 0);
            max = math.min(max, new int2(map.AllCorners.width, map.AllCorners.height));
            return math.all(max > min) ? new RectInt(min.x, min.y, max.x - min.x, max.y - min.y) : default;
        }

        private static HeightSnapshot Snapshot(WorldMap map, RectInt corners)
        {
            var area = Clip(map, new int2(corners.xMin - 1, corners.yMin - 1), new int2(corners.xMax + 1, corners.yMax + 1));
            var values = new float[area.width * area.height];
            for (var z = 0; z < area.height; z++)
            {
                for (var x = 0; x < area.width; x++)
                {
                    values[(z * area.width) + x] = map.Height[area.xMin + x, area.yMin + z];
                }
            }

            return new HeightSnapshot(area, values);
        }

        /// <summary>Hoogtes van vóór de streek, zodat gladstrijken niet afhangt van de volgorde.</summary>
        private sealed class HeightSnapshot
        {
            private readonly RectInt _area;
            private readonly float[] _values;

            public HeightSnapshot(RectInt area, float[] values)
            {
                _area = area;
                _values = values;
            }

            /// <summary>Gemiddelde van het hoekpunt en zijn buren (3 × 3, voor zover op de kaart).</summary>
            public float Average(int x, int z)
            {
                var sum = 0f;
                var count = 0;
                for (var dz = math.max(z - 1, _area.yMin); dz <= math.min(z + 1, _area.yMax - 1); dz++)
                {
                    for (var dx = math.max(x - 1, _area.xMin); dx <= math.min(x + 1, _area.xMax - 1); dx++)
                    {
                        sum += _values[((dz - _area.yMin) * _area.width) + (dx - _area.xMin)];
                        count++;
                    }
                }

                return sum / count;
            }
        }
    }
}
