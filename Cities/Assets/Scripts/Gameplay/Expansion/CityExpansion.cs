using System;
using System.Collections.Generic;
using Cities.Core.Persistence;
using Cities.Core.Randomness;
using Cities.Gameplay.Replay;
using Cities.Gameplay.UI;
using Cities.Gameplay.Visuals;
using Cities.Simulation.Disasters;
using Cities.Simulation.Fields;
using Cities.Simulation.Vehicles;
using Unity.Mathematics;

namespace Cities.Gameplay.Expansion
{
    /// <summary>
    /// Instellingen voor de M8-uitbreidingen. Alles met gevolgen voor het spel staat standaard uit, zodat de moeilijkheid laag blijft;
    /// seizoenen, hints en timelapse zijn alleen weergave en staan aan.
    /// </summary>
    public sealed class ExpansionSettings
    {
        public bool Seasons { get; set; } = true;

        public bool Hints { get; set; } = true;

        public bool Timelapse { get; set; } = true;

        public bool Accidents { get; set; }

        public bool Winter { get; set; }

        public bool Heatwaves { get; set; }

        public bool ClimateEffects { get; set; }
    }

    /// <summary>
    /// Bundelt de uitbreidingsmodellen van M8 achter één dagelijkse stap, zodat de scène ze met één aanroep laat meedraaien. De logica staat
    /// hier en niet in de host, zodat ze testbaar is.
    /// </summary>
    public sealed class CityExpansion
    {
        /// <summary>Uitstoot per inwoner per dag in ton, bewust klein zodat de opwarming pas na lang spelen merkbaar wordt.</summary>
        public const double TonnesPerResidentPerDay = 0.002d;

        /// <summary>Opname per dag in ton per ingezet bos- of parkpunt.</summary>
        public const double AbsorbedPerSinkPerDay = 0.5d;

        public const int HeatwaveMaxStrength = 3;

        private readonly ulong _seed;

        public CityExpansion(ulong seed)
        {
            _seed = seed;
            Seasons = new SeasonLook(seed);
            Accidents = new AccidentModel(seed);
            Heatwaves = new HeatwaveField();
            Carbon = new CarbonBalance();
            Winter = new WinterModel();
            Timelapse = new GrowthTimelapse();
            Hints = new ToolHints();
        }

        public ExpansionSettings Settings { get; } = new ExpansionSettings();

        public SeasonLook Seasons { get; }

        public AccidentModel Accidents { get; }

        public HeatwaveField Heatwaves { get; }

        public CarbonBalance Carbon { get; }

        public WinterModel Winter { get; }

        public GrowthTimelapse Timelapse { get; }

        public ToolHints Hints { get; }

        /// <summary>Snelheidsfactor voor het verkeer: 1, of lager door sneeuw als winter aan staat.</summary>
        public float TrafficGrip => Settings.Winter ? Winter.RoadGrip : 1f;

        public float WaterDemandFactor => Settings.Heatwaves && Heatwaves.Current != null ? Heatwaves.Current.WaterDemandFactor : 1f;

        public float EnergyDemandFactor
        {
            get
            {
                var heat = Settings.Heatwaves && Heatwaves.Current != null ? Heatwaves.Current.EnergyDemandFactor : 1f;
                var cold = Settings.Winter ? Winter.HeatingDemand : 1f;
                return heat * cold;
            }
        }

        /// <summary>Gemiddelde dagtemperatuur voor een maand (1 tot 12) in graden, zonder weer: zomer 19, winter 1.</summary>
        public static float BaseTemperature(int month)
        {
            var angle = ((month - 7) / 12f) * 2f * math.PI;
            return 10f + (9f * math.cos(angle));
        }

        /// <summary>Past de instellingen toe op de modellen.</summary>
        public void ApplySettings()
        {
            Accidents.Enabled = Settings.Accidents;
            Carbon.ClimateEffects = Settings.ClimateEffects;
            Hints.Enabled = Settings.Hints;
        }

        /// <summary>
        /// Eén spelletdag: uitstoot, hittegolf, winter en timelapse. <paramref name="snowing"/> is waar als het weer nu sneeuwt;
        /// <paramref name="sinks"/> telt bomen en parken die CO2 opnemen.
        /// </summary>
        public void Daily(int day, int month, int population, int buildings, bool snowing, int sinks)
        {
            if (day < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(day), day, "Een dag is niet negatief.");
            }

            ApplySettings();
            Carbon.Emit(PollutionSource.Traffic, population * TonnesPerResidentPerDay);
            Carbon.Absorb(Math.Max(0, sinks) * AbsorbedPerSinkPerDay);
            var temperature = BaseTemperature(month) + Carbon.TemperatureShift;
            AdvanceHeatwave(day, month, temperature);
            AdvanceWinter(temperature, snowing);
            if (Settings.Timelapse)
            {
                Timelapse.Record(day, population, buildings);
            }
        }

        private void AdvanceHeatwave(int day, int month, float temperature)
        {
            if (!Settings.Heatwaves)
            {
                Heatwaves.Clear();
                return;
            }

            var current = Heatwaves.Current;
            if (current != null)
            {
                current.Advance(null);
                if (current.IsOver)
                {
                    Heatwaves.Clear();
                }

                return;
            }

            // Alleen in de warme maanden, met de kans uit de ramp, hoger bij opwarming.
            if (month < 6 || month > 8 || temperature < 15f)
            {
                return;
            }

            var chance = Heatwave.Spec.ChancePerDay * 10f * Carbon.HeatwaveChanceFactor;
            var roll = (SeedHash.Combine(_seed, (ulong)day, 0x4EA7UL) >> 40) / (float)(1 << 24);
            if (roll < chance)
            {
                var strength = 1 + (int)((SeedHash.Combine(_seed, (ulong)day, 0x57E1UL) >> 40) % HeatwaveMaxStrength);
                Heatwaves.Start(strength, 4 + (int)((SeedHash.Combine(_seed, (ulong)day, 0xD14UL) >> 40) % 4UL), SeedHash.Combine(_seed, (ulong)day));
            }
        }

        private void AdvanceWinter(float temperature, bool snowing)
        {
            if (!Settings.Winter)
            {
                return;
            }

            // Een dag in vier stappen van zes uur is genoeg voor sneeuwdek en smelt.
            for (var step = 0; step < 4; step++)
            {
                Winter.AdvanceHour(temperature, snowing && temperature <= 1f ? 1.5f : 0f);
            }
        }

        /// <summary>De opslagsecties van de uitbreidingen.</summary>
        public IEnumerable<ISaveSection> Sections()
        {
            yield return new AccidentsSection(Accidents);
            yield return new HeatwaveSection(Heatwaves);
            yield return new CarbonBalanceSection(Carbon);
        }
    }
}
