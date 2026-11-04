using System;
using Cities.Core.Settings;
using Unity.Mathematics;

namespace Cities.Gameplay.Visuals
{
    public enum QualityLevel : byte
    {
        Low,
        Medium,
        High,
        Custom,
    }

    /// <summary>Alle grafische knoppen op één plek (E19-10): wat een kwaliteitsniveau vastlegt.</summary>
    public sealed class QualityProfile
    {
#pragma warning disable S107 // Gegevensklasse: elke parameter is een eigen eigenschap
        public QualityProfile(
            float shadowDistance,
            int shadowResolution,
            bool ambientOcclusion,
            bool bloom,
            float renderScale,
            float lodScale,
            float propDensity,
            float particleDensity,
            bool cloudShadows)
        {
            if (!(shadowDistance >= 0f) || shadowResolution < 0 || !(renderScale >= 0.5f && renderScale <= 2f)
                || !(lodScale > 0f) || !(propDensity >= 0f && propDensity <= 1f) || !(particleDensity >= 0f && particleDensity <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(shadowDistance), "Een kwaliteitswaarde ligt buiten zijn bereik.");
            }

            ShadowDistance = shadowDistance;
            ShadowResolution = shadowResolution;
            AmbientOcclusion = ambientOcclusion;
            Bloom = bloom;
            RenderScale = renderScale;
            LodScale = lodScale;
            PropDensity = propDensity;
            ParticleDensity = particleDensity;
            CloudShadows = cloudShadows;
        }
#pragma warning restore S107

        public float ShadowDistance { get; }

        public int ShadowResolution { get; }

        public bool AmbientOcclusion { get; }

        public bool Bloom { get; }

        /// <summary>Verhouding van de renderresolutie tot het scherm.</summary>
        public float RenderScale { get; }

        /// <summary>Factor op de LOD-afstanden (<see cref="BuildingLod"/>): lager is eerder een goedkoper niveau.</summary>
        public float LodScale { get; }

        /// <summary>Aandeel (0 tot 1) van de props (gras, struiken, rotsen) dat getoond wordt.</summary>
        public float PropDensity { get; }

        /// <summary>Aandeel (0 tot 1) van het deeltjesbudget dat gebruikt mag worden.</summary>
        public float ParticleDensity { get; }

        public bool CloudShadows { get; }

#pragma warning disable S107 // Gegevensklasse: elke parameter is een eigen eigenschap
        public QualityProfile With(
            float? shadowDistance = null,
            int? shadowResolution = null,
            bool? ambientOcclusion = null,
            bool? bloom = null,
            float? renderScale = null,
            float? lodScale = null,
            float? propDensity = null,
            float? particleDensity = null,
            bool? cloudShadows = null) =>
            new QualityProfile(
                shadowDistance ?? ShadowDistance,
                shadowResolution ?? ShadowResolution,
                ambientOcclusion ?? AmbientOcclusion,
                bloom ?? Bloom,
                renderScale ?? RenderScale,
                lodScale ?? LodScale,
                propDensity ?? PropDensity,
                particleDensity ?? ParticleDensity,
                cloudShadows ?? CloudShadows);
#pragma warning restore S107
    }

    /// <summary>De toestand van de nabewerking op een moment (E19-07).</summary>
    public readonly struct PostProcessState
    {
        public PostProcessState(bool ambientOcclusion, int aoSamples, bool bloom, float bloomIntensity, float exposure, float saturation, float tiltShift)
        {
            AmbientOcclusion = ambientOcclusion;
            AoSamples = aoSamples;
            Bloom = bloom;
            BloomIntensity = bloomIntensity;
            Exposure = exposure;
            Saturation = saturation;
            TiltShift = tiltShift;
        }

        public bool AmbientOcclusion { get; }

        /// <summary>Aantal samples van de AO; meer is mooier en duurder.</summary>
        public int AoSamples { get; }

        public bool Bloom { get; }

        public float BloomIntensity { get; }

        /// <summary>Belichting in stops (EV), voor kleurcorrectie.</summary>
        public float Exposure { get; }

        /// <summary>Kleurverzadiging, 1 is neutraal.</summary>
        public float Saturation { get; }

        /// <summary>Sterkte van het tilt-shift-effect, 0 (uit) tot 1.</summary>
        public float TiltShift { get; }
    }

    /// <summary>
    /// Grafische kwaliteitsprofielen (E19-10): laag, middel, hoog en aangepast, tijdens het spel te wisselen. Wie een los onderdeel aanpast
    /// (<see cref="Customise"/>), komt op <see cref="QualityLevel.Custom"/>. De keuze staat in de gebruikersinstellingen.
    /// </summary>
    public sealed class QualityManager
    {
        public static readonly QualityProfile Low = new QualityProfile(80f, 512, false, false, 0.75f, 0.6f, 0.25f, 0.3f, false);
        public static readonly QualityProfile Medium = new QualityProfile(200f, 1024, true, true, 1f, 0.85f, 0.6f, 0.6f, true);
        public static readonly QualityProfile High = new QualityProfile(500f, 2048, true, true, 1f, 1.2f, 1f, 1f, true);

        private QualityProfile _custom;

        public QualityManager(QualityLevel level = QualityLevel.High)
        {
            Level = level;
            _custom = High;
            Profile = level == QualityLevel.Custom ? _custom : Preset(level);
        }

        public QualityLevel Level { get; private set; }

        public QualityProfile Profile { get; private set; }

        /// <summary>Het profiel is veranderd (door wisselen of aanpassen).</summary>
        public event Action<QualityProfile> Changed;

        public static QualityProfile Preset(QualityLevel level) => level switch
        {
            QualityLevel.Low => Low,
            QualityLevel.Medium => Medium,
            QualityLevel.High => High,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Alleen laag, middel en hoog zijn vaste profielen."),
        };

        /// <summary>Wisselt naar een niveau; <see cref="QualityLevel.Custom"/> pakt het laatste eigen profiel.</summary>
        public void Select(QualityLevel level)
        {
            if (!Enum.IsDefined(typeof(QualityLevel), level))
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Onbekend kwaliteitsniveau.");
            }

            Level = level;
            Profile = level == QualityLevel.Custom ? _custom : Preset(level);
            Changed?.Invoke(Profile);
        }

        /// <summary>Past het huidige profiel aan; het resultaat is een eigen profiel dat bewaard blijft.</summary>
        public void Customise(Func<QualityProfile, QualityProfile> change)
        {
            if (change is null)
            {
                throw new ArgumentNullException(nameof(change));
            }

            _custom = change(Profile) ?? throw new InvalidOperationException("De aanpassing geeft geen profiel terug.");
            Level = QualityLevel.Custom;
            Profile = _custom;
            Changed?.Invoke(Profile);
        }

        /// <summary>Neemt de keuze uit de instellingen over.</summary>
        public void Apply(UserSettings settings)
        {
            if (settings is null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            Select((QualityLevel)settings.GetChoice(SettingsCatalog.Quality));
        }

        /// <summary>Schrijft het huidige niveau naar de instellingen.</summary>
        public void Store(UserSettings settings)
        {
            if (settings is null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.Set(SettingsCatalog.Quality, (float)Level);
        }
    }

    /// <summary>
    /// Nabewerking (E19-07): AO, bloom, kleurcorrectie en optioneel tilt-shift, afgeleid van het kwaliteitsprofiel. Kleurcorrectie volgt de
    /// zon (kouder en donkerder 's nachts, warmer bij zonsopgang) en het weer (grijzer bij regen). Tilt-shift groeit met de camerahoogte.
    /// </summary>
    public static class PostProcessing
    {
        public const float TiltShiftStartHeight = 200f;
        public const float TiltShiftFullHeight = 1500f;

        private static int AoSamples(QualityProfile profile, QualityLevel level)
        {
            if (!profile.AmbientOcclusion || level == QualityLevel.Low)
            {
                return 0;
            }

            return level == QualityLevel.Medium ? 6 : 12;
        }

        public static PostProcessState For(QualityProfile profile, QualityLevel level, bool tiltShiftEnabled, float cameraHeight, float sunElevation, float cloudCover)
        {
            if (profile is null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var samples = AoSamples(profile, level);
            var night = 1f - math.smoothstep(-6f, 10f, sunElevation);
            var exposure = -0.6f * night;
            var saturation = math.lerp(1.05f, 0.75f, math.saturate(cloudCover) * 0.5f + (night * 0.2f));
            var bloom = profile.Bloom ? math.lerp(0.15f, 0.5f, night) : 0f;
            var tilt = tiltShiftEnabled ? math.smoothstep(TiltShiftStartHeight, TiltShiftFullHeight, math.max(0f, cameraHeight)) : 0f;
            return new PostProcessState(profile.AmbientOcclusion, samples, profile.Bloom, bloom, exposure, saturation, tilt);
        }
    }
}
