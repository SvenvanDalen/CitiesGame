using System;
using Unity.Mathematics;

namespace Cities.Simulation.Utilities
{
    /// <summary>
    /// De grondwaterlaag onder een gebied (E06-12). Hij heeft een beperkte voorraad die door regen langzaam aanvult (<see cref="RechargePerDay"/>)
    /// en leegloopt als er meer wordt onttrokken dan aanvult. Industrie laat vervuiling in de laag sijpelen; de vervuilingsgraad (0 tot 1) mengt
    /// zich met de voorraad en herstelt maar heel langzaam (<see cref="CleanupPerDay"/>). Voor de kwaliteit van het opgepompte water zie
    /// <see cref="WaterQuality.Intake"/>.
    /// </summary>
    public sealed class Aquifer
    {
        /// <summary>Deel van de inhoud dat per dag door regen wordt aangevuld.</summary>
        public const float RechargePerDay = 0.002f;

        /// <summary>Deel van de vervuiling dat per dag vanzelf verdwijnt.</summary>
        public const float CleanupPerDay = 0.0005f;

        public Aquifer(float capacity, float level)
        {
            if (!(capacity > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "De waterlaag heeft een inhoud groter dan nul nodig.");
            }

            Capacity = capacity;
            Level = math.clamp(level, 0f, capacity);
        }

        /// <summary>Maximale voorraad in kubieke meter.</summary>
        public float Capacity { get; }

        public float Level { get; private set; }

        /// <summary>Vervuilingsgraad van 0 (schoon) tot 1 (volledig vervuild).</summary>
        public float Pollution { get; private set; }

        public float Fill => Level / Capacity;

        /// <summary>Pompt tot <paramref name="amount"/> kubieke meter op en geeft terug hoeveel er was.</summary>
        public float Extract(float amount)
        {
            if (!(amount >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "De onttrekking kan niet negatief zijn.");
            }

            var taken = math.min(amount, Level);
            Level -= taken;
            return taken;
        }

        /// <summary>
        /// Industrie laat <paramref name="amount"/> kubieke meter vervuild water in de laag lopen met vervuilingsgraad
        /// <paramref name="pollution"/>. De laag wordt daardoor niet voller dan zijn inhoud; de vervuiling mengt naar verhouding.
        /// </summary>
        public void Seep(float amount, float pollution)
        {
            if (!(amount >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "De hoeveelheid kan niet negatief zijn.");
            }

            var added = math.min(amount, Capacity - Level);
            var total = Level + added;
            if (total > 0f)
            {
                Pollution = math.clamp(((Pollution * Level) + (math.clamp(pollution, 0f, 1f) * added)) / total, 0f, 1f);
            }

            Level = total;
        }

        /// <summary>Eén dag: regen vult de laag aan en de vervuiling loopt een klein beetje terug.</summary>
        public void Step()
        {
            Level = math.min(Capacity, Level + (Capacity * RechargePerDay));
            Pollution *= 1f - CleanupPerDay;
        }
    }
}
