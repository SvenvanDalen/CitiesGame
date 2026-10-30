using System;
using Cities.World.Grid;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.World.Terrain
{
    /// <summary>Vult (een deel van) de splatmap: één RGBA-pixel per cel met de gewichten van <see cref="TerrainSplat"/>.</summary>
    public static class TerrainSplatBuilder
    {
        /// <param name="cells">Cellen om bij te werken (min inclusief, max exclusief); wordt tot de kaart beperkt.</param>
        /// <param name="pixels">De hele splatmap, rij voor rij, <c>CellsX × CellsZ</c> pixels.</param>
        public static void Build(WorldMap map, in TerrainSplatSettings settings, RectInt cells, NativeArray<Color32> pixels)
        {
            var dimensions = map.Dimensions;
            if (pixels.Length != dimensions.CellCount)
            {
                throw new ArgumentException($"De splatmap heeft {dimensions.CellCount} pixels nodig, niet {pixels.Length}.", nameof(pixels));
            }

            cells.ClampToBounds(map.AllCells);
            if (cells.width <= 0 || cells.height <= 0)
            {
                return;
            }

            new TerrainSplatJob
            {
                Settings = settings,
                Heights = map.Height.Values,
                Water = map.WaterDepth.Values,
                Cover = map.GroundCover.Values,
                CornersWidth = map.Height.Layout.Width,
                CornersDepth = map.Height.Layout.Depth,
                CellsWidth = dimensions.CellsX,
                CellSize = dimensions.CellSize,
                Origin = new int2(cells.xMin, cells.yMin),
                Width = cells.width,
                Pixels = pixels,
            }.Schedule(cells.height, 16).Complete();
        }
    }

    [BurstCompile]
    internal struct TerrainSplatJob : IJobParallelFor
    {
        public TerrainSplatSettings Settings;
        [ReadOnly] public NativeArray<float> Heights;
        [ReadOnly] public NativeArray<float> Water;
        [ReadOnly] public NativeArray<GroundCover> Cover;
        public int CornersWidth;
        public int CellsWidth;
        public float CellSize;
        public int2 Origin;
        public int Width;
        public int CornersDepth;

        [NativeDisableParallelForRestriction]
        [WriteOnly] public NativeArray<Color32> Pixels;

        public void Execute(int index)
        {
            var z = Origin.y + index;
            for (var x = Origin.x; x < Origin.x + Width; x++)
            {
                var c00 = (z * CornersWidth) + x;
                var c01 = c00 + CornersWidth;
                var h00 = Heights[c00];
                var h10 = Heights[c00 + 1];
                var h01 = Heights[c01];
                var h11 = Heights[c01 + 1];
                var slope = TerrainSplat.SlopeDegrees(h00, h10, h01, h11, CellSize);
                var height = (h00 + h10 + h01 + h11) * 0.25f;
                var cell = (z * CellsWidth) + x;
                Pixels[cell] = TerrainSplat.Encode(TerrainSplat.Weights(slope, height, NearbyWater(x, z), Cover[cell], Settings));
            }
        }

        /// <summary>De meeste waterdiepte binnen twee hoekpunten, zodat de oever een zandrand krijgt.</summary>
        private float NearbyWater(int x, int z)
        {
            var water = 0f;
            for (var dz = -2; dz <= 2; dz++)
            {
                for (var dx = -2; dx <= 2; dx++)
                {
                    water = math.max(water, Water[Corner(x + dx, z + dz)]);
                }
            }

            return water;
        }

        private int Corner(int x, int z) => (math.clamp(z, 0, CornersDepth - 1) * CornersWidth) + math.clamp(x, 0, CornersWidth - 1);
    }
}
