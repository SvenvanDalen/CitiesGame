namespace Cities.Gameplay.Population
{
    /// <summary>Wat de uitleg nodig heeft om te kiezen welke hint de speler nu helpt (E26-15).</summary>
    public readonly struct HintInputs
    {
        public HintInputs(int buildings, int jobs, int population, int services, int transitStops, long balance, int roads = 1)
        {
            Buildings = buildings;
            Jobs = jobs;
            Population = population;
            Services = services;
            TransitStops = transitStops;
            Balance = balance;
            Roads = roads;
        }

        public int Buildings { get; }

        public int Jobs { get; }

        public int Population { get; }

        public int Services { get; }

        public int TransitStops { get; }

        public long Balance { get; }

        /// <summary>Aantal wegsegmenten; standaard 1 zodat oudere aanroepen de wegstap overslaan.</summary>
        public int Roads { get; }
    }

    /// <summary>
    /// Korte hints bij de eerste acties (E26-15): de belangrijkste volgende stap voor een beginnende speler, op volgorde van urgentie.
    /// Zodra de stap gezet is, verschijnt de volgende; is alles gedaan, dan zwijgt het spel.
    /// </summary>
    public static class CityHints
    {
        public const long LowBalance = 100_000;

        public const int ServicesFromPopulation = 150;

        public const int TransitFromPopulation = 600;

        /// <summary>De hint voor de huidige situatie, of een lege tekst als er niets te melden is.</summary>
        public static string Next(in HintInputs state)
        {
            if (state.Balance < LowBalance)
            {
                return "Je geld raakt op. Verhoog de belasting, of leen geld in het financiënpaneel.";
            }

            if (state.Buildings == 0 && state.Roads == 0)
            {
                return "Stap 1: klik op 'Wegen' (of druk op R), kies 'Rechte weg' en klik twee keer op het gras.";
            }

            if (state.Buildings == 0)
            {
                return "Stap 2: klik op 'Zones' (of druk op Z), kies 'Wonen' en teken een vlak langs de weg. Dan komen er huizen.";
            }

            if (state.Jobs == 0)
            {
                return "Zonder banen komt er niemand wonen: teken een industrie- of winkelzone.";
            }

            if (state.Population >= ServicesFromPopulation && state.Services == 0)
            {
                return "Zet een brandweer, politie of kliniek neer: diensten trekken nieuwe bewoners aan.";
            }

            if (state.Population >= TransitFromPopulation && state.TransitStops == 0)
            {
                return "Plaats bushaltes en maak er een lijn van in het OV-paneel: dat verhoogt de landwaarde.";
            }

            return string.Empty;
        }
    }
}
