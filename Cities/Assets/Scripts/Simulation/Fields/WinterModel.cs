using System;
using Unity.Mathematics;

namespace Cities.Simulation.Fields
{
    /// <summary>
    /// Winter (E25-01): sneeuw op de weg, vorst, verwarmingsvraag en sneeuwruimen. Sneeuw hoopt op bij sneeuwval en vorst en smelt boven
    /// nul. Hoe meer sneeuw en vorst, hoe lager de wegligging (hooguit -40 %); ruimen en strooien halen de laag weg. De effecten zijn
    /// zacht zodat de winter hinderlijk maar nooit onspeelbaar is.
    /// </summary>
    public sealed class WinterModel
    {
        public const float MaxSnowDepth = 30f;

        public const float MeltPerDegreeHour = 0.4f;

        public const float PlowCmPerHour = 6f;

        public const float MaxGripLoss = 0.4f;

        public const float HeatingBaseline = 15f;

        /// <summary>Sneeuwdiepte in centimeter.</summary>
        public float SnowDepth { get; private set; }

        public float Temperature { get; private set; } = 10f;

        /// <summary>Aantal sneeuwschuiven dat nu ruimt.</summary>
        public int Plows { get; set; }

        /// <summary>Wegligging (0.6 tot 1): lager bij sneeuw en vorst.</summary>
        public float RoadGrip
        {
            get
            {
                var snow = SnowDepth / MaxSnowDepth;
                var ice = Temperature < 0f ? math.saturate(-Temperature / 10f) * 0.5f : 0f;
                return 1f - (MaxGripLoss * math.saturate(snow + ice));
            }
        }

        /// <summary>Verwarmingsvraag als factor op de basisvraag: 1 vanaf 15 graden, +4 % per graad eronder.</summary>
        public float HeatingDemand => 1f + (0.04f * math.max(0f, HeatingBaseline - Temperature));

        /// <summary>Eén uur: <paramref name="snowfallCm"/> valt erbij, de temperatuur bepaalt smelten, ruimen haalt weg.</summary>
        public void AdvanceHour(float temperature, float snowfallCm)
        {
            if (float.IsNaN(temperature) || !(snowfallCm >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(snowfallCm), snowfallCm, "Sneeuwval en temperatuur moeten een getal zijn.");
            }

            Temperature = temperature;
            var depth = SnowDepth + snowfallCm;
            if (temperature > 0f)
            {
                depth -= temperature * MeltPerDegreeHour;
            }

            depth -= Plows * PlowCmPerHour;
            SnowDepth = math.clamp(depth, 0f, MaxSnowDepth);
        }
    }
}
