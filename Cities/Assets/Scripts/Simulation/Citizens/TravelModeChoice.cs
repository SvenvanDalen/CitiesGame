using Cities.Core.Randomness;
using Unity.Mathematics;

namespace Cities.Simulation.Citizens
{
    public enum TravelMode
    {
        Walk = 0,
        Bike = 1,
        Car = 2,
        Transit = 3,
    }

    /// <summary>Wat een burger bezit en wat een rit hem per modus kost. Reistijd in seconden, kosten in hele munteenheden; een negatieve tijd betekent niet beschikbaar (geen OV-verbinding).</summary>
    public readonly struct TripOptions
    {
        public TripOptions(float distance, bool ownsBike, bool ownsCar, float transitTime, long transitFare, long carCost)
        {
            Distance = math.max(distance, 0f);
            OwnsBike = ownsBike;
            OwnsCar = ownsCar;
            TransitTime = transitTime;
            TransitFare = transitFare;
            CarCost = carCost;
        }

        /// <summary>Afstand over de weg in meters.</summary>
        public float Distance { get; }

        public bool OwnsBike { get; }

        public bool OwnsCar { get; }

        /// <summary>Totale OV-reistijd (lopen, wachten, rijden, overstappen) uit <c>TransitRouter</c>; negatief als er geen verbinding is.</summary>
        public float TransitTime { get; }

        public long TransitFare { get; }

        /// <summary>Brandstof, slijtage en parkeren voor deze rit.</summary>
        public long CarCost { get; }
    }

    /// <summary>
    /// Vervoerskeuze (E08-09): een burger kiest lopen, fiets, auto of OV met de laagste gegeneraliseerde kosten (reistijd x tijdswaarde + geld).
    /// Fiets en auto vragen bezit, lopen en fietsen hebben een maximale afstand. Een kleine smaakfactor per rit uit de deterministische
    /// generator spreidt gelijke burgers over de modi. Zuivere functie zonder status; het bezit komt uit <see cref="TravelOwnership"/>.
    /// De snelheden en waarden zijn schattingen.
    /// </summary>
    public static class TravelModeChoice
    {
        public const float WalkSpeed = 1.4f;
        public const float BikeSpeed = 4.5f;
        public const float CarSpeed = 11f;
        public const float MaxWalkDistance = 2500f;
        public const float MaxBikeDistance = 10000f;

        /// <summary>Vaste tijd per rit voor fiets pakken en slot (bike) of parkeren en lopen naar de auto (car), in seconden.</summary>
        public const float BikeOverhead = 240f;
        public const float CarOverhead = 300f;

        /// <summary>Wat een seconde reistijd waard is, in munteenheden.</summary>
        public const float ValueOfTime = 0.005f;

        /// <summary>Spreiding van de smaakfactor: elke modus kost 1 tot 1 + deze waarde keer zoveel.</summary>
        public const float TasteSpread = 0.2f;

        /// <summary>Kiest de modus; <paramref name="random"/> levert de smaak en wordt per burger en tick vers aangeleverd.</summary>
        public static TravelMode Choose(in TripOptions trip, ref DeterministicRandom random)
        {
            var best = TravelMode.Walk;
            var bestCost = float.MaxValue;
            for (var i = 0; i < 4; i++)
            {
                var mode = (TravelMode)i;
                var cost = Cost(trip, mode);
                if (cost < 0f)
                {
                    continue;
                }

                cost *= 1f + (TasteSpread * random.NextFloat());
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = mode;
                }
            }

            return best;
        }

        /// <summary>Gegeneraliseerde kosten van een modus, of een negatief getal als de modus niet kan.</summary>
        public static float Cost(in TripOptions trip, TravelMode mode)
        {
            switch (mode)
            {
                case TravelMode.Walk:
                    return trip.Distance <= MaxWalkDistance ? trip.Distance / WalkSpeed * ValueOfTime : -1f;
                case TravelMode.Bike:
                    return trip.OwnsBike && trip.Distance <= MaxBikeDistance ? ((trip.Distance / BikeSpeed) + BikeOverhead) * ValueOfTime : -1f;
                case TravelMode.Car:
                    return trip.OwnsCar ? (((trip.Distance / CarSpeed) + CarOverhead) * ValueOfTime) + trip.CarCost : -1f;
                default:
                    return trip.TransitTime >= 0f ? (trip.TransitTime * ValueOfTime) + trip.TransitFare : -1f;
            }
        }
    }

    /// <summary>Wie een auto of fiets heeft (E08-09): een vaste uitkomst per huishouden en seed, met meer kans bij meer welvaart. Er is geen status om op te slaan.</summary>
    public static class TravelOwnership
    {
        public const float BaseCarChance = 0.2f;
        public const float WealthyCarChance = 0.9f;
        public const float BikeChance = 0.7f;

        /// <param name="household">Huishouden-ID.</param>
        /// <param name="wealth">Welvaart van het huishouden van 0 (arm) tot 1 (rijk).</param>
        /// <param name="seed">Wereldseed.</param>
        public static bool OwnsCar(uint household, float wealth, ulong seed)
        {
            var chance = math.lerp(BaseCarChance, WealthyCarChance, math.saturate(wealth));
            return new DeterministicRandom(seed, household).NextFloat() < chance;
        }

        public static bool OwnsBike(uint household, ulong seed) => new DeterministicRandom(seed ^ 0xB1CEUL, household).NextFloat() < BikeChance;
    }
}
