using System;

namespace Cities.Simulation.Network
{
    /// <summary>Wie een lane mag gebruiken.</summary>
    [Flags]
    public enum LaneTypes : byte
    {
        None = 0,
        Car = 1,
        Bus = 2,
        Bike = 4,
        Pedestrian = 8,

        /// <summary>Alles wat op de rijbaan rijdt; deze lanes sluiten op elkaar aan.</summary>
        Vehicles = Car | Bus,
    }

    /// <summary>Rijrichting ten opzichte van het segment (van begin- naar eindknooppunt).</summary>
    public enum LaneDirection : byte
    {
        Forward,
        Backward,

        /// <summary>Beide kanten op, zoals een stoep.</summary>
        Both,
    }

    /// <summary>
    /// Eén lane in het dwarsprofiel van een weg: wie erop mag, welke kant op, en waar hij ligt. Er wordt rechts
    /// gereden; heen-lanes liggen dus rechts van de hartlijn (positieve offset).
    /// </summary>
    public readonly struct LaneSpec : IEquatable<LaneSpec>
    {
        public LaneSpec(LaneTypes types, LaneDirection direction, float offset, float width)
        {
            if (types == LaneTypes.None)
            {
                throw new ArgumentException("Een lane is voor minstens één soort verkeer.", nameof(types));
            }

            if (width <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "De breedte moet positief zijn.");
            }

            Types = types;
            Direction = direction;
            Offset = offset;
            Width = width;
        }

        public LaneTypes Types { get; }

        public LaneDirection Direction { get; }

        /// <summary>Afstand van het midden van de lane tot de hartlijn, positief rechts in de richting van het segment.</summary>
        public float Offset { get; }

        public float Width { get; }

        public bool Equals(LaneSpec other) =>
            Types == other.Types && Direction == other.Direction && Offset.CompareTo(other.Offset) == 0 && Width.CompareTo(other.Width) == 0;

        public override bool Equals(object obj) => obj is LaneSpec other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Types, Direction, Offset, Width);

        public override string ToString() => $"{Types} {Direction} op {Offset:0.##} m, {Width:0.##} m breed";

        public static bool operator ==(LaneSpec left, LaneSpec right) => left.Equals(right);

        public static bool operator !=(LaneSpec left, LaneSpec right) => !left.Equals(right);
    }
}
