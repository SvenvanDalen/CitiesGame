using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Cities.Gameplay.Overlays
{
    /// <summary>
    /// Wat de overlays voor economie en wereld (E17-06) nodig hebben. Elke bron is optioneel: een overlay wordt alleen gemaakt als zijn bron is gevuld.
    /// Celbronnen leveren een waarde per cel in de eenheid die in de naam staat; gebouwbronnen leveren per gebouw een waarde van 0 tot 100.
    /// </summary>
    public sealed class EconomySources
    {
        /// <summary>Hoeveelheid winbare grondstoffen per cel, 0 (niets) tot 1 (rijkste voorkomen).</summary>
        public Func<int, int, float> ResourceRichness { get; set; }

        /// <summary>Districtnummer per cel; 0 of lager waar geen district ligt (<see cref="float.NaN"/> mag ook).</summary>
        public Func<int, int, float> District { get; set; }

        /// <summary>Hoogste districtnummer, voor de kleurschaal van de districtenoverlay.</summary>
        public int MaxDistrict { get; set; } = 16;

        /// <summary>Dekking van het openbaar vervoer per cel, 0 (geen halte binnen bereik) tot 1 (volledig).</summary>
        public Func<int, int, float> TransitCoverage { get; set; }

        /// <summary>Toeristische aantrekkingskracht per gebouw, 0 tot 100.</summary>
        public Action<List<OverlayEntityValue>> Tourism { get; set; }

        /// <summary>Handelsactiviteit (import, export, vracht) per gebouw, 0 tot 100.</summary>
        public Action<List<OverlayEntityValue>> Trade { get; set; }
    }

    /// <summary>Overlays voor grondstoffen, districten, openbaar vervoer, toerisme en handel.</summary>
    public static class EconomyOverlays
    {
        public const string ResourcesId = "resources";
        public const string DistrictsId = "districts";
        public const string TransitId = "transit-coverage";
        public const string TourismId = "tourism";
        public const string TradeId = "trade";

        /// <summary>Maakt de overlays waarvoor <paramref name="sources"/> een bron heeft.</summary>
        public static IReadOnlyList<IOverlaySource> Create(int cellsX, int cellsZ, EconomySources sources, Func<long> version = null)
        {
            if (sources is null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            var list = new List<IOverlaySource>();
            if (sources.ResourceRichness != null)
            {
                var raw = sources.ResourceRichness;
                list.Add(new DelegateCellOverlaySource(ResourcesId, "Grondstoffen", cellsX, cellsZ,
                    new OverlayLegend("Grondstoffen", "%", 0f, 100f, OverlayColorScale.Good, "0"), (x, z) => math.saturate(raw(x, z)) * 100f, version));
            }

            if (sources.District != null)
            {
                var raw = sources.District;
                var max = math.max(1, sources.MaxDistrict);
                list.Add(new DelegateCellOverlaySource(DistrictsId, "Districten", cellsX, cellsZ,
                    new OverlayLegend("District", string.Empty, 1f, max, OverlayColorScale.Terrain, "0"), (x, z) => District(raw(x, z), max), version));
            }

            if (sources.TransitCoverage != null)
            {
                var raw = sources.TransitCoverage;
                list.Add(new DelegateCellOverlaySource(TransitId, "Openbaar vervoer", cellsX, cellsZ,
                    new OverlayLegend("OV-dekking", "%", 0f, 100f, OverlayColorScale.Good, "0"), (x, z) => math.saturate(raw(x, z)) * 100f, version));
            }

            AddBuilding(list, version, sources.Tourism, TourismId, "Toerisme", new OverlayLegend("Toerisme", "%", 0f, 100f, OverlayColorScale.Good, "0"));
            AddBuilding(list, version, sources.Trade, TradeId, "Handel", new OverlayLegend("Handel", "%", 0f, 100f, OverlayColorScale.Good, "0"));
            return list;
        }

        // Cellen zonder district (nummer 0 of lager, of NaN) blijven leeg; de rest wordt op het bereik van de legenda gehouden.
        private static float District(float value, int max) =>
            float.IsNaN(value) || value < 1f ? float.NaN : math.min(value, max);

        private static void AddBuilding(List<IOverlaySource> list, Func<long> version, Action<List<OverlayEntityValue>> collect, string id, string name, OverlayLegend legend)
        {
            if (collect != null)
            {
                list.Add(new EntityOverlaySource(id, name, OverlayTarget.Building, legend, collect, version));
            }
        }
    }
}
