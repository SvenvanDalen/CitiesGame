using System;
using System.Collections.Generic;
using Cities.Core.Configuration;
using Cities.World.Grid;
using UnityEngine;

namespace Cities.World.Buildings
{
    /// <summary>
    /// Eigenschappen van een gebouwtype. Groeigebouwen hebben een zone; diensten en monumenten hebben
    /// <see cref="ZoneType.None"/> en worden met de hand geplaatst (E05-07).
    /// </summary>
    [Serializable]
    public struct BuildingSpec
    {
        public const int MaxLevel = 5;

        [Tooltip("Breedte langs de weg, in cellen van 8 m.")]
        public int FootprintWidth;

        [Tooltip("Diepte vanaf de weg, in cellen van 8 m (zones zijn tot 4 cellen diep).")]
        public int FootprintDepth;

        public ZoneType Zone;

        public ZoneDensity Density;

        [Tooltip("Ontwikkelingsniveau 1 tot 5 (E05-09).")]
        public int Level;

        [Tooltip("Aantal huishoudens (alleen wonen).")]
        public int Households;

        [Tooltip("Aantal arbeidsplaatsen.")]
        public int Workplaces;

        [Tooltip("Deel van de arbeidsplaatsen voor middelbaar opgeleiden, in procenten.")]
        public int MediumEducationPercent;

        [Tooltip("Deel van de arbeidsplaatsen voor hoger opgeleiden, in procenten; samen met middelbaar hoogstens 100.")]
        public int HighEducationPercent;

        [Tooltip("Wat de winkels in dit gebouw per dag kunnen verkopen, in euro (alleen winkels).")]
        public long ShopCapacity;

        [Tooltip("Goederen die dit gebouw per dag produceert, in eenheden (alleen industrie).")]
        public int Production;

        [Tooltip("Bouwkosten; voor groeigebouwen 0, die betaalt de ontwikkelaar.")]
        public long ConstructionCost;

        public long UpkeepPerMonth;

        [Tooltip("Bouwtijd in speldagen (E05-06).")]
        public int ConstructionDays;

        public bool IsGrowable => Zone != ZoneType.None;

        public int FootprintCells => FootprintWidth * FootprintDepth;

        /// <summary>
        /// Arbeidsplaatsen voor <paramref name="level"/>. De drie niveaus tellen altijd op tot <see cref="Workplaces"/>:
        /// hoog en middelbaar worden afgerond, de rest is laag opgeleid.
        /// </summary>
        public int WorkplacesFor(EducationLevel level)
        {
            var high = (int)Math.Round(Workplaces * HighEducationPercent / 100.0, MidpointRounding.AwayFromZero);
            var medium = Math.Min((int)Math.Round(Workplaces * MediumEducationPercent / 100.0, MidpointRounding.AwayFromZero), Workplaces - high);
            switch (level)
            {
                case EducationLevel.High:
                    return high;
                case EducationLevel.Medium:
                    return medium;
                default:
                    return Workplaces - high - medium;
            }
        }

        public void Validate(ICollection<string> errors)
        {
            SpecValidation.Range(errors, nameof(FootprintWidth), FootprintWidth, 1, 16);
            SpecValidation.Range(errors, nameof(FootprintDepth), FootprintDepth, 1, 16);
            SpecValidation.Range(errors, nameof(Level), Level, 1, MaxLevel);
            SpecValidation.Range(errors, nameof(Households), Households, 0, 5000);
            SpecValidation.Range(errors, nameof(Workplaces), Workplaces, 0, 20000);
            SpecValidation.Range(errors, nameof(ConstructionDays), ConstructionDays, 0, 3650);
            SpecValidation.Range(errors, nameof(MediumEducationPercent), MediumEducationPercent, 0, 100);
            SpecValidation.Range(errors, nameof(HighEducationPercent), HighEducationPercent, 0, 100);
            SpecValidation.Range(errors, nameof(Production), Production, 0, 1_000_000);
            SpecValidation.NotNegative(errors, nameof(ShopCapacity), ShopCapacity);
            SpecValidation.NotNegative(errors, nameof(ConstructionCost), ConstructionCost);
            SpecValidation.NotNegative(errors, nameof(UpkeepPerMonth), UpkeepPerMonth);

            ValidateHousingAndWork(errors);
            ValidateShopsAndProduction(errors);

            if (IsGrowable && ConstructionCost > 0)
            {
                errors.Add("Groeigebouwen kosten de stad niets om te bouwen.");
            }
        }

        private void ValidateHousingAndWork(ICollection<string> errors)
        {
            if (IsGrowable && FootprintDepth > 4)
            {
                errors.Add("Groeigebouwen zijn hoogstens 4 cellen diep, net als de zones.");
            }

            if (Zone == ZoneType.Residential && Households == 0)
            {
                errors.Add("Een woongebouw heeft minstens één huishouden.");
            }

            if (Zone != ZoneType.Residential && Households > 0)
            {
                errors.Add("Alleen woongebouwen hebben huishoudens.");
            }

            if ((Zone == ZoneType.Commercial || Zone == ZoneType.Industrial || Zone == ZoneType.Office) && Workplaces == 0)
            {
                errors.Add("Een gebouw voor werk heeft minstens één arbeidsplaats.");
            }

            if (MediumEducationPercent + HighEducationPercent > 100)
            {
                errors.Add("Middelbaar en hoger opgeleide banen samen zijn hoogstens 100 procent.");
            }
        }

        private void ValidateShopsAndProduction(ICollection<string> errors)
        {
            if (Zone == ZoneType.Commercial && ShopCapacity == 0)
            {
                errors.Add("Een winkelgebouw heeft winkelcapaciteit.");
            }

            if (Zone != ZoneType.Commercial && ShopCapacity > 0)
            {
                errors.Add("Alleen winkelgebouwen hebben winkelcapaciteit.");
            }

            if (Zone == ZoneType.Industrial && Production == 0)
            {
                errors.Add("Een industriegebouw produceert iets.");
            }

            if (Zone != ZoneType.Industrial && Production > 0)
            {
                errors.Add("Alleen industriegebouwen produceren goederen.");
            }
        }
    }
}
