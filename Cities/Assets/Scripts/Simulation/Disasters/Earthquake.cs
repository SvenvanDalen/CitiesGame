using System;
using Unity.Mathematics;

namespace Cities.Simulation.Disasters
{
    /// <summary>Bouwvoorschrift waaronder een gebouw is neergezet; bepaalt hoeveel schudden het aankan.</summary>
    public enum BuildingCode : byte
    {
        None,
        Basic,
        Reinforced,
    }

    /// <summary>
    /// Aardbeving (E15-03). De intensiteit (op een schaal vergelijkbaar met magnitude) neemt met de afstand tot het epicentrum af.
    /// Elk gebouw heeft een weerstand uit zijn <see cref="BuildingCode"/>, verlaagd per niveau (hoger is slanker en kwetsbaarder).
    /// De schade loopt van 0 bij de weerstand tot 1 (instorting) twee intensiteitspunten erboven.
    /// </summary>
    public static class Earthquake
    {
        /// <summary>Intensiteitspunten tussen eerste schade en instorting.</summary>
        public const float CollapseRange = 2f;

        /// <summary>Weerstandsverlies per bouwniveau boven het eerste.</summary>
        public const float ResistanceLossPerLevel = 0.2f;

        // shortcut: vaste kosten per dag; schade per gebouw volgt uit Damage, koppelen aan het Ledger zodra gebouwen in de wereld staan.
        /// <summary>Aardbevingen komen zonder waarschuwing.</summary>
        public static DisasterSpec Spec { get; } = new DisasterSpec("Aardbeving", 0.0005f, 0, 1, 14, 100_000, 20_000);

        public static float ResistanceOf(BuildingCode code, int level)
        {
            if (level < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Het niveau begint bij 1.");
            }

            var baseResistance = code switch
            {
                BuildingCode.None => 4f,
                BuildingCode.Basic => 5f,
                BuildingCode.Reinforced => 6.5f,
                _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Onbekend bouwvoorschrift."),
            };
            return baseResistance - (ResistanceLossPerLevel * (level - 1));
        }

        /// <summary>Intensiteit op <paramref name="distance"/> meter van het epicentrum bij een bepaalde magnitude.</summary>
        public static float IntensityAt(float magnitude, float distance) =>
            math.max(0f, magnitude - (2f * math.log10(1f + (math.max(0f, distance) / 1000f))));

        /// <summary>Schade van 0 (geen) tot 1 (ingestort).</summary>
        public static float Damage(float intensity, BuildingCode code, int level) =>
            math.saturate((intensity - ResistanceOf(code, level)) / CollapseRange);

        public static bool Collapses(float intensity, BuildingCode code, int level) => Damage(intensity, code, level) >= 1f;
    }
}
