using System;
using System.Collections.Generic;

namespace Cities.Simulation.TouristSites
{
    /// <summary>Uitkomst van de aantrekkingskracht (E14-08): de score van 0 tot 100 en het aantal hotelbedden dat erbij hoort.</summary>
    public readonly struct TouristAppealResult
    {
        public TouristAppealResult(float score, int beds, int sites)
        {
            Score = score;
            Beds = beds;
            Sites = sites;
        }

        public float Score { get; }

        public int Beds { get; }

        public int Sites { get; }
    }

    /// <summary>
    /// Berekent de aantrekkingskracht van toeristische gebouwen (E14-08). Per gebouw telt de basiswaarde mee, gedempt door vervuiling en
    /// lawaai; een strand telt mee naar zonneschijn en een wandelroute naar uitzicht. Binnen één soort tellen de beste gebouwen het zwaarst
    /// (elk volgend nog <see cref="RankDecay"/> keer zo veel), en meer soorten samen geven een bonus. De som loopt via een verzadigende curve
    /// naar 100, zodat de honderdste attractie weinig toevoegt.
    /// </summary>
    public static class TouristAppeal
    {
        public const float RankDecay = 0.6f;
        public const float VarietyBonusPerKind = 0.1f;
        public const float SaturationScale = 60f;
        public const float MaxPollutionPenalty = 0.7f;
        public const float MaxNoisePenalty = 0.5f;
        public const float WorstBeachFactor = 0.3f;
        public const float WorstRouteFactor = 0.6f;

        /// <summary>Aantrekkingskracht van één gebouw in zijn omgeving, van 0 tot de basiswaarde.</summary>
        public static float Effective(TouristSiteProfile profile, TouristSiteConditions conditions)
        {
            var value = profile.Attractiveness
                * (1f - (MaxPollutionPenalty * conditions.Pollution))
                * (1f - (MaxNoisePenalty * conditions.Noise));
            switch (profile.Kind)
            {
                case TouristSiteKind.Beach:
                    value *= WorstBeachFactor + ((1f - WorstBeachFactor) * conditions.Sunshine);
                    break;
                case TouristSiteKind.WalkingRoute:
                    value *= WorstRouteFactor + ((1f - WorstRouteFactor) * conditions.Scenery);
                    break;
            }

            return value;
        }

        /// <summary>Aantrekkingskracht van de hele stad, zonder afstand.</summary>
        public static TouristAppealResult ForCity(IEnumerable<TouristSite> sites)
        {
            if (sites == null)
            {
                throw new ArgumentNullException(nameof(sites));
            }

            var perKind = NewBuckets();
            var beds = 0;
            var count = 0;
            foreach (var site in sites)
            {
                Add(perKind, site.Profile.Kind, Effective(site.Profile, site.Conditions));
                beds += site.Profile.Beds;
                count++;
            }

            return new TouristAppealResult(Combine(perKind), beds, count);
        }

        /// <summary>
        /// Aantrekkingskracht op een plek: elk gebouw telt mee voor zover het binnen zijn bereik ligt, lineair afnemend met de afstand
        /// (in meters, <paramref name="cellSize"/> per rastercel).
        /// </summary>
        public static TouristAppealResult At(IEnumerable<TouristSite> sites, int x, int y, float cellSize)
        {
            if (sites == null)
            {
                throw new ArgumentNullException(nameof(sites));
            }

            if (!(cellSize > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize, "Een cel heeft een positieve maat.");
            }

            var perKind = NewBuckets();
            var beds = 0;
            var count = 0;
            foreach (var site in sites)
            {
                var dx = (site.X - x) * cellSize;
                var dy = (site.Y - y) * cellSize;
                var falloff = 1f - (MathF.Sqrt((dx * dx) + (dy * dy)) / site.Profile.ReachMeters);
                if (falloff <= 0f)
                {
                    continue;
                }

                Add(perKind, site.Profile.Kind, Effective(site.Profile, site.Conditions) * falloff);
                beds += site.Profile.Beds;
                count++;
            }

            return new TouristAppealResult(Combine(perKind), beds, count);
        }

        private static List<float>[] NewBuckets() => new List<float>[Enum.GetValues(typeof(TouristSiteKind)).Length];

        private static void Add(List<float>[] perKind, TouristSiteKind kind, float value)
        {
            var index = (int)kind;
            if (perKind[index] == null)
            {
                perKind[index] = new List<float>();
            }

            perKind[index].Add(value);
        }

        private static float Combine(List<float>[] perKind)
        {
            var sum = 0f;
            var kinds = 0;
            foreach (var values in perKind)
            {
                if (values == null || values.Count == 0)
                {
                    continue;
                }

                kinds++;
                values.Sort((a, b) => b.CompareTo(a));
                var weight = 1f;
                foreach (var value in values)
                {
                    sum += value * weight;
                    weight *= RankDecay;
                }
            }

            if (kinds == 0)
            {
                return 0f;
            }

            var variety = 1f + (VarietyBonusPerKind * (kinds - 1));
            return TouristSiteProfile.MaxAttractiveness * (1f - MathF.Exp(-sum * variety / SaturationScale));
        }
    }
}
