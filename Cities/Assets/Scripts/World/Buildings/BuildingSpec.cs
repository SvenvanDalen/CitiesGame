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

        [Tooltip("Bouwkosten; voor groeigebouwen 0, die betaalt de ontwikkelaar.")]
        public long ConstructionCost;

        public long UpkeepPerMonth;

        [Tooltip("Bouwtijd in speldagen (E05-06).")]
        public int ConstructionDays;

        public bool IsGrowable => Zone != ZoneType.None;

        public int FootprintCells => FootprintWidth * FootprintDepth;

        public void Validate(ICollection<string> errors)
        {
            SpecValidation.Range(errors, nameof(FootprintWidth), FootprintWidth, 1, 16);
            SpecValidation.Range(errors, nameof(FootprintDepth), FootprintDepth, 1, 16);
            SpecValidation.Range(errors, nameof(Level), Level, 1, MaxLevel);
            SpecValidation.Range(errors, nameof(Households), Households, 0, 5000);
            SpecValidation.Range(errors, nameof(Workplaces), Workplaces, 0, 20000);
            SpecValidation.Range(errors, nameof(ConstructionDays), ConstructionDays, 0, 3650);
            SpecValidation.NotNegative(errors, nameof(ConstructionCost), ConstructionCost);
            SpecValidation.NotNegative(errors, nameof(UpkeepPerMonth), UpkeepPerMonth);

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

            if (IsGrowable && ConstructionCost > 0)
            {
                errors.Add("Groeigebouwen kosten de stad niets om te bouwen.");
            }
        }
    }
}
