using System;
using System.Collections.Generic;
using Cities.Simulation.Fields;
using Unity.Mathematics;

namespace Cities.Gameplay.Overlays
{
    /// <summary>
    /// Wat de milieu- en dienstenoverlays (E17-05) nodig hebben. Elke bron is optioneel: een overlay wordt alleen gemaakt als zijn bron is gevuld.
    /// Celbronnen leveren ruwe modelwaarden (concentratie, decibel, m/s); gebouwbronnen leveren per gebouw een waarde in de eenheid van de overlay.
    /// </summary>
    public sealed class EnvironmentSources
    {
        /// <summary>Luchtconcentratie per cel (<see cref="AirPollution"/>).</summary>
        public Func<int, int, float> AirConcentration { get; set; }

        /// <summary>Geluidsniveau in decibel per cel (<see cref="NoiseModel"/>).</summary>
        public Func<int, int, float> NoiseDecibel { get; set; }

        /// <summary>Bodemconcentratie per cel (<see cref="SoilPollution"/>).</summary>
        public Func<int, int, float> SoilConcentration { get; set; }

        /// <summary>Waterverontreiniging 0 tot 1 per cel; <see cref="float.NaN"/> waar geen water ligt.</summary>
        public Func<int, int, float> WaterLevel { get; set; }

        /// <summary>Windsnelheid in m/s per cel.</summary>
        public Func<int, int, float> WindSpeed { get; set; }

        /// <summary>Geluk per gebouw, 0 tot 100.</summary>
        public Action<List<OverlayEntityValue>> Happiness { get; set; }

        /// <summary>Gezondheid per gebouw, 0 tot 100.</summary>
        public Action<List<OverlayEntityValue>> Health { get; set; }

        /// <summary>Opleidingsniveau per gebouw, 0 tot 4.</summary>
        public Action<List<OverlayEntityValue>> Education { get; set; }

        /// <summary>Criminaliteit per gebouw, 0 tot 100.</summary>
        public Action<List<OverlayEntityValue>> Crime { get; set; }

        /// <summary>Brandrisico per gebouw, 0 tot 100.</summary>
        public Action<List<OverlayEntityValue>> FireRisk { get; set; }

        /// <summary>Parkeerbezetting per gebouw, 0 tot 100.</summary>
        public Action<List<OverlayEntityValue>> Parking { get; set; }
    }

    /// <summary>Overlays voor vervuiling, geluk, gezondheid, onderwijs, criminaliteit, brand, parkeren en wind.</summary>
    public static class EnvironmentOverlays
    {
        public const string AirId = "air-pollution";
        public const string NoiseId = "noise";
        public const string SoilId = "soil-pollution";
        public const string WaterId = "water-pollution";
        public const string WindId = "wind";
        public const string HappinessId = "happiness";
        public const string HealthId = "health";
        public const string EducationId = "education";
        public const string CrimeId = "crime";
        public const string FireId = "fire-risk";
        public const string ParkingId = "parking";

        /// <summary>Ondergrens van de geluidslegenda: de achtergrond (30 dB) is niets om te tonen.</summary>
        public const float NoiseMin = 30f;

        /// <summary>Geluid waarop de legenda stopt (drukke snelweg of vliegveld).</summary>
        public const float NoiseMax = 90f;

        /// <summary>Maakt de overlays waarvoor <paramref name="sources"/> een bron heeft.</summary>
        public static IReadOnlyList<IOverlaySource> Create(int cellsX, int cellsZ, EnvironmentSources sources, Func<long> version = null)
        {
            if (sources is null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            var list = new List<IOverlaySource>();
            void AddCell(Func<int, int, float> raw, string id, string name, OverlayLegend legend, Func<float, float> convert)
            {
                if (raw != null)
                {
                    list.Add(new DelegateCellOverlaySource(id, name, cellsX, cellsZ, legend, (x, z) => convert(raw(x, z)), version));
                }
            }

            AddCell(sources.AirConcentration, AirId, "Luchtvervuiling",
                new OverlayLegend("Luchtvervuiling", "%", 0f, 100f, OverlayColorScale.Hot, "0"), c => AirPollution.Level(c) * 100f);
            AddCell(sources.NoiseDecibel, NoiseId, "Geluid",
                new OverlayLegend("Geluid", "dB", NoiseMin, NoiseMax, OverlayColorScale.Hot, "0"), db => db);
            AddCell(sources.SoilConcentration, SoilId, "Bodemvervuiling",
                new OverlayLegend("Bodemvervuiling", "%", 0f, 100f, OverlayColorScale.Hot, "0"), c => SoilPollution.Level(c) * 100f);
            AddCell(sources.WaterLevel, WaterId, "Watervervuiling",
                new OverlayLegend("Watervervuiling", "%", 0f, 100f, OverlayColorScale.Hot, "0"), l => float.IsNaN(l) ? float.NaN : math.saturate(l) * 100f);
            AddCell(sources.WindSpeed, WindId, "Wind",
                new OverlayLegend("Wind", "m/s", 0f, WindModel.MaxSpeed, OverlayColorScale.Hot, "0.#"), s => s);
            AddBuilding(list, version, sources.Happiness, HappinessId, "Geluk",
                new OverlayLegend("Geluk", "%", 0f, 100f, OverlayColorScale.Good, "0"));
            AddBuilding(list, version, sources.Health, HealthId, "Gezondheid",
                new OverlayLegend("Gezondheid", "%", 0f, 100f, OverlayColorScale.Good, "0"));
            AddBuilding(list, version, sources.Education, EducationId, "Onderwijs",
                new OverlayLegend("Opleidingsniveau", string.Empty, 0f, 4f, OverlayColorScale.Good, "0.0"));
            AddBuilding(list, version, sources.Crime, CrimeId, "Criminaliteit",
                new OverlayLegend("Criminaliteit", "%", 0f, 100f, OverlayColorScale.Hot, "0"));
            AddBuilding(list, version, sources.FireRisk, FireId, "Brandrisico",
                new OverlayLegend("Brandrisico", "%", 0f, 100f, OverlayColorScale.Hot, "0"));
            AddBuilding(list, version, sources.Parking, ParkingId, "Parkeren",
                new OverlayLegend("Parkeerbezetting", "%", 0f, 100f, OverlayColorScale.Hot, "0"));
            return list;
        }

        private static void AddBuilding(List<IOverlaySource> list, Func<long> version, Action<List<OverlayEntityValue>> collect, string id, string name, OverlayLegend legend)
        {
            if (collect != null)
            {
                list.Add(new EntityOverlaySource(id, name, OverlayTarget.Building, legend, collect, version));
            }
        }
    }
}
