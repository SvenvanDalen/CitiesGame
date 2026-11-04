using System;
using Cities.Simulation.Policies;

namespace Cities.Simulation.Industry
{
    /// <summary>
    /// Koppelt industrietypes (E14-02) aan districtsbeleid (E13-06). Een beleidsmaatregel stuurt type <c>x</c> met het effect
    /// <c>industry.x</c>: factor 0 verbiedt het type in het district (de winning ligt stil), een factor boven 1 stimuleert het,
    /// zonder beleid is de factor 1. De factor schaalt opbrengst en vervuiling. Een nieuwe regel is zo alleen data.
    /// </summary>
    public static class IndustryPolicy
    {
        public const string EffectPrefix = "industry.";

        public static string EffectKey(IndustryKind kind) => EffectPrefix + kind.ToString().ToLowerInvariant();

        /// <summary>De beleidsfactor op een industrietype in een district; zonder beleidsboek is het 1.</summary>
        public static float Factor(PolicyBook policies, IndustryKind kind, int district) =>
            policies?.Multiplier(EffectKey(kind), district) ?? 1f;

        /// <summary>Mag dit type in het district draaien?</summary>
        public static bool Enabled(PolicyBook policies, IndustryKind kind, int district) => Factor(policies, kind, district) > 0f;

        /// <summary>Een functie voor <see cref="IndustryNetwork.Step(int, Func{ExtractionSite, float})"/> die per winning het district opzoekt.</summary>
        public static Func<ExtractionSite, float> FactorOf(PolicyBook policies, Func<ExtractionSite, int> districtOf)
        {
            if (districtOf is null)
            {
                throw new ArgumentNullException(nameof(districtOf));
            }

            return site => Factor(policies, site.Kind, districtOf(site));
        }
    }
}
