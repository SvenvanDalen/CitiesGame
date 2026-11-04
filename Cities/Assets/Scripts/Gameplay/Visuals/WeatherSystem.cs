using System;
using Cities.Core.Persistence;
using Cities.Core.Randomness;
using Unity.Mathematics;

namespace Cities.Gameplay.Visuals
{
    public enum WeatherKind : byte
    {
        Clear,
        Cloudy,
        Rain,
        Fog,
        Storm,
    }

    /// <summary>
    /// Het weer (E19-04): regen, mist, wind en onweer. De toestand wisselt na een aantal uur naar een nieuwe, gekozen met kansen per seizoen
    /// (<see cref="Weights"/>), uit de seed van de simulatie. Uit de toestand volgen zicht, regen, bewolking, windsnelheid, bliksemkans en
    /// optioneel het wegdek (<see cref="RoadGrip"/>, uit te zetten met <see cref="AffectsRoads"/>). Staat het weer uit, dan blijft het helder.
    /// </summary>
    public sealed class WeatherSystem
    {
        public const float MinHours = 3f;
        public const float MaxHours = 12f;

        // Per seizoen (winter, lente, zomer, herfst) de kansen op Clear, Cloudy, Rain, Fog, Storm.
        private static readonly int[][] SeasonWeights =
        {
            new[] { 20, 35, 25, 15, 5 },
            new[] { 35, 30, 25, 5, 5 },
            new[] { 50, 20, 15, 2, 13 },
            new[] { 20, 30, 30, 15, 5 },
        };

        private bool _enabled = true;

        public WeatherKind Kind { get; private set; }

        /// <summary>Hoe zwaar, 0 tot 1.</summary>
        public float Intensity { get; private set; } = 0.5f;

        /// <summary>Uren tot het weer verandert.</summary>
        public float HoursLeft { get; private set; }

        /// <summary>Windsnelheid in m/s.</summary>
        public float WindSpeed { get; private set; } = 3f;

        /// <summary>Of slecht weer het wegdek en dus het verkeer beïnvloedt (optioneel).</summary>
        public bool AffectsRoads { get; set; } = true;

        /// <summary>Weer uitzetten houdt het helder en windstil rustig.</summary>
        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value)
                {
                    return;
                }

                _enabled = value;
                if (!value)
                {
                    Set(WeatherKind.Clear, 0f, 0f, 3f);
                }
            }
        }

        public event Action<WeatherKind> Changed;

        /// <summary>De kansen op de vijf weertypen voor een maand (1 tot 12).</summary>
        public static int[] Weights(int month)
        {
            if (month < 1 || month > 12)
            {
                throw new ArgumentOutOfRangeException(nameof(month), month, "Een maand ligt tussen 1 en 12.");
            }

            var season = (month % 12) / 3;
            return (int[])SeasonWeights[season].Clone();
        }

        /// <summary>Zicht in meter.</summary>
        public float Visibility => Kind switch
        {
            WeatherKind.Clear => 20_000f,
            WeatherKind.Cloudy => 15_000f,
            WeatherKind.Rain => 12_000f - (8_000f * Intensity),
            WeatherKind.Fog => math.max(300f, 1_500f - (1_200f * Intensity)),
            _ => 6_000f - (3_000f * Intensity),
        };

        /// <summary>Hoeveelheid regen, 0 tot 1.</summary>
        public float RainAmount => Kind switch
        {
            WeatherKind.Rain => Intensity,
            WeatherKind.Storm => 0.6f + (0.4f * Intensity),
            _ => 0f,
        };

        /// <summary>Bewolking, 0 tot 1.</summary>
        public float CloudCover => Kind switch
        {
            WeatherKind.Clear => 0.15f,
            WeatherKind.Cloudy => 0.7f,
            WeatherKind.Rain => 0.85f,
            WeatherKind.Fog => 0.5f,
            _ => 1f,
        };

        /// <summary>Mist, 0 tot 1.</summary>
        public float FogAmount => Kind == WeatherKind.Fog ? Intensity : 0f;

        /// <summary>Kans per uur op bliksem; alleen bij onweer.</summary>
        public float LightningChancePerHour => Kind == WeatherKind.Storm ? 0.5f + (0.5f * Intensity) : 0f;

        /// <summary>Grip van het wegdek, 0 tot 1: 1 droog; regen en onweer verminderen hem als <see cref="AffectsRoads"/> aan staat.</summary>
        public float RoadGrip
        {
            get
            {
                if (!AffectsRoads)
                {
                    return 1f;
                }

                return Kind switch
                {
                    WeatherKind.Rain => 1f - (0.25f * Intensity),
                    WeatherKind.Storm => 1f - (0.35f * Intensity),
                    WeatherKind.Fog => 0.95f,
                    _ => 1f,
                };
            }
        }

        /// <summary>Schuift het weer <paramref name="hours"/> uur op; na afloop volgt een nieuwe toestand voor deze maand.</summary>
        public void Advance(float hours, int month, ref DeterministicRandom random)
        {
            if (hours < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(hours), hours, "De tijd loopt vooruit.");
            }

            if (!_enabled)
            {
                return;
            }

            HoursLeft -= hours;
            if (HoursLeft > 0f)
            {
                return;
            }

            var weights = Weights(month);
            var kind = (WeatherKind)random.NextWeightedIndex(weights);
            var intensity = random.NextFloat(0.3f, 1f);
            var length = random.NextFloat(MinHours, MaxHours);
            var wind = kind switch
            {
                WeatherKind.Clear => random.NextFloat(2f, 6f),
                WeatherKind.Cloudy => random.NextFloat(4f, 8f),
                WeatherKind.Rain => random.NextFloat(6f, 12f),
                WeatherKind.Fog => random.NextFloat(0f, 2f),
                _ => random.NextFloat(15f, 25f),
            };
            Set(kind, intensity, length, wind);
        }

        /// <summary>Zet het weer handmatig (testen, scenario's).</summary>
        public void Force(WeatherKind kind, float intensity, float hours, float windSpeed)
        {
            if (!Enum.IsDefined(typeof(WeatherKind), kind) || !(intensity >= 0f && intensity <= 1f) || hours < 0f || windSpeed < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), "Weertype, intensiteit (0 tot 1), duur en wind moeten kloppen.");
            }

            Set(kind, intensity, hours, windSpeed);
        }

        internal void Restore(bool enabled, bool affectsRoads, WeatherKind kind, float intensity, float hoursLeft, float wind)
        {
            _enabled = enabled;
            AffectsRoads = affectsRoads;
            Kind = kind;
            Intensity = intensity;
            HoursLeft = hoursLeft;
            WindSpeed = wind;
        }

        private void Set(WeatherKind kind, float intensity, float hours, float wind)
        {
            var changed = kind != Kind;
            Kind = kind;
            Intensity = intensity;
            HoursLeft = hours;
            WindSpeed = wind;
            if (changed)
            {
                Changed?.Invoke(kind);
            }
        }
    }

    /// <summary>Sectie <c>WTHR</c>: het huidige weer (E19-04).</summary>
    public sealed class WeatherSection : ISaveSection
    {
        public const string SectionId = "WTHR";

        private readonly WeatherSystem _weather;

        public WeatherSection(WeatherSystem weather)
        {
            _weather = weather ?? throw new ArgumentNullException(nameof(weather));
        }

        public string Id => SectionId;

        public int Version => 1;

        public void Write(SaveWriter writer)
        {
            writer.Write(_weather.Enabled);
            writer.Write(_weather.AffectsRoads);
            writer.Write((byte)_weather.Kind);
            writer.Write(_weather.Intensity);
            writer.Write(_weather.HoursLeft);
            writer.Write(_weather.WindSpeed);
        }

        public Action Read(SaveReader reader, int version)
        {
            var enabled = reader.ReadBool();
            var roads = reader.ReadBool();
            var kind = (WeatherKind)reader.ReadByte();
            var intensity = reader.ReadSingle();
            var hours = reader.ReadSingle();
            var wind = reader.ReadSingle();
            var valid = Enum.IsDefined(typeof(WeatherKind), kind) && intensity >= 0f && intensity <= 1f && hours >= -1000f && hours <= 1000f && wind >= 0f && wind <= 100f;
            if (!valid)
            {
                throw reader.Corrupt($"onmogelijk weer {kind} met intensiteit {intensity}");
            }

            reader.ExpectEnd();
            return () => _weather.Restore(enabled, roads, kind, intensity, hours, wind);
        }
    }
}
