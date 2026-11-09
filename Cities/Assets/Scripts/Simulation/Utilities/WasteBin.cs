using System;
using Unity.Mathematics;

namespace Cities.Simulation.Utilities
{
    /// <summary>
    /// De afvalopslag van een gebouw (E06-09): het gebouw vult hem elke dag, bij <see cref="CallThreshold"/> vragen we een ophaalvoertuig aan
    /// en een voertuig leegt hem tot zijn laadvermogen. Wat niet meer past blijft als zwerfvuil liggen (<see cref="Overflow"/>).
    /// </summary>
    public sealed class WasteBin
    {
        /// <summary>Vulling (deel van de inhoud) waarbij een ophaalvoertuig wordt gevraagd.</summary>
        public const float CallThreshold = 0.8f;

        public WasteBin(float capacity)
        {
            if (!(capacity > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "De opslag heeft een inhoud groter dan nul nodig.");
            }

            Capacity = capacity;
        }

        public float Capacity { get; }

        // Met een kleine marge: 100 * 0,8f is op de ene runtime precies 80 en op de andere iets meer, en dan miste de bak zijn drempel net.
        private bool AtThreshold() => Level >= (Capacity * CallThreshold) - 1e-4f;

        public float Level { get; private set; }

        /// <summary>Totaal afval dat niet meer in de opslag paste; het vervuilt de omgeving.</summary>
        public float Overflow { get; private set; }

        /// <summary>True vanaf de drempel tot de bak weer onder de drempel is geleegd: er is een voertuig onderweg of nodig.</summary>
        public bool PickupRequested { get; private set; }

        /// <summary>Het gebouw produceert <paramref name="amount"/> afval.</summary>
        public void Produce(float amount)
        {
            if (!(amount >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Afvalproductie kan niet negatief zijn.");
            }

            var stored = math.min(amount, Capacity - Level);
            Level += stored;
            Overflow += amount - stored;
            PickupRequested |= AtThreshold();
        }

        /// <summary>Een voertuig met <paramref name="vehicleCapacity"/> ruimte haalt op en geeft terug hoeveel het meeneemt.</summary>
        public float Collect(float vehicleCapacity)
        {
            if (!(vehicleCapacity >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(vehicleCapacity), vehicleCapacity, "Het laadvermogen kan niet negatief zijn.");
            }

            var taken = math.min(Level, vehicleCapacity);
            Level -= taken;
            PickupRequested = AtThreshold();
            return taken;
        }
    }
}
