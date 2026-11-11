using System;
using Unity.Mathematics;

namespace Cities.Simulation.Fields
{
    /// <summary>
    /// Pleinen en promenades (E25-09): een plein trekt voetgangers, een promenade met winkelstraten verhoogt de omzet van winkels
    /// langs de route. De bonus is begrensd zodat een stad vol pleinen niet te sterk wordt.
    /// </summary>
    public sealed class Promenade
    {
        public Promenade(float lengthMeters, int shopFronts, bool hasPlaza)
        {
            if (!(lengthMeters > 0f) || shopFronts < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lengthMeters), lengthMeters, "Een promenade heeft een lengte en geen negatieve winkelgevels.");
            }

            LengthMeters = lengthMeters;
            ShopFronts = shopFronts;
            HasPlaza = hasPlaza;
        }

        public float LengthMeters { get; }

        public int ShopFronts { get; }

        public bool HasPlaza { get; }

        /// <summary>Voetgangers per uur: 200 per 100 m, met plein anderhalf keer zoveel.</summary>
        public int PedestriansPerHour => (int)(LengthMeters / 100f * 200f * (HasPlaza ? 1.5f : 1f));

        /// <summary>Omzetbonus op winkels langs de route: 1 % per winkelgevel, hooguit 25 %, met plein 5 procentpunt erbij.</summary>
        public float ShopBonus => math.min(0.25f, ShopFronts * 0.01f) + (HasPlaza ? 0.05f : 0f);
    }
}
