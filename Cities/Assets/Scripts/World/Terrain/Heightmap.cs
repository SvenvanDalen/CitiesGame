using System;
using Unity.Mathematics;

namespace Cities.World.Terrain
{
    /// <summary>
    /// Een ingelezen hoogtekaart (E03-12): een raster van hoogtes tussen 0 en 1 dat met <see cref="HeightScale"/> naar meters schaalt, met een
    /// <see cref="WaterLevel"/> waaronder het land onder water staat. Het raster begint op wereldpositie (0, 0) en elk punt ligt
    /// <see cref="CellSize"/> meter van het volgende. Tussen punten wordt bilineair geïnterpoleerd; buiten het raster geldt de randhoogte.
    /// </summary>
    public sealed class Heightmap : ITerrainHeight
    {
        private readonly float[] _samples;

        /// <param name="samples">Hoogtes van 0 tot 1, rij voor rij van zuid naar noord, <paramref name="width"/> per rij.</param>
        public Heightmap(float[] samples, int width, int depth, float cellSize, float heightScale, float waterLevel)
        {
            if (samples is null)
            {
                throw new ArgumentNullException(nameof(samples));
            }

            if (width < 2 || depth < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Een hoogtekaart is minstens 2×2 punten.");
            }

            if (samples.Length != width * depth)
            {
                throw new ArgumentException($"{samples.Length} waarden voor een kaart van {width}×{depth}.", nameof(samples));
            }

            if (!(cellSize > 0f) || !(heightScale > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Puntafstand en hoogteschaal moeten groter dan nul zijn.");
            }

            _samples = (float[])samples.Clone();
            Width = width;
            Depth = depth;
            CellSize = cellSize;
            HeightScale = heightScale;
            WaterLevel = waterLevel;
        }

        public int Width { get; }

        public int Depth { get; }

        public float CellSize { get; }

        /// <summary>Hoogte in meters bij een waarde van 1.</summary>
        public float HeightScale { get; }

        /// <summary>Hoogte in meters waaronder het land onder water staat.</summary>
        public float WaterLevel { get; }

        /// <summary>Breedte en diepte van de kaart in meters.</summary>
        public float2 Size => new float2((Width - 1) * CellSize, (Depth - 1) * CellSize);

        /// <summary>Leest 16-bit RAW (little-endian, zoals Unity en de meeste terreintools schrijven) als hoogtekaart.</summary>
        public static Heightmap FromRaw16(byte[] data, int width, int depth, float cellSize, float heightScale, float waterLevel)
        {
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (width < 2 || depth < 2 || data.Length != width * depth * 2)
            {
                throw new ArgumentException($"{data.Length} bytes passen niet bij een 16-bit kaart van {width}×{depth}.", nameof(data));
            }

            var samples = new float[width * depth];
            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] = (data[2 * i] | (data[(2 * i) + 1] << 8)) / 65535f;
            }

            return new Heightmap(samples, width, depth, cellSize, heightScale, waterLevel);
        }

        /// <summary>Leest 8-bit grijswaarden (bijvoorbeeld uit een PNG) als hoogtekaart.</summary>
        public static Heightmap FromGray8(byte[] gray, int width, int depth, float cellSize, float heightScale, float waterLevel)
        {
            if (gray is null)
            {
                throw new ArgumentNullException(nameof(gray));
            }

            if (width < 2 || depth < 2 || gray.Length != width * depth)
            {
                throw new ArgumentException($"{gray.Length} bytes passen niet bij een 8-bit kaart van {width}×{depth}.", nameof(gray));
            }

            var samples = new float[gray.Length];
            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] = gray[i] / 255f;
            }

            return new Heightmap(samples, width, depth, cellSize, heightScale, waterLevel);
        }

        /// <summary>De hoogte van het land in meters, zonder water.</summary>
        public float GroundHeight(float2 position)
        {
            var x = math.clamp(position.x / CellSize, 0f, Width - 1);
            var z = math.clamp(position.y / CellSize, 0f, Depth - 1);
            var x0 = (int)math.floor(x);
            var z0 = (int)math.floor(z);
            var x1 = math.min(x0 + 1, Width - 1);
            var z1 = math.min(z0 + 1, Depth - 1);
            var fx = x - x0;
            var fz = z - z0;
            var south = math.lerp(_samples[(z0 * Width) + x0], _samples[(z0 * Width) + x1], fx);
            var north = math.lerp(_samples[(z1 * Width) + x0], _samples[(z1 * Width) + x1], fx);
            return math.lerp(south, north, fz) * HeightScale;
        }

        public bool IsUnderWater(float2 position) => GroundHeight(position) < WaterLevel;

        /// <summary>De hoogte van het oppervlak: het land, of het water als dat hoger staat.</summary>
        public float SurfaceHeight(float2 position) => math.max(GroundHeight(position), WaterLevel);
    }
}
