using System;
using UnityEngine;

namespace Cities.World.Grid
{
    /// <summary>Waar de waarden van een laag liggen.</summary>
    public enum LayerSampling : byte
    {
        /// <summary>Eén waarde per (eventueel grovere) cel, geldig voor het hele vlak; bijvoorbeeld zones of landwaarde.</summary>
        CellCentered,

        /// <summary>Eén waarde per hoekpunt van het celraster; bijvoorbeeld terreinhoogte en waterdiepte.</summary>
        Corners,
    }

    /// <summary>
    /// Geometrie van een laag: hoeveel waarden in X en Z, op welke onderlinge afstand, en hoe een wereldpositie
    /// op een waarde valt. Bevat geen data, zodat de rekenregels los van Unity te testen zijn.
    /// </summary>
    public readonly struct LayerLayout : IEquatable<LayerLayout>
    {
        private LayerLayout(int width, int depth, float spacing, int cellsPerSample, LayerSampling sampling)
        {
            Width = width;
            Depth = depth;
            Spacing = spacing;
            CellsPerSample = cellsPerSample;
            Sampling = sampling;
        }

        /// <summary>Aantal waarden in X-richting.</summary>
        public int Width { get; }

        /// <summary>Aantal waarden in Z-richting.</summary>
        public int Depth { get; }

        /// <summary>Afstand tussen twee waarden in meters.</summary>
        public float Spacing { get; }

        /// <summary>Hoeveel wereldcellen per richting één waarde dekt (1 bij volle resolutie).</summary>
        public int CellsPerSample { get; }

        public LayerSampling Sampling { get; }

        public int Length => Width * Depth;

        /// <summary>Laag met één waarde per blok van <paramref name="cellsPerSample"/>×<paramref name="cellsPerSample"/> cellen.</summary>
        public static LayerLayout ForCells(WorldDimensions world, int cellsPerSample = 1)
        {
            if (cellsPerSample < 1 || world.CellsPerTile % cellsPerSample != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cellsPerSample), cellsPerSample,
                    $"Moet een deler zijn van het aantal cellen per tegel ({world.CellsPerTile}).");
            }

            return new LayerLayout(world.CellsX / cellsPerSample, world.CellsZ / cellsPerSample,
                world.CellSize * cellsPerSample, cellsPerSample, LayerSampling.CellCentered);
        }

        /// <summary>Laag met een waarde op elk hoekpunt van het celraster, dus één meer dan het aantal cellen per richting.</summary>
        public static LayerLayout ForCorners(WorldDimensions world) =>
            new LayerLayout(world.CellsX + 1, world.CellsZ + 1, world.CellSize, 1, LayerSampling.Corners);

        public bool Contains(int x, int z) => x >= 0 && z >= 0 && x < Width && z < Depth;

        public int Index(int x, int z)
        {
            if (!Contains(x, z))
            {
                throw new ArgumentOutOfRangeException(nameof(x), $"({x}, {z}) valt buiten de laag van {Width}×{Depth}.");
            }

            return (z * Width) + x;
        }

        /// <summary>
        /// De waarde die een wereldcel dekt. Bij een hoekpuntlaag is dat het zuidwestelijke hoekpunt van de cel.
        /// </summary>
        public int IndexOfCell(CellCoord cell) => Index(FloorDiv(cell.X, CellsPerSample), FloorDiv(cell.Z, CellsPerSample));

        /// <summary>Wereldpositie (X en Z) van waarde (x, z).</summary>
        public Vector2 SamplePosition(int x, int z)
        {
            var offset = Sampling == LayerSampling.CellCentered ? 0.5f : 0f;
            return new Vector2((x + offset) * Spacing, (z + offset) * Spacing);
        }

        /// <summary>
        /// Bepaalt de vier waarden rond een wereldpositie en de gewichten voor bilineair interpoleren.
        /// Posities buiten de laag worden op de rand vastgezet.
        /// </summary>
        public BilinearSample GetBilinear(Vector2 position)
        {
            var offset = Sampling == LayerSampling.CellCentered ? 0.5f : 0f;
            var gx = Clamp((position.x / Spacing) - offset, 0f, Width - 1);
            var gz = Clamp((position.y / Spacing) - offset, 0f, Depth - 1);
            var x0 = Math.Min((int)gx, Math.Max(Width - 2, 0));
            var z0 = Math.Min((int)gz, Math.Max(Depth - 2, 0));
            var x1 = Math.Min(x0 + 1, Width - 1);
            var z1 = Math.Min(z0 + 1, Depth - 1);
            return new BilinearSample(Index(x0, z0), Index(x1, z0), Index(x0, z1), Index(x1, z1), gx - x0, gz - z0);
        }

        public bool Equals(LayerLayout other) =>
            Width == other.Width && Depth == other.Depth && Spacing.Equals(other.Spacing) &&
            CellsPerSample == other.CellsPerSample && Sampling == other.Sampling;

        public override bool Equals(object obj) => obj is LayerLayout other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Width, Depth, Spacing, CellsPerSample, Sampling);

        public override string ToString() => $"{Width}×{Depth} {Sampling} @ {Spacing} m";

        public static bool operator ==(LayerLayout left, LayerLayout right) => left.Equals(right);

        public static bool operator !=(LayerLayout left, LayerLayout right) => !left.Equals(right);

        private static float Clamp(float value, float min, float max) => value < min ? min : (value > max ? max : value);

        private static int FloorDiv(int value, int divisor) => (int)Math.Floor((double)value / divisor);
    }

    /// <summary>De vier indices en gewichten voor bilineaire interpolatie in een laag.</summary>
    public readonly struct BilinearSample
    {
        public BilinearSample(int index00, int index10, int index01, int index11, float tx, float tz)
        {
            Index00 = index00;
            Index10 = index10;
            Index01 = index01;
            Index11 = index11;
            Tx = tx;
            Tz = tz;
        }

        public int Index00 { get; }

        public int Index10 { get; }

        public int Index01 { get; }

        public int Index11 { get; }

        /// <summary>Gewicht richting X, van 0 (bij Index00) tot 1 (bij Index10).</summary>
        public float Tx { get; }

        /// <summary>Gewicht richting Z, van 0 (bij Index00) tot 1 (bij Index01).</summary>
        public float Tz { get; }

        public float Interpolate(float v00, float v10, float v01, float v11)
        {
            var south = v00 + ((v10 - v00) * Tx);
            var north = v01 + ((v11 - v01) * Tx);
            return south + ((north - south) * Tz);
        }
    }
}
