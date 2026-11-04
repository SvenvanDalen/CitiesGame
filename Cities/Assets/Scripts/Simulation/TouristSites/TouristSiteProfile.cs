using System;

namespace Cities.Simulation.TouristSites
{
    /// <summary>Soort toeristisch gebouw (E14-08).</summary>
    public enum TouristSiteKind
    {
        Attraction = 0,
        Hotel = 1,
        Beach = 2,
        WalkingRoute = 3,
    }

    /// <summary>
    /// De rekenwaarden van een toeristisch gebouw (E14-08), los van Unity zodat de simulatie ze kan gebruiken: de basisaantrekkingskracht
    /// (0 tot 100), het aantal hotelbedden en tot hoever (in meters) de aantrekkingskracht reikt.
    /// </summary>
    public readonly struct TouristSiteProfile
    {
        public const float MaxAttractiveness = 100f;
        public const int MaxBeds = 100_000;

        public TouristSiteProfile(TouristSiteKind kind, float attractiveness, int beds, float reachMeters)
        {
            if (!Enum.IsDefined(typeof(TouristSiteKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Onbekend soort toeristisch gebouw.");
            }

            if (!(attractiveness >= 0f && attractiveness <= MaxAttractiveness))
            {
                throw new ArgumentOutOfRangeException(nameof(attractiveness), attractiveness, "Aantrekkingskracht ligt tussen 0 en 100.");
            }

            if (beds < 0 || beds > MaxBeds)
            {
                throw new ArgumentOutOfRangeException(nameof(beds), beds, "Aantal bedden ligt tussen 0 en 100000.");
            }

            if (!(reachMeters > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(reachMeters), reachMeters, "Het bereik is positief.");
            }

            if ((kind == TouristSiteKind.Hotel) != (beds > 0))
            {
                throw new ArgumentException("Alleen een hotel heeft bedden, en een hotel heeft er minstens één.", nameof(beds));
            }

            Kind = kind;
            Attractiveness = attractiveness;
            Beds = beds;
            ReachMeters = reachMeters;
        }

        public TouristSiteKind Kind { get; }

        public float Attractiveness { get; }

        public int Beds { get; }

        public float ReachMeters { get; }
    }

    /// <summary>
    /// De omgeving van een toeristisch gebouw, elk van 0 tot 1: vervuiling en lawaai drukken de aantrekkingskracht, uitzicht (groen, water)
    /// helpt wandelroutes en zon helpt stranden.
    /// </summary>
    public readonly struct TouristSiteConditions
    {
        public TouristSiteConditions(float pollution, float noise, float scenery, float sunshine)
        {
            Pollution = Check(pollution, nameof(pollution));
            Noise = Check(noise, nameof(noise));
            Scenery = Check(scenery, nameof(scenery));
            Sunshine = Check(sunshine, nameof(sunshine));
        }

        /// <summary>Neutrale omgeving: schoon, stil, gemiddeld uitzicht en veel zon.</summary>
        public static TouristSiteConditions Neutral => new TouristSiteConditions(0f, 0f, 0.5f, 1f);

        public float Pollution { get; }

        public float Noise { get; }

        public float Scenery { get; }

        public float Sunshine { get; }

        private static float Check(float value, string name)
        {
            if (!(value >= 0f && value <= 1f))
            {
                throw new ArgumentOutOfRangeException(name, value, "Een omgevingswaarde ligt tussen 0 en 1.");
            }

            return value;
        }
    }

    /// <summary>Een geplaatst toeristisch gebouw in de stad.</summary>
    public readonly struct TouristSite
    {
        public TouristSite(int id, string definitionId, TouristSiteProfile profile, int x, int y, TouristSiteConditions conditions)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                throw new ArgumentException("Een toeristisch gebouw heeft een definitie nodig.", nameof(definitionId));
            }

            Id = id;
            DefinitionId = definitionId;
            Profile = profile;
            X = x;
            Y = y;
            Conditions = conditions;
        }

        public int Id { get; }

        public string DefinitionId { get; }

        public TouristSiteProfile Profile { get; }

        public int X { get; }

        public int Y { get; }

        public TouristSiteConditions Conditions { get; }

        public TouristSite WithConditions(TouristSiteConditions conditions) => new TouristSite(Id, DefinitionId, Profile, X, Y, conditions);
    }
}
