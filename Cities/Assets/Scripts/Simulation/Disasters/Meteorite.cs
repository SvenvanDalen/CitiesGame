using System;
using Unity.Mathematics;

namespace Cities.Simulation.Disasters
{
    /// <summary>
    /// Meteorietinslag (E15-05). De doorsnede van de inslagkrater is <see cref="CraterFactor"/> keer die van de meteoriet. Binnen de krater
    /// is alles weg (schade 1). Daarbuiten neemt de schade van de schokgolf kwadratisch af tot nul op <see cref="BlastFactor"/> keer de
    /// kraterstraal, en de kans op brand tot nul op <see cref="FireFactor"/> keer de kraterstraal.
    /// </summary>
    public static class Meteorite
    {
        /// <summary>Kraterdoorsnede gedeeld door meteorietdoorsnede.</summary>
        public const float CraterFactor = 20f;

        public const float BlastFactor = 8f;

        public const float FireFactor = 4f;

        // shortcut: vaste kosten per dag; de schade per gebouw volgt uit DamageAt, koppelen aan het Ledger zodra gebouwen in de wereld staan.
        /// <summary>Een inslag komt zonder waarschuwing en is zeldzaam.</summary>
        public static DisasterSpec Spec { get; } = new DisasterSpec("Meteorietinslag", 0.0001f, 0, 1, 21, 150_000, 25_000);

        /// <summary>Straal van de krater in meter voor een meteoriet met doorsnede <paramref name="diameter"/> meter.</summary>
        public static float CraterRadius(float diameter)
        {
            if (!(diameter > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(diameter), diameter, "De meteoriet heeft een doorsnede groter dan nul nodig.");
            }

            return CraterFactor * diameter / 2f;
        }

        public static float BlastRadius(float diameter) => BlastFactor * CraterRadius(diameter);

        /// <summary>Schade van 0 (geen) tot 1 (weggevaagd) op <paramref name="distance"/> meter van het inslagpunt.</summary>
        public static float DamageAt(float diameter, float distance) => Falloff(CraterRadius(diameter), BlastRadius(diameter), distance);

        /// <summary>Kans van 0 tot 1 dat brandbaar materiaal op die afstand vlam vat door de hitte van de inslag.</summary>
        public static float FireChanceAt(float diameter, float distance) =>
            Falloff(CraterRadius(diameter), FireFactor * CraterRadius(diameter), distance);

        private static float Falloff(float inner, float outer, float distance)
        {
            var t = math.saturate((math.max(0f, distance) - inner) / (outer - inner));
            return (1f - t) * (1f - t);
        }
    }
}
