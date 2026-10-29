using System;

namespace Cities.World.Grid
{
    public enum ZoneType : byte
    {
        None = 0,
        Residential = 1,
        Commercial = 2,
        Industrial = 3,
        Office = 4,
    }

    public enum ZoneDensity : byte
    {
        Low = 0,
        High = 1,
    }

    /// <summary>Zone-informatie van één cel: twee bytes, zodat de hele kaart (ruim 5 miljoen cellen) ongeveer 10 MB kost.</summary>
    public readonly struct ZoneCell : IEquatable<ZoneCell>
    {
        public static readonly ZoneCell Empty = default;

        public ZoneCell(ZoneType type, ZoneDensity density)
        {
            Type = type;
            Density = type == ZoneType.None ? ZoneDensity.Low : density;
        }

        public ZoneType Type { get; }

        public ZoneDensity Density { get; }

        public bool IsZoned => Type != ZoneType.None;

        public bool Equals(ZoneCell other) => Type == other.Type && Density == other.Density;

        public override bool Equals(object obj) => obj is ZoneCell other && Equals(other);

        public override int GetHashCode() => ((int)Type << 8) | (int)Density;

        public override string ToString() => IsZoned ? $"{Type} ({Density})" : "None";

        public static bool operator ==(ZoneCell left, ZoneCell right) => left.Equals(right);

        public static bool operator !=(ZoneCell left, ZoneCell right) => !left.Equals(right);
    }
}
