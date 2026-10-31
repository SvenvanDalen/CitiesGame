using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Cities.Simulation.Utilities
{
    /// <summary>
    /// Wat een stroomtekort doet (E06-03): bij vraag boven aanbod valt een deel van de wijken helemaal uit (<see cref="Shed"/>),
    /// daar valt de verlichting weg (<see cref="StreetLight"/>) en verliezen gebouwen geluk (<see cref="Happiness"/>).
    /// </summary>
    public static class PowerShortage
    {
        public const float HappinessLossPerDay = 0.15f;
        public const float HappinessRecoveryPerDay = 0.05f;

        /// <summary>
        /// Kiest de wijken die uitvallen. Is het aanbod genoeg, dan niemand. Anders gaan hele wijken uit, vanaf het einde van de volgorde,
        /// tot de resterende vraag past. <paramref name="rotation"/> schuift het begin van de volgorde op, zodat de pijn eerlijk rouleert.
        /// </summary>
        // shortcut: een wijk valt altijd helemaal uit, ook als een deel van haar vraag nog te dekken was; verfijnen per straat als dat nodig blijkt.
        public static HashSet<int> Shed(float supply, IReadOnlyList<float> demandPerDistrict, int rotation)
        {
            if (demandPerDistrict == null)
            {
                throw new ArgumentNullException(nameof(demandPerDistrict));
            }

            var dark = new HashSet<int>();
            var count = demandPerDistrict.Count;
            var remaining = 0f;
            for (var i = 0; i < count; i++)
            {
                remaining += demandPerDistrict[i];
            }

            var start = count == 0 ? 0 : ((rotation % count) + count) % count;
            for (var step = count - 1; step >= 0 && remaining > supply; step--)
            {
                var district = (start + step) % count;
                dark.Add(district);
                remaining -= demandPerDistrict[district];
            }

            return dark;
        }

        /// <summary>Lichtsterkte van de straatverlichting van 0 tot 1: aan in het donker, uit bij stroomuitval of overdag.</summary>
        public static float StreetLight(bool districtDark, float timeOfDay)
        {
            return districtDark ? 0f : 1f - PowerCurves.Solar(timeOfDay);
        }

        /// <summary>Geluk (0 tot 1) na één dag: daalt in een donkere wijk, herstelt anders langzaam.</summary>
        public static float Happiness(float current, bool districtDark) =>
            math.clamp(current + (districtDark ? -HappinessLossPerDay : HappinessRecoveryPerDay), 0f, 1f);
    }
}
