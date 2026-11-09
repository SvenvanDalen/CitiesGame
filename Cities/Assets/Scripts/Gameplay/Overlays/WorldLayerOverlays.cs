using System;
using System.Collections.Generic;
using Cities.World.Grid;

namespace Cities.Gameplay.Overlays
{
    /// <summary>Overlays op de lagen van de <see cref="WorldMap"/>: hoogte, landwaarde, grondvervuiling, waterdiepte en zones.</summary>
    public static class WorldLayerOverlays
    {
        public const string HeightId = "height";
        public const string LandValueId = "land-value";
        public const string GroundPollutionId = "ground-pollution";
        public const string WaterDepthId = "water-depth";
        public const string ZonesId = "zones";

        /// <summary>
        /// Maakt de vijf bronnen. <paramref name="version"/> levert een teller die oploopt als de lagen veranderden
        /// (bijvoorbeeld de simulatietick gedeeld door het verversinterval); zonder teller blijft het beeld staan.
        /// </summary>
        public static IReadOnlyList<IOverlaySource> Create(WorldMap map, Func<long> version = null)
        {
            if (map is null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var cellsX = map.Dimensions.CellsX;
            var cellsZ = map.Dimensions.CellsZ;
            var coarse = WorldMap.CoarseCellsPerSample;
            return new IOverlaySource[]
            {
                new DelegateCellOverlaySource(
                    HeightId,
                    "Hoogte",
                    cellsX,
                    cellsZ,
                    new OverlayLegend("Hoogte", "m", -5f, 150f, OverlayColorScale.Terrain, "0"),
                    (x, z) => CornerAverage(map, x, z),
                    version),
                new DelegateCellOverlaySource(
                    LandValueId,
                    "Landwaarde",
                    cellsX,
                    cellsZ,
                    new OverlayLegend("Landwaarde", "per m²", 0f, 500f, OverlayColorScale.Good, "0"),
                    (x, z) => map.LandValue[x / coarse, z / coarse],
                    version),
                new DelegateCellOverlaySource(
                    GroundPollutionId,
                    "Grondvervuiling",
                    cellsX,
                    cellsZ,
                    new OverlayLegend("Grondvervuiling", "%", 0f, 100f, OverlayColorScale.Hot, "0"),
                    (x, z) => map.GroundPollution[x / coarse, z / coarse] * 100f,
                    version),
                new DelegateCellOverlaySource(
                    WaterDepthId,
                    "Waterdiepte",
                    cellsX,
                    cellsZ,
                    new OverlayLegend("Waterdiepte", "m", 0f, 10f, OverlayColorScale.Hot, "0.0"),
                    (x, z) =>
                    {
                        map.CompleteWaterWrites();
                        return map.WaterDepth[x, z];
                    },
                    version),
                new DelegateCellOverlaySource(
                    ZonesId,
                    "Zones",
                    cellsX,
                    cellsZ,
                    ZoneLegend,
                    (x, z) => ZoneValue(map.Zones[x, z]),
                    version),
            };
        }

        /// <summary>Zonetypen op een schaal van 1 tot 4; een cel zonder zone heeft geen gegevens.</summary>
        public static OverlayLegend ZoneLegend { get; } = new OverlayLegend(
            "Zones: 1 wonen, 2 handel, 3 industrie, 4 kantoor",
            string.Empty,
            0f,
            4f,
            new OverlayColorScale(
                new[]
                {
                    new OverlayColor(90, 90, 90), new OverlayColor(70, 170, 80), new OverlayColor(60, 120, 215),
                    new OverlayColor(225, 190, 50), new OverlayColor(150, 90, 200),
                },
                new OverlayColor(90, 90, 90, 0)),
            "0");

        /// <summary>Waarde van een zonecel voor <see cref="ZoneLegend"/>; <see cref="float.NaN"/> zonder zone.</summary>
        public static float ZoneValue(ZoneCell zone) => zone.IsZoned ? (float)(int)zone.Type : float.NaN;

        /// <summary>Gemiddelde hoogte van de vier hoekpunten van een cel.</summary>
        private static float CornerAverage(WorldMap map, int x, int z) =>
            (map.Height[x, z] + map.Height[x + 1, z] + map.Height[x, z + 1] + map.Height[x + 1, z + 1]) * 0.25f;
    }
}
