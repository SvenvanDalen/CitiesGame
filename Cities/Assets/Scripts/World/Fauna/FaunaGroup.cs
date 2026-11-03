using Unity.Mathematics;

namespace Cities.World.Fauna
{
    public enum FaunaKind : byte
    {
        /// <summary>Zwerm vogels die boven de stad cirkelt (meeuwen boven water, kraaien boven land).</summary>
        Birds,

        /// <summary>School vissen in diep water.</summary>
        Fish,

        /// <summary>Kudde reeën op open land.</summary>
        Deer,
    }

    /// <summary>
    /// Een groep dieren met een vast ankerpunt. Alles aan de beweging volgt uit deze waarden en de tijd, dus de groep
    /// heeft geen toestand die bijgewerkt of opgeslagen hoeft te worden.
    /// </summary>
    public readonly struct FaunaGroup
    {
        public FaunaGroup(FaunaKind kind, ulong seed, float2 anchor, int count, float radius, float speed, float height, bool onWater)
        {
            Kind = kind;
            Seed = seed;
            Anchor = anchor;
            Count = count;
            Radius = radius;
            Speed = speed;
            Height = height;
            OnWater = onWater;
        }

        public FaunaKind Kind { get; }

        /// <summary>Bepaalt fase, richting en de eigenheden van elk dier.</summary>
        public ulong Seed { get; }

        /// <summary>Middelpunt van de beweging in wereldmeters (x, z).</summary>
        public float2 Anchor { get; }

        public int Count { get; }

        /// <summary>Straal van de baan of het weidegebied in meters.</summary>
        public float Radius { get; }

        /// <summary>Snelheid langs de baan in m/s.</summary>
        public float Speed { get; }

        /// <summary>Vogels: vlieghoogte boven de grond of het water in meters.</summary>
        public float Height { get; }

        /// <summary>Vogels: de zwerm hoort bij het water (meeuwen) in plaats van het land (kraaien).</summary>
        public bool OnWater { get; }
    }

    /// <summary>Stand van één dier op een tijdstip. Hoeken in radialen; gieren om de y-as, nul kijkt langs +z.</summary>
    public readonly struct FaunaPose
    {
        public FaunaPose(float3 position, float yaw, float pitch, float roll, float flap)
        {
            Position = position;
            Yaw = yaw;
            Pitch = pitch;
            Roll = roll;
            Flap = flap;
        }

        public float3 Position { get; }

        public float Yaw { get; }

        /// <summary>Positief is neus omhoog.</summary>
        public float Pitch { get; }

        /// <summary>Positief is kanteling naar rechts.</summary>
        public float Roll { get; }

        /// <summary>Vogels: vleugelstand in radialen, positief is de vleugeltoppen omhoog.</summary>
        public float Flap { get; }
    }
}
