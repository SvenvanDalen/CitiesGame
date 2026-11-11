using System;

namespace Cities.Simulation.Freight
{
    /// <summary>
    /// Industrie met opslag (E25-05): een magazijn buffert goederen tussen productie en afname, zodat een keten niet stilvalt bij een
    /// tijdelijk tekort. Opslag is begrensd; overschot gaat verloren en wordt geteld.
    /// </summary>
    public sealed class Warehouse
    {
        public Warehouse(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Een magazijn heeft ruimte nodig.");
            }

            Capacity = capacity;
        }

        public int Capacity { get; }

        public int Stored { get; private set; }

        public int Lost { get; private set; }

        /// <summary>Slaat op; geeft wat er echt in paste.</summary>
        public int Store(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Een hoeveelheid is niet negatief.");
            }

            var fits = Math.Min(amount, Capacity - Stored);
            Stored += fits;
            Lost += amount - fits;
            return fits;
        }

        /// <summary>Neemt af; geeft wat er echt was.</summary>
        public int Take(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Een hoeveelheid is niet negatief.");
            }

            var taken = Math.Min(amount, Stored);
            Stored -= taken;
            return taken;
        }
    }
}
