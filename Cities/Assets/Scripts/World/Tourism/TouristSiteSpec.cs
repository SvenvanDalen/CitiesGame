using System;
using System.Collections.Generic;
using Cities.Core.Configuration;
using Cities.Simulation.TouristSites;
using UnityEngine;

namespace Cities.World.Tourism
{
    /// <summary>
    /// Eigenschappen van een toeristisch gebouw (E14-08): attractie, hotel, strand of wandelroute, met een aantrekkingskracht van 0 tot 100.
    /// De berekening staat in <see cref="TouristAppeal"/>.
    /// </summary>
    [Serializable]
    public struct TouristSiteSpec
    {
        public TouristSiteKind Kind;

        [Tooltip("Breedte langs de weg, in cellen van 8 m.")]
        public int FootprintWidth;

        [Tooltip("Diepte vanaf de weg, in cellen van 8 m.")]
        public int FootprintDepth;

        [Tooltip("Basisaantrekkingskracht van 0 tot 100, bij schone, stille omgeving.")]
        public float Attractiveness;

        [Tooltip("Hotelbedden; alleen een hotel heeft er.")]
        public int Beds;

        [Tooltip("Tot hoever in meters de aantrekkingskracht reikt.")]
        public float ReachMeters;

        public long ConstructionCost;

        public long UpkeepPerMonth;

        [Tooltip("Geluidsniveau van 0 tot 100 dat het gebouw zelf maakt.")]
        public float Noise;

        [Tooltip("Strand: moet aan water liggen.")]
        public bool NeedsWater;

        public TouristSiteProfile ToProfile() => new TouristSiteProfile(Kind, Attractiveness, Beds, ReachMeters);

        public void Validate(ICollection<string> errors)
        {
            SpecValidation.Range(errors, nameof(FootprintWidth), FootprintWidth, 1, 32);
            SpecValidation.Range(errors, nameof(FootprintDepth), FootprintDepth, 1, 32);
            SpecValidation.Range(errors, nameof(Attractiveness), Attractiveness, 0f, TouristSiteProfile.MaxAttractiveness);
            SpecValidation.Range(errors, nameof(Beds), Beds, 0, TouristSiteProfile.MaxBeds);
            SpecValidation.Range(errors, nameof(ReachMeters), ReachMeters, 1f, 20000f, " m");
            SpecValidation.Range(errors, nameof(Noise), Noise, 0f, 100f);
            SpecValidation.NotNegative(errors, nameof(ConstructionCost), ConstructionCost);
            SpecValidation.NotNegative(errors, nameof(UpkeepPerMonth), UpkeepPerMonth);

            if ((Kind == TouristSiteKind.Hotel) != (Beds > 0))
            {
                errors.Add("Alleen een hotel heeft bedden, en een hotel heeft er minstens één.");
            }

            if ((Kind == TouristSiteKind.Beach) != NeedsWater)
            {
                errors.Add("Alleen een strand ligt aan het water, en een strand moet dat doen.");
            }
        }
    }
}
