using System;

namespace Cities.Simulation.Economy
{
    /// <summary>Uitkomst van één week van een <see cref="Company"/>.</summary>
    public readonly struct CompanyWeek
    {
        public CompanyWeek(float sold, float revenue, float wages, float materials)
        {
            Sold = sold;
            Revenue = revenue;
            Wages = wages;
            Materials = materials;
        }

        /// <summary>Verkochte eenheden, gelijk aan de productie.</summary>
        public float Sold { get; }

        public float Revenue { get; }

        /// <summary>Betaalde lonen; die lopen door ook als er te weinig vraag is.</summary>
        public float Wages { get; }

        public float Materials { get; }

        public float Profit => Revenue - Wages - Materials;
    }

    /// <summary>
    /// Een bedrijf (E11-07): het heeft werknemers die samen een productiecapaciteit geven, produceert wat de vraag toelaat, betaalt lonen en
    /// grondstoffen en houdt winst of verlies over. Een bedrijf dat <see cref="BankruptWeeks"/> weken achter elkaar verlies maakt, sluit.
    /// shortcut: productie wordt direct verkocht, zonder voorraad en zonder prijsvorming, tot de goederenketens van M4 er zijn.
    /// </summary>
    public sealed class Company
    {
        /// <summary>Aantal weken achtereen verlies waarna het bedrijf sluit.</summary>
        public const int BankruptWeeks = 8;

        private readonly float _wagePerWorker;
        private readonly float _unitsPerWorker;
        private readonly float _price;
        private readonly float _unitCost;

        public Company(int workers, float wagePerWorker, float unitsPerWorker, float price, float unitCost, float cash)
        {
            if (workers < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(workers), workers, "Een bedrijf heeft geen negatief aantal werknemers.");
            }

            if (!(wagePerWorker >= 0f) || !(unitsPerWorker >= 0f) || !(price >= 0f) || !(unitCost >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(wagePerWorker), "Loon, productie, prijs en kosten kunnen niet negatief zijn.");
            }

            Workers = workers;
            _wagePerWorker = wagePerWorker;
            _unitsPerWorker = unitsPerWorker;
            _price = price;
            _unitCost = unitCost;
            Cash = cash;
        }

        public int Workers { get; }

        public float Cash { get; private set; }

        /// <summary>Aantal weken achter elkaar met verlies.</summary>
        public int LossWeeks { get; private set; }

        public bool IsClosed { get; private set; }

        /// <summary>Eenheden per week bij volle bezetting.</summary>
        public float Capacity => Workers * _unitsPerWorker;

        /// <summary>Eén week: produceert tot de capaciteit of de vraag, betaalt de lonen en boekt de winst. Een gesloten bedrijf doet niets.</summary>
        public CompanyWeek Step(float demand)
        {
            if (IsClosed)
            {
                return default;
            }

            var sold = Math.Min(Capacity, Math.Max(0f, demand));
            var week = new CompanyWeek(sold, sold * _price, Workers * _wagePerWorker, sold * _unitCost);
            Cash += week.Profit;
            LossWeeks = week.Profit < 0f ? LossWeeks + 1 : 0;
            IsClosed = LossWeeks >= BankruptWeeks;
            return week;
        }
    }
}
