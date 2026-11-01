using System;
using DemandMeters = Cities.Simulation.Demand.Demand;

namespace Cities.Simulation.Economy
{
    /// <summary>Soort zone waarover belasting wordt geheven; los van de wereldlaag, die hierboven ligt.</summary>
    public enum TaxZone
    {
        Residential = 0,
        Commercial = 1,
        Industrial = 2,
        Office = 3,
    }

    public enum TaxDensity
    {
        Low = 0,
        High = 1,
    }

    /// <summary>
    /// De belastingen van de stad (E11-02): een tarief (0 tot <see cref="MaxRate"/>) per zone en dichtheid. De opbrengst gaat als inkomst naar
    /// het <see cref="Ledger"/>. Een tarief boven <see cref="DefaultRate"/> drukt de vraag, de migratie en het geluk; eronder helpt het.
    /// Alleen het gemiddelde tarief van een zone telt voor die effecten.
    /// </summary>
    public sealed class TaxPolicy
    {
        /// <summary>Het tarief waarbij belasting geen effect heeft op vraag, migratie of geluk.</summary>
        public const float DefaultRate = 0.09f;

        public const float MaxRate = 0.30f;

        /// <summary>Vraagverlies per 1,0 tarief boven het standaardtarief (10 procentpunt geeft 0,2 minder vraag).</summary>
        public const float DemandPerRate = 2f;

        /// <summary>Verandering van de migratiefactor per 1,0 tarief boven het standaardtarief.</summary>
        public const float MigrationPerRate = 3f;

        /// <summary>Geluk (0 tot 100) dat een inwoner per procentpunt woonbelasting boven het standaardtarief kwijtraakt.</summary>
        public const float HappinessPerRate = 100f;

        private const int Zones = 4;
        private const int Densities = 2;

        private readonly float[] _rates = new float[Zones * Densities];

        public TaxPolicy()
        {
            Array.Fill(_rates, DefaultRate);
        }

        public float Rate(TaxZone zone, TaxDensity density) => _rates[IndexOf(zone, density)];

        public void SetRate(TaxZone zone, TaxDensity density, float rate)
        {
            if (!(rate >= 0f && rate <= MaxRate))
            {
                throw new ArgumentOutOfRangeException(nameof(rate), rate, "Het tarief ligt tussen 0 en " + MaxRate + ".");
            }

            _rates[IndexOf(zone, density)] = rate;
        }

        /// <summary>Belasting over <paramref name="taxableValue"/> (afgerond op hele euro's) zonder te boeken.</summary>
        public long Revenue(TaxZone zone, TaxDensity density, long taxableValue)
        {
            if (taxableValue < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(taxableValue), taxableValue, "De belastbare waarde kan niet negatief zijn.");
            }

            return (long)Math.Round(taxableValue * (double)Rate(zone, density));
        }

        /// <summary>Int de belasting en boekt die in <paramref name="week"/> als inkomst; geeft het bedrag terug.</summary>
        public long Collect(Ledger ledger, int week, TaxZone zone, TaxDensity density, long taxableValue)
        {
            if (ledger is null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            var amount = Revenue(zone, density, taxableValue);
            ledger.Earn(week, LedgerCategory.Taxes, amount);
            return amount;
        }

        /// <summary>Hoe ver het gemiddelde tarief van de zone boven (+) of onder (-) het standaardtarief ligt.</summary>
        public float Deviation(TaxZone zone) =>
            ((_rates[IndexOf(zone, TaxDensity.Low)] + _rates[IndexOf(zone, TaxDensity.High)]) / Densities) - DefaultRate;

        /// <summary>
        /// De vraag na belasting: woonbelasting drukt de vraag naar woningen, bedrijfsbelasting (handel, industrie, kantoor) die naar werk en
        /// handelsbelasting die naar winkels. Het resultaat blijft tussen -1 en 1.
        /// </summary>
        public DemandMeters Apply(in DemandMeters demand)
        {
            var business = (Deviation(TaxZone.Commercial) + Deviation(TaxZone.Industrial) + Deviation(TaxZone.Office)) / 3f;
            return new DemandMeters(
                Shift(demand.Residential, Deviation(TaxZone.Residential)),
                Shift(demand.Work, business),
                Shift(demand.Commerce, Deviation(TaxZone.Commercial)));
        }

        /// <summary>Factor op de instroom van nieuwe inwoners: 1 bij het standaardtarief; bij het maximumtarief 0,37 en bij nul belasting 1,27.</summary>
        public float MigrationFactor() => 1f - (MigrationPerRate * Deviation(TaxZone.Residential));

        /// <summary>Punten geluk (negatief bij hoge woonbelasting) die elke inwoner erbij of eraf krijgt.</summary>
        public float HappinessShift() => -HappinessPerRate * Deviation(TaxZone.Residential);

        private static float Shift(float value, float deviation) => Math.Max(-1f, Math.Min(1f, value - (DemandPerRate * deviation)));

        private static int IndexOf(TaxZone zone, TaxDensity density)
        {
            if ((uint)zone >= Zones)
            {
                throw new ArgumentOutOfRangeException(nameof(zone), zone, "Onbekende zone.");
            }

            if ((uint)density >= Densities)
            {
                throw new ArgumentOutOfRangeException(nameof(density), density, "Onbekende dichtheid.");
            }

            return ((int)zone * Densities) + (int)density;
        }
    }
}
