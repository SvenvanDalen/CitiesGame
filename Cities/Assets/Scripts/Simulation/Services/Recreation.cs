using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

namespace Cities.Simulation.Services
{
    public enum RecreationKind : byte
    {
        Park,
        Playground,
        SportsField,
        Plaza,
    }

    /// <summary>
    /// Een recreatieplek (E10-07): park, speeltuin, sportveld of plein. De werking neemt lineair af van het midden naar de rand van
    /// <see cref="Radius"/>. Onderhoud loopt via de gewone dienstkosten (<see cref="UpkeepPerMonth"/>), net als bij <see cref="ServiceStation"/>.
    /// </summary>
    public sealed class RecreationSite
    {
        public RecreationSite(RecreationKind kind, float2 position, float radius, long upkeepPerMonth)
        {
            if (!(radius > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "Het bereik moet groter dan nul zijn.");
            }

            if (upkeepPerMonth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(upkeepPerMonth), upkeepPerMonth, "De onderhoudskosten kunnen niet negatief zijn.");
            }

            Kind = kind;
            Position = position;
            Radius = radius;
            UpkeepPerMonth = upkeepPerMonth;
        }

        public RecreationKind Kind { get; }

        public float2 Position { get; }

        public float Radius { get; }

        public long UpkeepPerMonth { get; }

        /// <summary>Sterkte op een punt: 1 in het midden, 0 op de rand en daarbuiten.</summary>
        public float StrengthAt(float2 point) => math.saturate(1f - (math.distance(point, Position) / Radius));
    }

    /// <summary>
    /// Alle recreatieplekken van de stad (E10-07) met hun bonus op landwaarde en geluk. Per soort telt alleen de sterkste plek voor het
    /// geluk (tien parken naast elkaar maken je niet tien keer zo blij); voor de landwaarde stapelen plekken met afnemend rendement. De
    /// landwaardebonus is de <c>Parks</c>-factor van <c>LandValueFactors</c>. De bonussen zijn schattingen, af te stellen met speltesten.
    /// </summary>
    public sealed class RecreationMap
    {
        public const float ParkLandValue = 0.6f;
        public const float PlaygroundLandValue = 0.3f;
        public const float SportsFieldLandValue = 0.3f;
        public const float PlazaLandValue = 0.5f;

        public const float ParkHappiness = 0.15f;
        public const float PlaygroundHappiness = 0.1f;
        public const float SportsFieldHappiness = 0.1f;
        public const float PlazaHappiness = 0.08f;

        private readonly List<RecreationSite> _sites = new List<RecreationSite>();

        public IReadOnlyList<RecreationSite> Sites => _sites;

        public long UpkeepPerMonth => _sites.Sum(s => s.UpkeepPerMonth);

        public void Add(RecreationSite site) => _sites.Add(site ?? throw new ArgumentNullException(nameof(site)));

        public bool Remove(RecreationSite site) => _sites.Remove(site);

        /// <summary>De landwaardebonus (0 tot 1) op een punt.</summary>
        public float LandValueBonus(float2 point)
        {
            var left = 1f;
            foreach (var site in _sites)
            {
                left *= 1f - (site.StrengthAt(point) * LandValueOf(site.Kind));
            }

            return 1f - left;
        }

        /// <summary>De geluksbonus (0 tot 1) op een punt: per soort de sterkste plek.</summary>
        public float HappinessBonus(float2 point) =>
            math.saturate(_sites.GroupBy(s => s.Kind).Sum(g => g.Max(s => s.StrengthAt(point)) * HappinessOf(g.Key)));

        public static float LandValueOf(RecreationKind kind) => kind switch
        {
            RecreationKind.Park => ParkLandValue,
            RecreationKind.Playground => PlaygroundLandValue,
            RecreationKind.SportsField => SportsFieldLandValue,
            _ => PlazaLandValue,
        };

        public static float HappinessOf(RecreationKind kind) => kind switch
        {
            RecreationKind.Park => ParkHappiness,
            RecreationKind.Playground => PlaygroundHappiness,
            RecreationKind.SportsField => SportsFieldHappiness,
            _ => PlazaHappiness,
        };
    }
}
