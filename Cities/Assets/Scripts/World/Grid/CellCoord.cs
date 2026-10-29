using System;

namespace Cities.World.Grid
{
    /// <summary>Positie van een cel in het wereldraster; X loopt naar oost, Z naar noord.</summary>
    public readonly struct CellCoord : IEquatable<CellCoord>
    {
        public CellCoord(int x, int z)
        {
            X = x;
            Z = z;
        }

        public int X { get; }

        public int Z { get; }

        public CellCoord North => new CellCoord(X, Z + 1);

        public CellCoord South => new CellCoord(X, Z - 1);

        public CellCoord East => new CellCoord(X + 1, Z);

        public CellCoord West => new CellCoord(X - 1, Z);

        /// <summary>Manhattan-afstand, het aantal stappen via buurcellen zonder diagonalen.</summary>
        public int ManhattanDistance(CellCoord other) => Math.Abs(X - other.X) + Math.Abs(Z - other.Z);

        /// <summary>Chebyshev-afstand, het aantal stappen als diagonalen ook mogen.</summary>
        public int ChebyshevDistance(CellCoord other) => Math.Max(Math.Abs(X - other.X), Math.Abs(Z - other.Z));

        public bool Equals(CellCoord other) => X == other.X && Z == other.Z;

        public override bool Equals(object obj) => obj is CellCoord other && Equals(other);

        public override int GetHashCode() => (X * 73856093) ^ (Z * 19349663);

        public override string ToString() => $"({X}, {Z})";

        public static bool operator ==(CellCoord left, CellCoord right) => left.Equals(right);

        public static bool operator !=(CellCoord left, CellCoord right) => !left.Equals(right);
    }
}
