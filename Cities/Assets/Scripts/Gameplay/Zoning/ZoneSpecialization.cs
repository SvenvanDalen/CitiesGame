using Cities.Simulation.Policies;

namespace Cities.Gameplay.Zoning
{
    /// <summary>
    /// Koppelt zone-specialisaties (E05-12: eco-woonwijk, IT-cluster, toeristisch winkelen) aan beleid. Een beleidsmaatregel
    /// schakelt specialisatie <c>x</c> in met een effect <c>zone.specialization.x</c> met factor boven 1; die factor is ook het
    /// gewicht waarmee de gespecialiseerde gebouwen kiezen tegen gewone gebouwen. Zo is een nieuwe specialisatie alleen data.
    /// </summary>
    public static class ZoneSpecialization
    {
        public const string EffectPrefix = "zone.specialization.";

        public static string EffectKey(string specialization) => EffectPrefix + specialization;

        /// <summary>
        /// Het gewicht van een gebouw met deze specialisatie in een district: 1 voor een gewoon gebouw (leeg), de beleidsfactor als
        /// die boven 1 ligt, anders 0 (niet ingeschakeld, of geen beleid).
        /// </summary>
        public static float Weight(PolicyBook policies, string specialization, int district)
        {
            if (string.IsNullOrEmpty(specialization))
            {
                return 1f;
            }

            var factor = policies?.Multiplier(EffectKey(specialization), district) ?? 0f;
            return factor > 1f ? factor : 0f;
        }
    }
}
