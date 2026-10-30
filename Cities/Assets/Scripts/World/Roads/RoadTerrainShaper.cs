using System;
using System.Collections.Generic;
using Cities.Simulation.Network;
using Cities.World.Grid;
using Cities.World.Terrain;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.World.Roads
{
    /// <summary>
    /// Legt het terrein onder een nieuwe weg vlak: de hoekpunten binnen de halve breedte plus een cel gaan naar de
    /// hoogte van de hartlijn (net onder het wegdek) en lopen daarbuiten zacht terug naar het oude terrein. Kapt de bomen
    /// en maakt de grond bebouwd onder de weg. Wordt aangeroepen bij het aanleggen; het terrein is een opgeslagen
    /// laag en wordt dus niet bij het laden opnieuw aangepast.
    /// </summary>
    public static class RoadTerrainShaper
    {
        /// <summary>Het terrein ligt zoveel onder het wegdek, tegen flikkeren.</summary>
        public const float SurfaceClearance = 0.15f;

        /// <summary>Breedte van het zachte randje in cellen.</summary>
        public const float ShoulderCells = 2f;

        /// <summary>Halve breedte van het wegdek van <paramref name="lanes"/>.</summary>
        public static float HalfWidth(IReadOnlyList<LaneSpec> lanes)
        {
            if (lanes == null)
            {
                throw new ArgumentNullException(nameof(lanes));
            }

            var half = 0f;
            foreach (var lane in lanes)
            {
                half = math.max(half, math.abs(lane.Offset) + (lane.Width * 0.5f));
            }

            return half;
        }

        /// <summary>Vormt het terrein en geeft de gewijzigde hoekpunten (max exclusief), of een lege rect.</summary>
        public static RectInt Apply(WorldMap map, Bezier3 curve, float halfWidth)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var spacing = map.Dimensions.CellSize;
            var core = halfWidth + spacing;
            var reach = core + (ShoulderCells * spacing);
            var count = math.max(2, (int)math.ceil(curve.Length() / (spacing * 0.5f)) + 1);
            var samples = new float3[count];
            var min = new float2(float.MaxValue);
            var max = new float2(float.MinValue);
            for (var i = 0; i < count; i++)
            {
                samples[i] = curve.Position(i / (float)(count - 1));
                min = math.min(min, samples[i].xz);
                max = math.max(max, samples[i].xz);
            }

            ref var heights = ref map.Height;
            var layout = heights.Layout;
            var corners = Clip(min - reach, max + reach, spacing, layout.Width, layout.Depth);
            if (corners.width <= 0 || corners.height <= 0)
            {
                return default;
            }

            Blend(ref heights, samples, corners, core, reach, spacing);
            map.NotifyHeightChanged(corners);
            ClearCover(map, samples, halfWidth);
            return corners;
        }

        private static void Blend(ref CellLayer<float> heights, float3[] samples, RectInt corners, float core, float reach, float spacing)
        {
            var layout = heights.Layout;
            var weight = new float[corners.width * corners.height];
            var target = new float[weight.Length];
            foreach (var sample in samples)
            {
                var around = Clip(sample.xz - reach, sample.xz + reach, spacing, layout.Width, layout.Depth);
                for (var z = around.yMin; z < around.yMax; z++)
                {
                    for (var x = around.xMin; x < around.xMax; x++)
                    {
                        var w = Weight(math.distance(new float2(x, z) * spacing, sample.xz), core, reach);
                        var index = ((z - corners.yMin) * corners.width) + (x - corners.xMin);
                        if (w > weight[index])
                        {
                            weight[index] = w;
                            target[index] = sample.y - SurfaceClearance;
                        }
                    }
                }
            }

            for (var z = corners.yMin; z < corners.yMax; z++)
            {
                for (var x = corners.xMin; x < corners.xMax; x++)
                {
                    var index = ((z - corners.yMin) * corners.width) + (x - corners.xMin);
                    if (weight[index] > 0f)
                    {
                        heights[x, z] = math.clamp(math.lerp(heights[x, z], target[index], weight[index]), TerrainBrush.MinHeight, TerrainBrush.MaxHeight);
                    }
                }
            }
        }

        /// <summary>1 binnen <paramref name="core"/>, daarna zacht (smoothstep) naar 0 op <paramref name="reach"/>.</summary>
        public static float Weight(float distance, float core, float reach)
        {
            if (distance <= core)
            {
                return 1f;
            }

            return distance >= reach ? 0f : 1f - math.smoothstep(0f, 1f, (distance - core) / (reach - core));
        }

        private static void ClearCover(WorldMap map, float3[] samples, float halfWidth)
        {
            var cell = map.Dimensions.CellSize;
            var all = map.AllCells;
            var minX = int.MaxValue;
            var minZ = int.MaxValue;
            var maxX = int.MinValue;
            var maxZ = int.MinValue;
            foreach (var sample in samples)
            {
                var from = new int2((int)math.floor((sample.x - halfWidth) / cell), (int)math.floor((sample.z - halfWidth) / cell));
                var to = new int2((int)math.floor((sample.x + halfWidth) / cell), (int)math.floor((sample.z + halfWidth) / cell));
                for (var z = math.max(from.y, 0); z <= math.min(to.y, all.yMax - 1); z++)
                {
                    for (var x = math.max(from.x, 0); x <= math.min(to.x, all.xMax - 1); x++)
                    {
                        map.Trees[x, z] = 0;
                        map.GroundCover[x, z] = GroundCover.Built;
                        minX = math.min(minX, x);
                        minZ = math.min(minZ, z);
                        maxX = math.max(maxX, x);
                        maxZ = math.max(maxZ, z);
                    }
                }
            }

            if (minX > maxX)
            {
                return;
            }

            var changed = new RectInt(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);
            map.NotifyTreesChanged(changed);
            map.NotifyGroundCoverChanged(changed);
        }

        /// <summary>De hoekpunten waarvan de positie in <paramref name="min"/>..<paramref name="max"/> valt, binnen de laag.</summary>
        private static RectInt Clip(float2 min, float2 max, float spacing, int width, int depth)
        {
            var x0 = math.max(0, (int)math.ceil(min.x / spacing));
            var z0 = math.max(0, (int)math.ceil(min.y / spacing));
            var x1 = math.min(width - 1, (int)math.floor(max.x / spacing));
            var z1 = math.min(depth - 1, (int)math.floor(max.y / spacing));
            return new RectInt(x0, z0, math.max(0, x1 - x0 + 1), math.max(0, z1 - z0 + 1));
        }
    }
}
