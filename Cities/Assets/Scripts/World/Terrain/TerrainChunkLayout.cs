using System;
using System.Collections.Generic;
using Cities.World.Grid;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.World.Terrain
{
    /// <summary>
    /// Deelt de hoogtekaart op in vierkante chunks van <see cref="ChunkCells"/> cellen voor de terreinmesh. Een tegel
    /// van 256 cellen telt 8 × 8 chunks; aangrenzende chunks (en tegels) delen hun randhoekpunten, zodat de mesh
    /// naadloos aansluit. Elke chunk heeft <see cref="LodCount"/> detailniveaus.
    /// </summary>
    public readonly struct TerrainChunkLayout
    {
        public const int ChunkCells = 32;
        public const int LodCount = 4;

        public TerrainChunkLayout(WorldDimensions dimensions)
        {
            if (dimensions.CellsPerTile % ChunkCells != 0)
            {
                throw new ArgumentException($"Cellen per tegel ({dimensions.CellsPerTile}) moet deelbaar zijn door {ChunkCells}.", nameof(dimensions));
            }

            Dimensions = dimensions;
        }

        public WorldDimensions Dimensions { get; }

        public int ChunksX => Dimensions.CellsX / ChunkCells;

        public int ChunksZ => Dimensions.CellsZ / ChunkCells;

        public int ChunkCount => ChunksX * ChunksZ;

        public int ChunksPerTile => Dimensions.CellsPerTile / ChunkCells;

        /// <summary>Zijde van een chunk in meters.</summary>
        public float ChunkSize => ChunkCells * Dimensions.CellSize;

        /// <summary>Aantal vlakjes per zijde op een detailniveau: 32, 16, 8 of 4.</summary>
        public static int Resolution(int lod)
        {
            if (lod < 0 || lod >= LodCount)
            {
                throw new ArgumentOutOfRangeException(nameof(lod), lod, $"Detailniveau loopt van 0 tot {LodCount - 1}.");
            }

            return ChunkCells >> lod;
        }

        public int Index(int chunkX, int chunkZ) => (chunkZ * ChunksX) + chunkX;

        public Vector2Int Coord(int index) => new Vector2Int(index % ChunksX, index / ChunksX);

        /// <summary>Hoekpunt (in de hoogtelaag) linksonder van de chunk.</summary>
        public Vector2Int OriginCorner(int index)
        {
            var coord = Coord(index);
            return new Vector2Int(coord.x * ChunkCells, coord.y * ChunkCells);
        }

        /// <summary>Wereldpositie (X, Z) van de hoek linksonder van de chunk.</summary>
        public Vector2 Origin(int index) => (Vector2)OriginCorner(index) * Dimensions.CellSize;

        /// <summary>
        /// Chunks waarvan de mesh verandert als de hoekpunten in <paramref name="corners"/> (inclusief min, exclusief max)
        /// een andere hoogte krijgen. Een rand van één hoekpunt telt mee, omdat de normalen ook naar de buren kijken.
        /// </summary>
        public void ChunksTouching(RectInt corners, List<int> result)
        {
            result.Clear();
            if (corners.width <= 0 || corners.height <= 0)
            {
                return;
            }

            // Chunk c bevat hoekpunten c*32 tot en met c*32+32. Geraakt zijn hoekpunten xMin-1 tot en met xMax (max is exclusief, plus één buur).
            var minX = Math.Max(0, FloorDiv(corners.xMin - 2, ChunkCells));
            var minZ = Math.Max(0, FloorDiv(corners.yMin - 2, ChunkCells));
            var maxX = Math.Min(ChunksX - 1, FloorDiv(corners.xMax, ChunkCells));
            var maxZ = Math.Min(ChunksZ - 1, FloorDiv(corners.yMax, ChunkCells));

            for (var z = minZ; z <= maxZ; z++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    result.Add(Index(x, z));
                }
            }
        }

        /// <summary>
        /// Detailniveau voor een afstand van de camera tot de chunk: 0 tot de eerste grens, dan één niveau grover per
        /// grens. De grenzen lopen op.
        /// </summary>
        public static int ChooseLod(float distance, IReadOnlyList<float> lodDistances)
        {
            var lod = 0;
            while (lod < lodDistances.Count && lod < LodCount - 1 && distance >= lodDistances[lod])
            {
                lod++;
            }

            return lod;
        }

        private static int FloorDiv(int value, int divisor) => (int)Math.Floor((double)value / divisor);

        /// <summary>
        /// Stap in hoekpunten (1, 2, 4 of 8) van de buren zuid, oost, noord en west op hun detailniveau uit
        /// <paramref name="lods"/>; 0 aan de rand van de kaart.
        /// </summary>
        public int4 NeighbourSteps(int index, IReadOnlyList<int> lods)
        {
            var coord = Coord(index);
            return new int4(
                coord.y > 0 ? 1 << lods[Index(coord.x, coord.y - 1)] : 0,
                coord.x < ChunksX - 1 ? 1 << lods[Index(coord.x + 1, coord.y)] : 0,
                coord.y < ChunksZ - 1 ? 1 << lods[Index(coord.x, coord.y + 1)] : 0,
                coord.x > 0 ? 1 << lods[Index(coord.x - 1, coord.y)] : 0);
        }

        /// <summary>Afstand van een punt tot het blok van de chunk (0 als het punt erin ligt).</summary>
        public float Distance(int index, Vector3 point, float minHeight, float maxHeight)
        {
            var origin = Origin(index);
            var size = ChunkSize;
            var dx = Math.Max(Math.Max(origin.x - point.x, 0f), point.x - (origin.x + size));
            var dz = Math.Max(Math.Max(origin.y - point.z, 0f), point.z - (origin.y + size));
            var dy = Math.Max(Math.Max(minHeight - point.y, 0f), point.y - maxHeight);
            return (float)Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        }
    }
}
