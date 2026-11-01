using System;
using System.Collections.Generic;
using Cities.Simulation.Timing;
using Cities.World.Terrain;

namespace Cities.Gameplay.NewCity
{
    public enum Difficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
    }

    /// <summary>
    /// De keuzes in het scherm Nieuwe stad (E22-02): naam, kaart (de seed van het terrein), moeilijkheid en startjaar. De moeilijkheid bepaalt het
    /// startgeld; de rest van de startvoorwaarden volgt uit de kaart en het startjaar. De plan levert de instellingen voor terrein en klok.
    /// </summary>
    public readonly struct NewCityPlan
    {
        public const int MaxNameLength = 40;
        public const int MinYear = 1900;
        public const int MaxYear = 2100;

        public NewCityPlan(string name, int seed, Difficulty difficulty, int startYear)
        {
            Name = name;
            Seed = seed;
            Difficulty = difficulty;
            StartYear = startYear;
        }

        public string Name { get; }

        /// <summary>Dezelfde seed geeft dezelfde kaart.</summary>
        public int Seed { get; }

        public Difficulty Difficulty { get; }

        public int StartYear { get; }

        /// <summary>Startgeld in euro: ruim bij makkelijk, krap bij moeilijk.</summary>
        public long StartMoney
        {
            get
            {
                switch (Difficulty)
                {
                    case Difficulty.Easy: return 2_000_000;
                    case Difficulty.Hard: return 500_000;
                    default: return 1_000_000;
                }
            }
        }

        /// <summary>De terreininstellingen van de gekozen kaart.</summary>
        public TerrainShapeSettings Terrain()
        {
            var settings = TerrainShapeSettings.Default;
            settings.Seed = Seed;
            return settings;
        }

        /// <summary>De klokinstellingen met het gekozen startjaar, begin januari.</summary>
        public SimulationClockSettings Clock()
        {
            var settings = SimulationClockSettings.Default;
            settings.StartYear = StartYear;
            settings.StartMonth = 1;
            settings.StartDay = 1;
            return settings;
        }

        public void Validate(ICollection<string> errors)
        {
            if (errors is null)
            {
                throw new ArgumentNullException(nameof(errors));
            }

            if (string.IsNullOrWhiteSpace(Name))
            {
                errors.Add("Geef de stad een naam.");
            }
            else if (Name.Trim().Length > MaxNameLength)
            {
                errors.Add($"De naam is hoogstens {MaxNameLength} tekens.");
            }

            if (StartYear < MinYear || StartYear > MaxYear)
            {
                errors.Add($"Het startjaar ligt tussen {MinYear} en {MaxYear}.");
            }

            if (!Enum.IsDefined(typeof(Difficulty), Difficulty))
            {
                errors.Add("Onbekende moeilijkheid.");
            }
        }
    }
}
