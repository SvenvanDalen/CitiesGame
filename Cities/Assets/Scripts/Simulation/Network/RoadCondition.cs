using System;
using Unity.Mathematics;

namespace Cities.Simulation.Network
{
    /// <summary>
    /// De conditie van een wegsegment (E04-16), van 1 (nieuw) tot 0 (kapot). Verkeer slijt de weg: per dag daalt de conditie met het aantal
    /// voertuigen, zware voertuigen tellen meer mee (<see cref="HeavyWeight"/>). Onderhoud kost geld en een onderhoudsvoertuig en zet de conditie
    /// terug omhoog; een slechte weg is langzamer en wordt duurder om te herstellen. De basis van de maandelijkse kosten is
    /// <see cref="RoadTypeSpec.UpkeepPerMeterPerMonth"/>.
    /// </summary>
    public sealed class RoadCondition
    {
        /// <summary>Slijtage (deel van de conditie) per voertuig per dag.</summary>
        public const float WearPerVehicle = 0.00002f;

        /// <summary>Een zwaar voertuig (vrachtwagen, bus) slijt zoveel keer meer dan een personenauto.</summary>
        public const float HeavyWeight = 8f;

        /// <summary>Onder deze conditie vraagt de weg om onderhoud.</summary>
        public const float MaintenanceThreshold = 0.5f;

        /// <summary>Onder deze conditie rijdt het verkeer op zijn langzaamst.</summary>
        public const float MinSpeedFactor = 0.4f;

        /// <summary>Herstel (deel van de conditie) van één onderhoudsbeurt per dag.</summary>
        public const float RepairPerDay = 0.1f;

        public float Value { get; private set; } = 1f;

        public bool NeedsMaintenance => Value < MaintenanceThreshold;

        /// <summary>Factor op de maximumsnelheid van de weg: 1 bij een goede weg, tot <see cref="MinSpeedFactor"/> bij een kapotte.</summary>
        public float SpeedFactor => MinSpeedFactor + ((1f - MinSpeedFactor) * Value);

        /// <summary>
        /// Eén dag slijtage door <paramref name="cars"/> personenauto's en <paramref name="heavy"/> zware voertuigen. Een slechtere weg slijt
        /// niet sneller, maar de conditie kan nooit onder nul komen.
        /// </summary>
        public void Wear(float cars, float heavy)
        {
            if (!(cars >= 0f) || !(heavy >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(cars), "Aantallen voertuigen moeten nul of meer zijn.");
            }

            Value = math.max(0f, Value - ((cars + (heavy * HeavyWeight)) * WearPerVehicle));
        }

        /// <summary>
        /// Eén dag onderhoud met een onderhoudsvoertuig. Geeft de kosten in euro: <paramref name="lengthMetres"/> maal de maandelijkse basis van
        /// het wegtype gedeeld door dertig, maal <see cref="CostFactor"/> (hoe slechter de weg, hoe duurder het herstel).
        /// </summary>
        public long Repair(float lengthMetres, long upkeepPerMeterPerMonth)
        {
            if (!(lengthMetres >= 0f) || upkeepPerMeterPerMonth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lengthMetres), "Lengte en onderhoudskosten moeten nul of meer zijn.");
            }

            var repaired = math.min(RepairPerDay, 1f - Value);
            if (repaired <= 0f)
            {
                return 0L;
            }

            var cost = lengthMetres * upkeepPerMeterPerMonth / 30f * CostFactor;
            Value += repaired;
            return (long)Math.Round(cost);
        }

        /// <summary>Herstel kost 1 keer de basis bij een goede weg en tot 3 keer bij een kapotte.</summary>
        public float CostFactor => 1f + (2f * (1f - Value));
    }
}
