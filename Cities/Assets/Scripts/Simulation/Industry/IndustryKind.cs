using System;

namespace Cities.Simulation.Industry
{
    /// <summary>De gespecialiseerde industrietypes (E14-02).</summary>
    public enum IndustryKind
    {
        Agriculture = 0,
        Forestry = 1,
        Ore = 2,
        Oil = 3,
    }

    /// <summary>Hoe de voorraad van een winning zich gedraagt.</summary>
    public enum StockBehaviour
    {
        /// <summary>Bodemvruchtbaarheid die in het seizoen afneemt en in de winter herstelt.</summary>
        Fertility,

        /// <summary>Staand hout dat logistisch aangroeit.</summary>
        Regrowing,

        /// <summary>Eindige voorraad zonder aanvulling.</summary>
        Finite,
    }

    /// <summary>Vaste eigenschappen van een industrietype: grondstof, eigen gebouwen en productie.</summary>
    public sealed class IndustryProfile
    {
        private static readonly IndustryProfile[] All =
        {
            new IndustryProfile(IndustryKind.Agriculture, "voedsel", "Boerderij", "Voedselfabriek", StockBehaviour.Fertility, 40f, 1f),
            new IndustryProfile(IndustryKind.Forestry, "hout", "Houtkap", "Zagerij", StockBehaviour.Regrowing, 12f, 1500f),
            new IndustryProfile(IndustryKind.Ore, "erts", "Mijn", "Smelter", StockBehaviour.Finite, 50f, 20000f),
            new IndustryProfile(IndustryKind.Oil, "olie", "Oliepomp", "Raffinaderij", StockBehaviour.Finite, 60f, 30000f),
        };

        private static readonly float[] Workers = { 6f, 8f, 15f, 10f };
        private static readonly float[] Pollution = { 0.02f, 0.05f, 0.35f, 0.5f };

        private IndustryProfile(IndustryKind kind, string good, string extractionBuilding, string processingBuilding,
            StockBehaviour behaviour, float baseOutput, float stockPerRichness)
        {
            Kind = kind;
            Good = good;
            ExtractionBuilding = extractionBuilding;
            ProcessingBuilding = processingBuilding;
            Behaviour = behaviour;
            BaseOutput = baseOutput;
            StockPerRichness = stockPerRichness;
        }

        public IndustryKind Kind { get; }

        /// <summary>De grondstof die de winning levert (naam van de <c>GoodsChain</c>).</summary>
        public string Good { get; }

        /// <summary>Het winningsgebouw (boerderij, houtkap, mijn, oliepomp).</summary>
        public string ExtractionBuilding { get; }

        /// <summary>Het verwerkingsgebouw (voedselfabriek, zagerij, smelter, raffinaderij).</summary>
        public string ProcessingBuilding { get; }

        public StockBehaviour Behaviour { get; }

        /// <summary>Opbrengst per week van een winning van niveau 1 op rijkdom 1.</summary>
        public float BaseOutput { get; }

        /// <summary>Voorraad per rijkdom-eenheid (ertsvoorraad, hout, 1 voor vruchtbaarheid).</summary>
        public float StockPerRichness { get; }

        public float WorkersPerLevel => Workers[(int)Kind];

        /// <summary>Vervuiling per geproduceerde eenheid.</summary>
        public float PollutionPerUnit => Pollution[(int)Kind];

        public bool Renewable => Behaviour != StockBehaviour.Finite;

        public static IndustryProfile For(IndustryKind kind)
        {
            var index = (int)kind;
            if (index < 0 || index >= All.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), "Onbekend industrietype.");
            }

            return All[index];
        }
    }
}
