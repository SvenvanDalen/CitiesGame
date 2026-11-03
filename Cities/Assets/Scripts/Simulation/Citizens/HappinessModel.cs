using System;
using Unity.Mathematics;

namespace Cities.Simulation.Citizens
{
    /// <summary>
    /// Wat één burger van zijn omgeving ervaart (E07-10). Pollution, Noise en Crime lopen van 0 (geen) tot 1 (extreem); Services van 0 (geen
    /// dekking) tot 1 (volledig); <see cref="TaxShift"/> is <c>TaxPolicy.HappinessShift()</c> in punten (negatief bij hoge belasting);
    /// <see cref="CommuteMinutes"/> is de reistijd naar het werk.
    /// </summary>
    public readonly struct HappinessFactors
    {
        public HappinessFactors(float pollution, float noise, float crime, float services, float taxShift, float commuteMinutes)
        {
            Pollution = pollution;
            Noise = noise;
            Crime = crime;
            Services = services;
            TaxShift = taxShift;
            CommuteMinutes = commuteMinutes;
        }

        public float Pollution { get; }

        public float Noise { get; }

        public float Crime { get; }

        public float Services { get; }

        public float TaxShift { get; }

        public float CommuteMinutes { get; }
    }

    /// <summary>
    /// Geluksmodel (E07-10): het doelgeluk van een burger is een gewogen som van werk, belasting, vervuiling, geluid, veiligheid, diensten en
    /// reistijd (elk als tevredenheid van 0 tot 1). Het geluk in <see cref="Citizen.Happiness"/> schuift per dag hoogstens
    /// <see cref="MaxDailyChange"/> punten naar dat doel. Zonder eigen status: het geluk zit al in de burger en gaat dus mee in de save.
    /// </summary>
    public static class HappinessModel
    {
        public const int MaxDailyChange = 3;

        /// <summary>Reistijd tot waar niemand het erg vindt, en vanaf waar het onacceptabel is.</summary>
        public const float EasyCommuteMinutes = 10f;
        public const float UnbearableCommuteMinutes = 60f;

        /// <summary>Belastingschuif (punten) waarbij de belastingtevredenheid op nul staat.</summary>
        public const float TaxShiftFloor = -50f;

        // Som is 1; werk en diensten wegen het zwaarst.
        private const float WorkWeight = 0.25f;
        private const float ServicesWeight = 0.2f;
        private const float CrimeWeight = 0.15f;
        private const float PollutionWeight = 0.1f;
        private const float NoiseWeight = 0.1f;
        private const float CommuteWeight = 0.1f;
        private const float TaxWeight = 0.1f;

        /// <summary>Werkloze volwassenen zijn ontevreden; kinderen en gepensioneerden hebben geen baan nodig.</summary>
        public static float WorkSatisfaction(in Citizen citizen) => citizen.IsJobSeeker ? 0f : 1f;

        /// <summary>1 bij een korte reis, 0 bij een lange, lineair daartussen; wie niet werkt reist niet.</summary>
        public static float CommuteSatisfaction(in Citizen citizen, float minutes) =>
            !citizen.HasWork ? 1f : 1f - math.saturate((minutes - EasyCommuteMinutes) / (UnbearableCommuteMinutes - EasyCommuteMinutes));

        /// <summary>1 bij een normale belasting of lager, 0 bij een schuif van <see cref="TaxShiftFloor"/> of erger.</summary>
        public static float TaxSatisfaction(float taxShift) => math.saturate(1f + (math.min(taxShift, 0f) / -TaxShiftFloor));

        /// <summary>Het geluk (0 tot 100) waar deze burger naartoe beweegt.</summary>
        public static int Target(in Citizen citizen, in HappinessFactors factors)
        {
            var satisfaction = (WorkWeight * WorkSatisfaction(citizen))
                + (ServicesWeight * math.saturate(factors.Services))
                + (CrimeWeight * (1f - math.saturate(factors.Crime)))
                + (PollutionWeight * (1f - math.saturate(factors.Pollution)))
                + (NoiseWeight * (1f - math.saturate(factors.Noise)))
                + (CommuteWeight * CommuteSatisfaction(citizen, factors.CommuteMinutes))
                + (TaxWeight * TaxSatisfaction(factors.TaxShift));
            return (int)math.round(math.saturate(satisfaction) * Citizen.MaxScore);
        }

        /// <summary>Geluk na één dag: een stap van hoogstens <see cref="MaxDailyChange"/> naar het doel.</summary>
        public static byte Step(in Citizen citizen, in HappinessFactors factors) =>
            (byte)(citizen.Happiness + math.clamp(Target(citizen, factors) - citizen.Happiness, -MaxDailyChange, MaxDailyChange));

        /// <summary>Gemiddeld geluk van de stad (0 tot 100); 0 bij een lege stad.</summary>
        public static float Average(ReadOnlySpan<Citizen> citizens)
        {
            if (citizens.IsEmpty)
            {
                return 0f;
            }

            long sum = 0;
            foreach (var citizen in citizens)
            {
                sum += citizen.Happiness;
            }

            return (float)sum / citizens.Length;
        }
    }
}
