using System;
using UnityEngine;

namespace Cities.World.Grid
{
    /// <summary>
    /// Afmetingen van de wereld: een raster van tegels, elk opgedeeld in vierkante cellen. De wereld begint op
    /// wereldpositie (0, 0) en loopt in positieve X- en Z-richting. Alle lagen leiden hun grootte hiervan af.
    /// </summary>
    public readonly struct WorldDimensions : IEquatable<WorldDimensions>
    {
        /// <summary>Doelkaart uit het backlog: 9×9 tegels van 256 cellen van 8 m, ruim 2 km per tegel.</summary>
        public static readonly WorldDimensions Default = new WorldDimensions(9, 9, 256, 8f);

        public WorldDimensions(int tilesX, int tilesZ, int cellsPerTile, float cellSize)
        {
            if (tilesX < 1 || tilesZ < 1)
            {
                throw new ArgumentOutOfRangeException(tilesX < 1 ? nameof(tilesX) : nameof(tilesZ), "Minstens één tegel per richting.");
            }

            if (cellsPerTile < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(cellsPerTile), cellsPerTile, "Minstens één cel per tegel.");
            }

            if (!(cellSize > 0f) || float.IsInfinity(cellSize))
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize, "Celgrootte moet een positief getal zijn.");
            }

            if ((long)tilesX * cellsPerTile * tilesZ * cellsPerTile > int.MaxValue / 4)
            {
                throw new ArgumentOutOfRangeException(nameof(cellsPerTile), cellsPerTile, "Wereld heeft te veel cellen.");
            }

            TilesX = tilesX;
            TilesZ = tilesZ;
            CellsPerTile = cellsPerTile;
            CellSize = cellSize;
        }

        public int TilesX { get; }

        public int TilesZ { get; }

        public int CellsPerTile { get; }

        /// <summary>Zijde van een cel in meters.</summary>
        public float CellSize { get; }

        public int CellsX => TilesX * CellsPerTile;

        public int CellsZ => TilesZ * CellsPerTile;

        public int CellCount => CellsX * CellsZ;

        public int TileCount => TilesX * TilesZ;

        public float TileSize => CellsPerTile * CellSize;

        public float SizeX => CellsX * CellSize;

        public float SizeZ => CellsZ * CellSize;

        public bool Contains(CellCoord cell) => cell.X >= 0 && cell.Z >= 0 && cell.X < CellsX && cell.Z < CellsZ;

        public bool Contains(TileCoord tile) => tile.X >= 0 && tile.Z >= 0 && tile.X < TilesX && tile.Z < TilesZ;

        /// <summary>Of de wereldpositie (X en Z, in meters) binnen de kaart valt.</summary>
        public bool Contains(Vector2 position) =>
            position.x >= 0f && position.y >= 0f && position.x < SizeX && position.y < SizeZ;

        /// <summary>De cel onder een wereldpositie; posities buiten de kaart geven cellen buiten het bereik.</summary>
        public CellCoord WorldToCell(Vector2 position) =>
            new CellCoord((int)Math.Floor(position.x / CellSize), (int)Math.Floor(position.y / CellSize));

        public Vector2 CellCenter(CellCoord cell) =>
            new Vector2((cell.X + 0.5f) * CellSize, (cell.Z + 0.5f) * CellSize);

        public Vector2 CellMin(CellCoord cell) => new Vector2(cell.X * CellSize, cell.Z * CellSize);

        public TileCoord CellToTile(CellCoord cell) =>
            new TileCoord(FloorDiv(cell.X, CellsPerTile), FloorDiv(cell.Z, CellsPerTile));

        /// <summary>De cel linksonder (zuidwest) van een tegel.</summary>
        public CellCoord TileOrigin(TileCoord tile) => new CellCoord(tile.X * CellsPerTile, tile.Z * CellsPerTile);

        /// <summary>Rij-voor-rij index van een cel, voor het adresseren van lagen.</summary>
        public int CellIndex(CellCoord cell)
        {
            if (!Contains(cell))
            {
                throw new ArgumentOutOfRangeException(nameof(cell), cell, "Cel valt buiten de wereld.");
            }

            return (cell.Z * CellsX) + cell.X;
        }

        public CellCoord IndexToCell(int index)
        {
            if (index < 0 || index >= CellCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index valt buiten de wereld.");
            }

            return new CellCoord(index % CellsX, index / CellsX);
        }

        public bool Equals(WorldDimensions other) =>
            TilesX == other.TilesX && TilesZ == other.TilesZ && CellsPerTile == other.CellsPerTile && CellSize.Equals(other.CellSize);

        public override bool Equals(object obj) => obj is WorldDimensions other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(TilesX, TilesZ, CellsPerTile, CellSize);

        public override string ToString() =>
            $"{TilesX}×{TilesZ} tegels × {CellsPerTile}² cellen van {CellSize} m ({SizeX / 1000f:0.#}×{SizeZ / 1000f:0.#} km)";

        public static bool operator ==(WorldDimensions left, WorldDimensions right) => left.Equals(right);

        public static bool operator !=(WorldDimensions left, WorldDimensions right) => !left.Equals(right);

        private static int FloorDiv(int value, int divisor) => (int)Math.Floor((double)value / divisor);
    }
}
