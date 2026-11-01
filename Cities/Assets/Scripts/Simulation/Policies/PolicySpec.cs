using System;
using System.Collections.Generic;
using Cities.Core.Configuration;
using UnityEngine;

namespace Cities.Simulation.Policies
{
    public enum PolicyScope : byte
    {
        City,
        District,
    }

    /// <summary>
    /// Eén effect van een beleidsmaatregel: een sleutel die een systeem opvraagt (<c>traffic.speed</c>, <c>tax.income</c>) en de
    /// factor die daarop komt (1 = geen verandering). Een nieuwe regel is zo alleen data.
    /// </summary>
    [Serializable]
    public struct PolicyEffect
    {
        public string Key;

        [Tooltip("Factor op de waarde van de sleutel; 1 = geen verandering.")]
        public float Multiplier;
    }

    /// <summary>Kosten, geldigheid en effecten van een beleidsmaatregel (E13); de systemen vragen een effect op via <see cref="PolicyBook.Multiplier"/>.</summary>
    [Serializable]
    public struct PolicySpec
    {
        public PolicyScope Scope;

        [Tooltip("Eenmalige kosten bij invoeren.")]
        public long ActivationCost;

        [Tooltip("Vaste kosten per maand zolang het beleid geldt.")]
        public long MonthlyCost;

        [Tooltip("Extra kosten per inwoner per maand binnen het gebied waar het beleid geldt.")]
        public float MonthlyCostPerCitizen;

        [Tooltip("Inwoners nodig om het beleid te ontgrendelen (E16); 0 = altijd beschikbaar.")]
        public int UnlockPopulation;

        public PolicyEffect[] Effects;

        /// <summary>Maandkosten voor een gebied met het opgegeven aantal inwoners, afgerond op hele munteenheden.</summary>
        public long MonthlyCostFor(int citizens)
        {
            if (citizens < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(citizens), citizens, "Aantal inwoners kan niet negatief zijn.");
            }

            return MonthlyCost + (long)Math.Round(MonthlyCostPerCitizen * citizens, MidpointRounding.AwayFromZero);
        }

        public void Validate(ICollection<string> errors)
        {
            SpecValidation.NotNegative(errors, nameof(ActivationCost), ActivationCost);
            SpecValidation.NotNegative(errors, nameof(MonthlyCost), MonthlyCost);
            SpecValidation.Range(errors, nameof(MonthlyCostPerCitizen), MonthlyCostPerCitizen, 0f, 100f);
            SpecValidation.Range(errors, nameof(UnlockPopulation), UnlockPopulation, 0, 10_000_000);
            if (Effects == null)
            {
                return;
            }

            foreach (var effect in Effects)
            {
                if (string.IsNullOrWhiteSpace(effect.Key))
                {
                    errors.Add("Een effect heeft geen sleutel.");
                }

                SpecValidation.Range(errors, nameof(PolicyEffect.Multiplier), effect.Multiplier, 0f, 10f);
            }
        }
    }
}
