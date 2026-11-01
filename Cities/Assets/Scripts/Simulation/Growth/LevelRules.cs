using System;

namespace Cities.Simulation.Growth
{
    /// <summary>Wat een kavel biedt, elk als waarde van 0 tot 1: landwaarde, dekking met diensten, opleidingsniveau van de buurt en vervuiling.</summary>
    public readonly struct LevelInputs
    {
        public LevelInputs(float landValue, float services, float education, float pollution)
        {
            LandValue = landValue;
            Services = services;
            Education = education;
            Pollution = pollution;
        }

        public float LandValue { get; }

        public float Services { get; }

        public float Education { get; }

        public float Pollution { get; }
    }

    /// <summary>
    /// De ontwikkelingsniveaus 1 tot 5 van een gebouw (E05-09). Elk niveau boven 1 vraagt meer landwaarde, diensten en opleiding en minder
    /// vervuiling. Een gebouw gaat per keer één niveau omhoog; het zakt pas als de kavel de eisen van zijn niveau met een marge mist, zodat
    /// een kavel op de grens niet heen en weer springt.
    /// </summary>
    public static class LevelRules
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 5;

        /// <summary>Hoeveel onder de eis van het huidige niveau een kavel mag zakken voordat het gebouw terugvalt.</summary>
        public const float DowngradeMargin = 0.1f;

        // De eis voor landwaarde, diensten en opleiding loopt van 0 (niveau 1) tot 0,8 (niveau 5); de vervuiling mag van 1 tot 0,2.
        public static float Required(int level) => (Math.Min(Math.Max(level, MinLevel), MaxLevel) - 1) * 0.2f;

        public static float MaxPollution(int level) => 1f - Required(level);

        /// <summary>Of de kavel de eisen van <paramref name="level"/> haalt, met <paramref name="margin"/> ruimte op elke eis.</summary>
        public static bool Meets(int level, in LevelInputs inputs, float margin = 0f) =>
            inputs.LandValue >= Required(level) - margin
            && inputs.Services >= Required(level) - margin
            && inputs.Education >= Required(level) - margin
            && inputs.Pollution <= MaxPollution(level) + margin;

        /// <summary>Het niveau dat dit gebouw nu zou moeten hebben: eentje hoger, gelijk of eentje lager.</summary>
        public static int Next(int current, in LevelInputs inputs)
        {
            current = Math.Min(Math.Max(current, MinLevel), MaxLevel);
            if (current < MaxLevel && Meets(current + 1, inputs))
            {
                return current + 1;
            }

            if (current > MinLevel && !Meets(current, inputs, DowngradeMargin))
            {
                return current - 1;
            }

            return current;
        }
    }
}
