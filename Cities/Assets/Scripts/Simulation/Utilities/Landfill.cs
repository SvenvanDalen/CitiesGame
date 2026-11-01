using System;
using Unity.Mathematics;

namespace Cities.Simulation.Utilities
{
    /// <summary>
    /// Een stortplaats met een eindige inhoud (E06-11). Hij neemt afval aan tot hij vol is en gaat dan dicht. Dichte stortplaatsen nemen niets
    /// meer aan, maar de bodem blijft vervuild: de restvervuiling loopt per dag een klein deel terug (<see cref="RecoveryPerDay"/>) en is
    /// nooit meteen weg.
    /// </summary>
    public sealed class Landfill
    {
        /// <summary>Deel van de restvervuiling dat per dag verdwijnt nadat de stortplaats dicht is.</summary>
        public const float RecoveryPerDay = 0.0005f;

        public Landfill(float capacity)
        {
            if (!(capacity > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "De stortplaats heeft een inhoud groter dan nul nodig.");
            }

            Capacity = capacity;
        }

        /// <summary>Inhoud in ton.</summary>
        public float Capacity { get; }

        public float Filled { get; private set; }

        public bool IsClosed { get; private set; }

        /// <summary>Vervuiling van de grond rond de stortplaats; groeit met wat erin gestort wordt en loopt na sluiting langzaam terug.</summary>
        public float GroundPollution { get; private set; }

        public float Fill => Filled / Capacity;

        /// <summary>
        /// Stort <paramref name="tonnes"/> met <paramref name="pollutionPerTonne"/> vervuiling per ton. Geeft terug hoeveel is aangenomen; bij een
        /// volle stortplaats gaat hij dicht en blijft de rest liggen.
        /// </summary>
        public float Dump(float tonnes, float pollutionPerTonne)
        {
            if (!(tonnes >= 0f) || !(pollutionPerTonne >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(tonnes), "Hoeveelheid en vervuiling moeten nul of meer zijn.");
            }

            if (IsClosed)
            {
                return 0f;
            }

            var accepted = math.min(tonnes, Capacity - Filled);
            Filled += accepted;
            GroundPollution += accepted * pollutionPerTonne;
            if (Filled >= Capacity)
            {
                IsClosed = true;
            }

            return accepted;
        }

        /// <summary>Eén dag: na sluiting verdwijnt een klein deel van de restvervuiling.</summary>
        public void Step()
        {
            if (IsClosed)
            {
                GroundPollution *= 1f - RecoveryPerDay;
            }
        }
    }
}
