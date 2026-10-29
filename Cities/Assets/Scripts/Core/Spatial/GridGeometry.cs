using System;
using Unity.Mathematics;

namespace Cities.Core.Spatial
{
    /// <summary>
    /// Indeling van een rechthoekig gebied in vierkante zoekcellen. Posities buiten het gebied vallen in de randcellen,
    /// zodat ook objecten net over de kaartrand vindbaar blijven.
    /// </summary>
    public readonly struct GridGeometry : IEquatable<GridGeometry>
    {
        public GridGeometry(float2 origin, float cellSize, int2 cellCount)
        {
            if (!(cellSize > 0f) || float.IsInfinity(cellSize))
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize, "Celgrootte moet een positief getal zijn.");
            }

            if (math.any(cellCount < 1) || (long)cellCount.x * cellCount.y > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(cellCount), cellCount, "Minstens één cel per richting en niet te veel cellen.");
            }

            Origin = origin;
            CellSize = cellSize;
            CellCount = cellCount;
        }

        /// <summary>Geometrie die een gebied van <paramref name="size"/> vanaf <paramref name="origin"/> dekt met cellen van ongeveer <paramref name="cellSize"/>.</summary>
        public static GridGeometry Covering(float2 origin, float2 size, float cellSize)
        {
            var count = (int2)math.ceil(size / cellSize);
            return new GridGeometry(origin, cellSize, math.max(count, 1));
        }

        public float2 Origin { get; }

        public float CellSize { get; }

        public int2 CellCount { get; }

        public int Length => CellCount.x * CellCount.y;

        public Aabb2 Bounds => new Aabb2(Origin, Origin + ((float2)CellCount * CellSize));

        /// <summary>De cel waarin een punt valt, vastgezet op de rand van het raster.</summary>
        public int2 CellOf(float2 position) =>
            math.clamp((int2)math.floor((position - Origin) / CellSize), 0, CellCount - 1);

        public int Index(int2 cell)
        {
            if (math.any(cell < 0) || math.any(cell >= CellCount))
            {
                throw new ArgumentOutOfRangeException(nameof(cell), cell, "Cel valt buiten het raster.");
            }

            return IndexUnchecked(cell);
        }

        /// <summary>Index zonder grenscontrole, voor cellen die al binnen het raster liggen (en in Burst-jobs).</summary>
        public int IndexUnchecked(int2 cell) => (cell.y * CellCount.x) + cell.x;

        public int IndexOf(float2 position) => IndexUnchecked(CellOf(position));

        /// <summary>Alle cellen die een rechthoek raakt, inclusief beide grenzen.</summary>
        public void CellRange(Aabb2 area, out int2 min, out int2 max)
        {
            min = CellOf(area.Min);
            max = CellOf(area.Max);
        }

        public Aabb2 CellBounds(int2 cell)
        {
            var min = Origin + ((float2)cell * CellSize);
            return new Aabb2(min, min + CellSize);
        }

        public bool Equals(GridGeometry other) =>
            math.all(Origin == other.Origin) && BitConverter.SingleToInt32Bits(CellSize) == BitConverter.SingleToInt32Bits(other.CellSize) &&
            math.all(CellCount == other.CellCount);

        public override bool Equals(object obj) => obj is GridGeometry other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Origin, CellSize, CellCount);

        public override string ToString() => $"{CellCount.x}×{CellCount.y} cellen van {CellSize} m vanaf ({Origin.x}, {Origin.y})";

        public static bool operator ==(GridGeometry left, GridGeometry right) => left.Equals(right);

        public static bool operator !=(GridGeometry left, GridGeometry right) => !left.Equals(right);
    }
}
