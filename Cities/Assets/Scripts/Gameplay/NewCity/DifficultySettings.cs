using System;

namespace Cities.Gameplay.NewCity
{
    /// <summary>
    /// De spelregels die uit de moeilijkheid volgen (E11-05): startgeld, een kostenfactor op bouw en onderhoud, onbeperkt geld en alles ontgrendeld.
    /// De moeilijkheid geeft de basiswaarden; de laatste twee schakelaars zijn voor een zandbak en staan standaard uit.
    /// </summary>
    public readonly struct DifficultySettings
    {
        public DifficultySettings(long startMoney, float costFactor, bool unlimitedMoney, bool allUnlocked)
        {
            if (startMoney < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startMoney), startMoney, "Het startgeld kan niet negatief zijn.");
            }

            if (!(costFactor > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(costFactor), costFactor, "De kostenfactor is groter dan nul.");
            }

            StartMoney = startMoney;
            CostFactor = costFactor;
            UnlimitedMoney = unlimitedMoney;
            AllUnlocked = allUnlocked;
        }

        public long StartMoney { get; }

        /// <summary>Vermenigvuldiger op alle kosten: onder 1 goedkoper, boven 1 duurder.</summary>
        public float CostFactor { get; }

        /// <summary>Alles kost niets en het saldo blijft staan.</summary>
        public bool UnlimitedMoney { get; }

        /// <summary>Alle gebouwen, wegen en beleid zijn vanaf het begin beschikbaar.</summary>
        public bool AllUnlocked { get; }

        public static DifficultySettings For(Difficulty difficulty, bool unlimitedMoney = false, bool allUnlocked = false)
        {
            switch (difficulty)
            {
                case Difficulty.Easy: return new DifficultySettings(2_000_000, 0.75f, unlimitedMoney, allUnlocked);
                case Difficulty.Normal: return new DifficultySettings(1_000_000, 1f, unlimitedMoney, allUnlocked);
                case Difficulty.Hard: return new DifficultySettings(500_000, 1.25f, unlimitedMoney, allUnlocked);
                default: throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Onbekende moeilijkheid.");
            }
        }

        /// <summary>De kosten in euro na de factor, naar boven afgerond; nul bij onbeperkt geld.</summary>
        public long Cost(long baseCost)
        {
            if (baseCost < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseCost), baseCost, "De kosten kunnen niet negatief zijn.");
            }

            return UnlimitedMoney ? 0 : (long)Math.Ceiling(baseCost * (double)CostFactor);
        }

        /// <summary>Of het saldo genoeg is voor de kosten van een basisbedrag.</summary>
        public bool CanAfford(long balance, long baseCost) => UnlimitedMoney || balance >= Cost(baseCost);
    }
}
