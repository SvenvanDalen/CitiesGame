using System;
using Unity.Mathematics;

namespace Cities.Simulation.Outside
{
    public enum OutsideKind : byte
    {
        Highway = 0,
        Rail = 1,
        Harbour = 2,
        Airport = 3,
    }

    /// <summary>De kant van de kaart waar een buitenverbinding ligt.</summary>
    public enum MapEdge : byte
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3,
    }

    /// <summary>Wat één niveau van een soort buitenverbinding aankan, plus de kosten (E14-04).</summary>
    public readonly struct OutsideProfile
    {
        public OutsideProfile(int vehiclesPerHour, float tonnesPerDay, int passengersPerDay, long buildCost, long upkeepPerWeek)
        {
            VehiclesPerHour = vehiclesPerHour;
            TonnesPerDay = tonnesPerDay;
            PassengersPerDay = passengersPerDay;
            BuildCost = buildCost;
            UpkeepPerWeek = upkeepPerWeek;
        }

        /// <summary>Voertuigen per uur die over de weg binnenkomen; alleen snelwegen.</summary>
        public int VehiclesPerHour { get; }

        /// <summary>Ton goederen per dag, in elke richting.</summary>
        public float TonnesPerDay { get; }

        /// <summary>Reizigers per dag die niet met de auto komen (trein, schip, vliegtuig).</summary>
        public int PassengersPerDay { get; }

        public long BuildCost { get; }

        public long UpkeepPerWeek { get; }

        public static OutsideProfile For(OutsideKind kind)
        {
            switch (kind)
            {
                case OutsideKind.Highway:
                    return new OutsideProfile(3600, 4000f, 0, 2_000_000, 20_000);
                case OutsideKind.Rail:
                    return new OutsideProfile(0, 6000f, 8000, 3_000_000, 30_000);
                case OutsideKind.Harbour:
                    return new OutsideProfile(0, 20000f, 500, 5_000_000, 50_000);
                case OutsideKind.Airport:
                    return new OutsideProfile(0, 300f, 12000, 10_000_000, 100_000);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Onbekende soort buitenverbinding.");
            }
        }
    }

    /// <summary>Een aansluiting op de buitenwereld aan de kaartrand (E14-04). Een niveau vermenigvuldigt de capaciteit.</summary>
    public sealed class OutsideConnection
    {
        public const int MaxLevel = 3;

        internal OutsideConnection(int id, OutsideKind kind, MapEdge edge, float offset, int level, bool isOpen)
        {
            Id = id;
            Kind = kind;
            Edge = edge;
            Offset = offset;
            Level = level;
            IsOpen = isOpen;
        }

        public int Id { get; }

        public OutsideKind Kind { get; }

        public MapEdge Edge { get; }

        /// <summary>Meter langs de rand, vanaf de zuidwesthoek.</summary>
        public float Offset { get; }

        public int Level { get; internal set; }

        /// <summary>Een gesloten verbinding laat niets door en kost niets aan onderhoud.</summary>
        public bool IsOpen { get; internal set; }

        public OutsideProfile Profile => OutsideProfile.For(Kind);

        public int VehiclesPerHour => IsOpen ? Profile.VehiclesPerHour * Level : 0;

        public float TonnesPerDay => IsOpen ? Profile.TonnesPerDay * Level : 0f;

        public int PassengersPerDay => IsOpen ? Profile.PassengersPerDay * Level : 0;

        public long UpkeepPerWeek => IsOpen ? Profile.UpkeepPerWeek * Level : 0L;

        /// <summary>Plek op de kaart voor een kaart van <paramref name="mapSize"/> meter.</summary>
        public float3 PositionOn(float mapSize)
        {
            switch (Edge)
            {
                case MapEdge.North:
                    return new float3(Offset, 0f, mapSize);
                case MapEdge.East:
                    return new float3(mapSize, 0f, Offset);
                case MapEdge.South:
                    return new float3(Offset, 0f, 0f);
                default:
                    return new float3(0f, 0f, Offset);
            }
        }
    }
}
