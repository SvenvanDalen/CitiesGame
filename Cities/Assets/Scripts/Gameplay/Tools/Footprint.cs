using System;
using Cities.World.Grid;
using UnityEngine;

namespace Cities.Gameplay.Tools
{
    /// <summary>
    /// Rechthoek van cellen die een object op de kaart inneemt. <see cref="Width"/> loopt langs de lokale X-as en
    /// <see cref="Depth"/> langs de lokale Z-as; na een kwartslag wisselen ze in het wereldraster.
    /// </summary>
    public readonly struct Footprint : IEquatable<Footprint>
    {
        public Footprint(CellCoord origin, int width, int depth, int quarterTurns)
        {
            if (width < 1 || depth < 1)
            {
                throw new ArgumentOutOfRangeException(width < 1 ? nameof(width) : nameof(depth), "Minstens één cel per richting.");
            }

            Origin = origin;
            Width = width;
            Depth = depth;
            QuarterTurns = ((quarterTurns % 4) + 4) % 4;
        }

        /// <summary>De cel linksonder (zuidwest) in het wereldraster.</summary>
        public CellCoord Origin { get; }

        public int Width { get; }

        public int Depth { get; }

        /// <summary>Kwartslagen met de klok mee (van boven gezien), 0 tot 3.</summary>
        public int QuarterTurns { get; }

        public int SizeX => QuarterTurns % 2 == 0 ? Width : Depth;

        public int SizeZ => QuarterTurns % 2 == 0 ? Depth : Width;

        /// <summary>De cel rechtsboven (noordoost).</summary>
        public CellCoord Max => new CellCoord(Origin.X + SizeX - 1, Origin.Z + SizeZ - 1);

        /// <summary>Draaiing om de Y-as in graden, zoals Unity die gebruikt.</summary>
        public float YawDegrees => QuarterTurns * 90f;

        public bool Contains(CellCoord cell) =>
            cell.X >= Origin.X && cell.Z >= Origin.Z && cell.X <= Max.X && cell.Z <= Max.Z;

        public bool Overlaps(Footprint other) =>
            Origin.X <= other.Max.X && other.Origin.X <= Max.X && Origin.Z <= other.Max.Z && other.Origin.Z <= Max.Z;

        public bool IsInside(WorldDimensions dimensions) => dimensions.Contains(Origin) && dimensions.Contains(Max);

        /// <summary>Midden van de rechthoek in wereld-X en -Z.</summary>
        public Vector2 Center(WorldDimensions dimensions) =>
            new Vector2((Origin.X + (SizeX * 0.5f)) * dimensions.CellSize, (Origin.Z + (SizeZ * 0.5f)) * dimensions.CellSize);

        public bool Equals(Footprint other) =>
            Origin == other.Origin && Width == other.Width && Depth == other.Depth && QuarterTurns == other.QuarterTurns;

        public override bool Equals(object obj) => obj is Footprint other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Origin, Width, Depth, QuarterTurns);

        public override string ToString() => $"{Width}×{Depth} op {Origin}, {YawDegrees:0}°";

        public static bool operator ==(Footprint left, Footprint right) => left.Equals(right);

        public static bool operator !=(Footprint left, Footprint right) => !left.Equals(right);
    }
}
