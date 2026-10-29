using System;
using Unity.Collections;

namespace Cities.World.Grid
{
    /// <summary>
    /// Alle celgebaseerde lagen van de wereld. Eigenaar van het geheugen: roep <see cref="Dispose"/> aan bij het
    /// sluiten van een stad.
    /// </summary>
    /// <remarks>
    /// Resoluties zijn afgestemd op het gebruik. Hoogte en water liggen op de hoekpunten van de 8 m-cellen, zodat de
    /// terreinmesh (E03-01) en de watersimulatie (E03-05) er direct op passen. Zones staan per cel. Vervuiling en
    /// landwaarde veranderen geleidelijk over de kaart en gebruiken blokken van 4×4 cellen (32 m).
    /// </remarks>
    public sealed class WorldMap : IDisposable
    {
        /// <summary>Aantal cellen per richting dat één waarde van de grove lagen dekt.</summary>
        public const int CoarseCellsPerSample = 4;

        private CellLayer<float> _height;
        private CellLayer<float> _waterDepth;
        private CellLayer<ZoneCell> _zones;
        private CellLayer<float> _groundPollution;
        private CellLayer<float> _landValue;

        public WorldMap(WorldDimensions dimensions, float baseHeight = 0f)
        {
            if (dimensions.CellsPerTile % CoarseCellsPerSample != 0)
            {
                throw new ArgumentException(
                    $"Cellen per tegel ({dimensions.CellsPerTile}) moet deelbaar zijn door {CoarseCellsPerSample}.", nameof(dimensions));
            }

            Dimensions = dimensions;
            var corners = LayerLayout.ForCorners(dimensions);
            var coarse = LayerLayout.ForCells(dimensions, CoarseCellsPerSample);
            _height = new CellLayer<float>(corners, Allocator.Persistent, baseHeight);
            _waterDepth = new CellLayer<float>(corners, Allocator.Persistent);
            _zones = new CellLayer<ZoneCell>(LayerLayout.ForCells(dimensions), Allocator.Persistent);
            _groundPollution = new CellLayer<float>(coarse, Allocator.Persistent);
            _landValue = new CellLayer<float>(coarse, Allocator.Persistent);
        }

        public WorldDimensions Dimensions { get; }

        /// <summary>Terreinhoogte in meters op elk hoekpunt.</summary>
        public ref CellLayer<float> Height => ref _height;

        /// <summary>Waterdiepte boven het terrein in meters op elk hoekpunt; 0 is droog.</summary>
        public ref CellLayer<float> WaterDepth => ref _waterDepth;

        public ref CellLayer<ZoneCell> Zones => ref _zones;

        /// <summary>Grondvervuiling van 0 (schoon) tot 1 (zwaar vervuild), per blok van 32 m (E03-10, E12).</summary>
        public ref CellLayer<float> GroundPollution => ref _groundPollution;

        /// <summary>Landwaarde in munteenheden per m², per blok van 32 m (E12).</summary>
        public ref CellLayer<float> LandValue => ref _landValue;

        public bool IsDisposed { get; private set; }

        /// <summary>Totaal geheugen van alle lagen in bytes.</summary>
        public long MemoryBytes =>
            ((long)_height.Layout.Length * sizeof(float)) +
            ((long)_waterDepth.Layout.Length * sizeof(float)) +
            ((long)_zones.Layout.Length * 2) +
            ((long)_groundPollution.Layout.Length * sizeof(float)) +
            ((long)_landValue.Layout.Length * sizeof(float));

        /// <summary>Waterspiegel (terrein plus waterdiepte) op een hoekpunt.</summary>
        public float WaterSurface(int x, int z) => _height[x, z] + _waterDepth[x, z];

        /// <summary>Een cel staat onder water als een van de vier hoekpunten water heeft.</summary>
        public bool IsUnderwater(CellCoord cell, float minimumDepth = 0.05f) =>
            _waterDepth[cell.X, cell.Z] > minimumDepth ||
            _waterDepth[cell.X + 1, cell.Z] > minimumDepth ||
            _waterDepth[cell.X, cell.Z + 1] > minimumDepth ||
            _waterDepth[cell.X + 1, cell.Z + 1] > minimumDepth;

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            _height.Dispose();
            _waterDepth.Dispose();
            _zones.Dispose();
            _groundPollution.Dispose();
            _landValue.Dispose();
            IsDisposed = true;
        }
    }
}
