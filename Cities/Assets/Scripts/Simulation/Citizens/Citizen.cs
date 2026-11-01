using System.Collections.Generic;
using System.Runtime.InteropServices;
using Cities.Core.Ids;

namespace Cities.Simulation.Citizens
{
    public enum AgeGroup
    {
        Child = 0,
        Adult = 1,
        Retired = 2,
    }

    /// <summary>
    /// Eén burger (E07-01) in 24 bytes, zodat honderdduizenden in een gewone array passen. Alles is data zonder verwijzingen: leeftijd in jaren,
    /// opleiding, gezondheid en geluk (0 tot 100), het huishouden en de woning en werkplek als gebouw-ID. <c>default</c> is een pasgeboren,
    /// laag opgeleide burger zonder huishouden, woning of werk.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Citizen
    {
        public const int MaxAge = 120;
        public const int MaxScore = 100;

        // shortcut: Group en Validate gebruiken de standaardgrenzen; LifeRules maakt ze instelbaar, upgrade als een stad eigen grenzen krijgt.
        public const int AdultAge = 18;
        public const int RetirementAge = 67;

        /// <summary>Opleiding als 0 (laag), 1 (middelbaar) of 2 (hoog); gelijk aan <c>EducationLevel</c> in de wereldlaag, die hier niet bereikbaar is.</summary>
        public const int MaxEducation = 2;

        /// <summary>Woning; <see cref="EntityId{TKind}.None"/> als de burger dakloos is.</summary>
        public EntityId<BuildingKind> Home;

        /// <summary>Werkplek; <see cref="EntityId{TKind}.None"/> bij een burger zonder werk.</summary>
        public EntityId<BuildingKind> Work;

        /// <summary>Huishouden-ID; 0 betekent geen huishouden.</summary>
        public uint Household;

        /// <summary>Leeftijd in hele jaren.</summary>
        public byte Age;

        public byte Education;

        /// <summary>Gezondheid van 0 (overlijdt bijna) tot 100.</summary>
        public byte Health;

        /// <summary>Geluk van 0 tot 100.</summary>
        public byte Happiness;

        public readonly bool HasHome => Home.IsValid;

        public readonly bool HasWork => Work.IsValid;

        public readonly bool HasHousehold => Household != 0;

        public readonly AgeGroup Group
        {
            get
            {
                if (Age < AdultAge)
                {
                    return AgeGroup.Child;
                }

                return Age >= RetirementAge ? AgeGroup.Retired : AgeGroup.Adult;
            }
        }

        /// <summary>Volwassen, in de leeftijd om te werken en zonder baan.</summary>
        public readonly bool IsJobSeeker => Group == AgeGroup.Adult && !HasWork;

        public readonly void Validate(ICollection<string> errors)
        {
            if (Age > MaxAge)
            {
                errors.Add($"Leeftijd {Age} is hoger dan {MaxAge}.");
            }

            if (Education > MaxEducation)
            {
                errors.Add($"Opleiding {Education} is hoger dan {MaxEducation}.");
            }

            if (Health > MaxScore)
            {
                errors.Add($"Gezondheid {Health} is hoger dan {MaxScore}.");
            }

            if (Happiness > MaxScore)
            {
                errors.Add($"Geluk {Happiness} is hoger dan {MaxScore}.");
            }

            if (HasWork && Group != AgeGroup.Adult)
            {
                errors.Add("Alleen volwassenen onder de pensioenleeftijd hebben werk.");
            }
        }
    }
}
