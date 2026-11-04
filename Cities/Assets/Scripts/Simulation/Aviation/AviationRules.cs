using System;

namespace Cities.Simulation.Aviation
{
    /// <summary>Soort vliegtuig (E09-09); bepaalt de benodigde baanlengte, het aantal stoelen en hoe lang een gate bezet blijft.</summary>
    public enum AircraftClass
    {
        /// <summary>Regionaal toestel (turboprop of kleine jet, rond 70 stoelen).</summary>
        Regional,

        /// <summary>Smalrompvliegtuig (rond 180 stoelen).</summary>
        Narrowbody,

        /// <summary>Breedrompvliegtuig (rond 350 stoelen).</summary>
        Widebody,
    }

    /// <summary>
    /// De rekenregels van de luchtvaart (E09-09): baanlengte per vliegtuigsoort, de omlooptijd van een gate, hoeveel bewegingen een baan aankan
    /// en hoeveel passagiers een vlucht meeneemt. Alle getallen zijn gebaseerd op gangbare luchthavenplanning.
    /// </summary>
    /// <remarks>
    /// shortcut: er is één baan per luchthaven en een vlucht is een gemiddelde in plaats van een individueel vliegtuig met een rooster; de wind,
    /// het weer en nachtvluchten spelen niet mee. Upgrade naar meerdere banen en losse vluchten zodra er een luchtverkeersmodel is.
    /// </remarks>
    public static class AviationRules
    {
        /// <summary>Kortste baan in meter waar nog een toestel op kan.</summary>
        public const float MinRunway = 1200f;

        /// <summary>Langste baan in meter.</summary>
        public const float MaxRunway = 4500f;

        /// <summary>Minimale afstand tussen twee luchthavens in meter.</summary>
        public const float MinAirportSpacing = 8000f;

        /// <summary>Het aantal gates dat een luchthaven maximaal heeft.</summary>
        public const int MaxGates = 60;

        /// <summary>Maximale terminalcapaciteit in passagiers per uur.</summary>
        public const float MaxTerminalCapacity = 100_000f;

        /// <summary>Aandeel bezette stoelen gemiddeld over een vlucht.</summary>
        public const float LoadFactor = 0.82f;

        /// <summary>Afstand in meter vanaf een baanuiteinde waarop een toestel nog laag genoeg vliegt om hoorbaar te zijn.</summary>
        public const float ApproachDistance = 3000f;

        /// <summary>Baanlengte in meter die een toestel nodig heeft om veilig op te stijgen en te landen (met marge).</summary>
        public static float RequiredRunway(AircraftClass kind) => kind switch
        {
            AircraftClass.Regional => 1400f,
            AircraftClass.Narrowbody => 2200f,
            AircraftClass.Widebody => 3000f,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Onbekende vliegtuigsoort."),
        };

        public static int Seats(AircraftClass kind) => kind switch
        {
            AircraftClass.Regional => 70,
            AircraftClass.Narrowbody => 180,
            AircraftClass.Widebody => 350,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Onbekende vliegtuigsoort."),
        };

        /// <summary>Passagiers per beweging (landing of start) gemiddeld.</summary>
        public static float PassengersPerMovement(AircraftClass kind) => Seats(kind) * LoadFactor;

        /// <summary>Seconden dat een toestel een gate bezet houdt: uitstappen, schoonmaken, tanken, bagage en instappen.</summary>
        public static float TurnaroundTime(AircraftClass kind) => kind switch
        {
            AircraftClass.Regional => 2100f,
            AircraftClass.Narrowbody => 3000f,
            AircraftClass.Widebody => 5400f,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Onbekende vliegtuigsoort."),
        };

        /// <summary>Seconden dat een beweging de baan bezet houdt, inclusief de turbulentieafstand die zwaardere toestellen vragen.</summary>
        public static float RunwayOccupancy(AircraftClass kind) => kind switch
        {
            AircraftClass.Regional => 60f,
            AircraftClass.Narrowbody => 75f,
            AircraftClass.Widebody => 100f,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Onbekende vliegtuigsoort."),
        };

        /// <summary>
        /// Hoe vaak de lawaaibron van een luchthaven meetelt: een breedrompvliegtuig maakt veel meer lawaai dan een turboprop
        /// (1, 2 en 4 maal is 0, 3 en 6 dB erbij).
        /// </summary>
        public static int NoiseMultiplicity(AircraftClass kind) => kind switch
        {
            AircraftClass.Regional => 1,
            AircraftClass.Narrowbody => 2,
            AircraftClass.Widebody => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Onbekende vliegtuigsoort."),
        };

        public static bool IsRunwayOk(float length) => length >= MinRunway && length <= MaxRunway;

        /// <summary>Of een toestel van deze soort op een baan van <paramref name="runwayLength"/> meter kan landen en opstijgen.</summary>
        public static bool CanOperate(AircraftClass kind, float runwayLength) => runwayLength >= RequiredRunway(kind);

        /// <summary>Gate-bewegingen (landingen plus starts) per uur voor een gate van deze soort.</summary>
        public static float GateMovementsPerHour(AircraftClass kind) => 2f * 3600f / TurnaroundTime(kind);
    }
}
