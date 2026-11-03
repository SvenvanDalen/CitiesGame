using System;
using Cities.Simulation.Economy;
using Unity.Mathematics;

namespace Cities.Simulation.Fields
{
    /// <summary>
    /// Grondvervuiling (E12-05): per cel een concentratie die industrie en stortplaatsen opbouwen (de aanroeper telt de uitstoot met <see cref="Add"/> op)
    /// en die daarna bijna niet meer weggaat (<see cref="NaturalDecayPerDay"/>). Sanering (<see cref="TryStartCleanup"/>) kost geld naar de concentratie
    /// en haalt de cel daarna met <see cref="CleanupPerDay"/> per dag schoon. <see cref="Level"/> (0 tot 1) is de <c>Pollution</c> van <see cref="LandValueFactors"/>.
    /// De waarden zijn schattingen.
    /// </summary>
    public sealed class SoilPollution
    {
        /// <summary>Deel van de concentratie dat per dag vanzelf verdwijnt; grond herstelt in jaren, niet in weken.</summary>
        public const float NaturalDecayPerDay = 0.0002f;

        /// <summary>Concentratie die een sanering per dag weghaalt.</summary>
        public const float CleanupPerDay = 2f;

        /// <summary>Saneringskosten per eenheid concentratie.</summary>
        public const long CostPerUnit = 50;

        /// <summary>Concentratie waarbij het niveau 0,5 is.</summary>
        public const float HalfLevel = 100f;

        private readonly float[] _values;
        private readonly bool[] _cleaning;

        public SoilPollution(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "De kaart heeft een positieve breedte en hoogte nodig.");
            }

            Width = width;
            Height = height;
            _values = new float[width * height];
            _cleaning = new bool[width * height];
        }

        public int Width { get; }

        public int Height { get; }

        public float this[int x, int z] => _values[Index(x, z)];

        public bool IsCleaning(int x, int z) => _cleaning[Index(x, z)];

        /// <summary>Concentratie (0 of meer) naar vervuilingsniveau van 0 tot 1.</summary>
        public static float Level(float concentration)
        {
            var c = math.max(concentration, 0f);
            return c / (c + HalfLevel);
        }

        /// <summary>Kosten om de cel nu te saneren.</summary>
        public long CleanupCost(int x, int z) => (long)math.ceil(this[x, z] * CostPerUnit);

        public void Add(int x, int z, float amount)
        {
            if (!(amount >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "De uitstoot kan niet negatief zijn.");
            }

            _values[Index(x, z)] += amount;
        }

        /// <summary>Betaalt de sanering van een vervuilde cel en start hem; false als de cel schoon is, al wordt gesaneerd of het geld ontbreekt.</summary>
        public bool TryStartCleanup(int x, int z, Ledger ledger, int week)
        {
            if (ledger is null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            var i = Index(x, z);
            if (_values[i] <= 0f || _cleaning[i] || !ledger.TrySpend(week, LedgerCategory.Other, CleanupCost(x, z)))
            {
                return false;
            }

            _cleaning[i] = true;
            return true;
        }

        /// <summary>Eén dag: de grond herstelt een heel klein beetje en gesaneerde cellen gaan met vaste snelheid naar nul.</summary>
        public void Step()
        {
            for (var i = 0; i < _values.Length; i++)
            {
                _values[i] *= 1f - NaturalDecayPerDay;
                if (_cleaning[i])
                {
                    _values[i] = math.max(0f, _values[i] - CleanupPerDay);
                    _cleaning[i] = _values[i] > 0f;
                }
            }
        }

        private int Index(int x, int z)
        {
            if ((uint)x >= (uint)Width || (uint)z >= (uint)Height)
            {
                throw new ArgumentOutOfRangeException(nameof(x), "De cel ligt buiten de kaart.");
            }

            return (z * Width) + x;
        }
    }
}
