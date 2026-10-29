using System;

namespace Cities.World.Grid
{
    /// <summary>Positie van een kaarttegel; tegels zijn de eenheid voor ontgrendelen (E03-09) en streamen (E23-07).</summary>
    public readonly struct TileCoord : IEquatable<TileCoord>
    {
        public TileCoord(int x, int z)
        {
            X = x;
            Z = z;
        }

        public int X { get; }

        public int Z { get; }

        public bool Equals(TileCoord other) => X == other.X && Z == other.Z;

        public override bool Equals(object obj) => obj is TileCoord other && Equals(other);

        public override int GetHashCode() => (X * 397) ^ Z;

        public override string ToString() => $"Tile({X}, {Z})";

        public static bool operator ==(TileCoord left, TileCoord right) => left.Equals(right);

        public static bool operator !=(TileCoord left, TileCoord right) => !left.Equals(right);
    }
}
