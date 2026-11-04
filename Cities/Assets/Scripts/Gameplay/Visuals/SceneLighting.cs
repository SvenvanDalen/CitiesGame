using System;
using Cities.Core.Randomness;
using Unity.Mathematics;

namespace Cities.Gameplay.Visuals
{
    /// <summary>
    /// Bewegende wolken met zachte schaduw (E19-11). Het wolkenveld is ruis die met de wind over de kaart schuift; de bewolking (0 tot 1, uit het
    /// weer) bepaalt hoeveel van het veld dicht is. <see cref="Density"/> geeft de dikte op een plek, <see cref="LightFactor"/> hoeveel licht daar
    /// nog doorkomt: de schaduwrand is zacht (<see cref="Softness"/>) en nooit helemaal donker.
    /// </summary>
    public sealed class CloudField
    {
        /// <summary>Hoeveel van het zonlicht een dikke wolk maximaal wegneemt.</summary>
        public const float MaxShadow = 0.7f;

        private readonly ulong _seed;

        public CloudField(ulong seed, float cloudSize = 800f, float softness = 0.25f)
        {
            if (!(cloudSize > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(cloudSize), cloudSize, "Een wolk heeft een grootte.");
            }

            if (!(softness > 0f && softness <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(softness), softness, "De zachtheid ligt boven 0 tot 1.");
            }

            _seed = seed;
            CloudSize = cloudSize;
            Softness = softness;
        }

        /// <summary>Typische afmeting van een wolk in meter.</summary>
        public float CloudSize { get; }

        /// <summary>Breedte (0 tot 1) van de overgang tussen licht en schaduw.</summary>
        public float Softness { get; }

        /// <summary>Het veld verschoven met de wind: positie van het wolkenveld na <paramref name="seconds"/> bij windrichting en -snelheid.</summary>
        public static float2 Offset(float2 windDirection, float windSpeed, float seconds) =>
            math.lengthsq(windDirection) <= 0f ? float2.zero : math.normalize(windDirection) * windSpeed * seconds;

        /// <summary>Wolkendikte op een plek, 0 tot 1: twee lagen ruis, drempel uit de bewolking.</summary>
        public float Density(float2 position, float2 offset, float cover)
        {
            var p = (position - offset) / CloudSize;
            var noise = (0.65f * Noise(p)) + (0.35f * Noise(p * 2.3f + 17.1f));
            var threshold = 1f - math.saturate(cover);
            return math.smoothstep(threshold - Softness, threshold + Softness, noise);
        }

        /// <summary>Licht dat op een plek doorkomt: 1 onder open lucht, 1 min <see cref="MaxShadow"/> onder een dikke wolk.</summary>
        public float LightFactor(float2 position, float2 offset, float cover) => 1f - (MaxShadow * Density(position, offset, cover));

        // Waarderuis op een raster met gladde interpolatie; deterministisch uit de seed.
        private float Noise(float2 p)
        {
            var cell = math.floor(p);
            var f = p - cell;
            var u = f * f * (3f - (2f * f));
            var a = Hash((int)cell.x, (int)cell.y);
            var b = Hash((int)cell.x + 1, (int)cell.y);
            var c = Hash((int)cell.x, (int)cell.y + 1);
            var d = Hash((int)cell.x + 1, (int)cell.y + 1);
            return math.lerp(math.lerp(a, b, u.x), math.lerp(c, d, u.x), u.y);
        }

        private float Hash(int x, int y) => (SeedHash.Combine(_seed, (ulong)(uint)x, (ulong)(uint)y) >> 40) / (float)(1 << 24);
    }

    /// <summary>
    /// Nachtverlichting (E19-06): straatlantaarns, koplampen en ramen gaan aan volgens de tijd. Lantaarns hebben hysterese (aan onder 3° zon,
    /// uit boven 8°) en elk een eigen schakeltijd, zodat een straat niet in één klap oplicht. Ramen branden 's avonds vaker dan diep in de
    /// nacht. Koplampen gaan aan in het donker en bij slecht zicht.
    /// </summary>
    public static class NightLighting
    {
        public const float LampsOnBelow = 3f;
        public const float LampsOffAbove = 8f;

        /// <summary>Zichtafstand (m) waaronder voertuigen hun koplampen aanzetten, ook overdag.</summary>
        public const float HeadlightVisibility = 3_000f;

        /// <summary>Straatlantaarns met hysterese: aan onder <see cref="LampsOnBelow"/> graden, uit boven <see cref="LampsOffAbove"/>.</summary>
        public static bool StreetLampsOn(float sunElevation, bool currentlyOn) =>
            currentlyOn ? sunElevation < LampsOffAbove : sunElevation < LampsOnBelow;

        /// <summary>Zet één lantaarn aan of uit met een eigen verschuiving van ±<paramref name="spreadDegrees"/> graden zonshoogte, vast per lantaarn.</summary>
        public static bool LampOn(int lampId, float sunElevation, bool currentlyOn, float spreadDegrees = 3f) =>
            StreetLampsOn(sunElevation + (Offset((ulong)(uint)lampId) * spreadDegrees), currentlyOn);

        /// <summary>Brandt het licht in een raam? Kans per uur van de dag (0 tot 23), vast per gebouw; kantoren branden overdag, woningen 's avonds.</summary>
        public static bool WindowLit(int buildingId, int hour, bool residential, bool dark)
        {
            if (hour < 0 || hour > 23)
            {
                throw new ArgumentOutOfRangeException(nameof(hour), hour, "Een uur ligt tussen 0 en 23.");
            }

            var chance = residential ? ResidentialChance(hour) : OfficeChance(hour);
            if (!dark)
            {
                chance *= 0.2f;
            }

            return (SeedHash.Combine(7UL, (ulong)(uint)buildingId, (ulong)hour) >> 40) / (float)(1 << 24) < chance;
        }

        /// <summary>Voertuigen rijden met koplampen als het donker is of het zicht slecht.</summary>
        public static bool HeadlightsOn(float sunElevation, float visibility) =>
            sunElevation < LampsOffAbove || visibility < HeadlightVisibility;

        private static float ResidentialChance(int hour)
        {
            if (hour >= 18 && hour <= 23)
            {
                return 0.8f;
            }

            if (hour <= 1 || (hour >= 6 && hour <= 8))
            {
                return 0.35f;
            }

            return hour <= 5 ? 0.08f : 0.15f;
        }

        private static float OfficeChance(int hour) => hour >= 8 && hour <= 18 ? 0.85f : hour >= 19 && hour <= 21 ? 0.25f : 0.05f;

        // Vaste waarde van -1 tot 1 per lantaarn.
        private static float Offset(ulong id) => (((SeedHash.Mix(id ^ 0x9E3779B97F4A7C15UL) >> 40) / (float)(1 << 24)) * 2f) - 1f;
    }

    public enum LodLevel : byte
    {
        Lod0,
        Lod1,
        Lod2,
        Imposter,
        Culled,
    }

    /// <summary>
    /// Gebouw-LOD (E19-05): drie meshniveaus, daarna een imposter (plaatje) en ten slotte niets. De afstanden schalen met de grootte van het
    /// gebouw (een toren blijft langer scherp dan een schuur) en met een kwaliteitsfactor. Een hysterese van <see cref="Hysteresis"/> voorkomt
    /// flikkeren bij een grens: wie op een niveau staat, wisselt pas na een extra marge.
    /// </summary>
    public static class BuildingLod
    {
        /// <summary>Afstanden (meter) voor een gebouw van referentiegrootte <see cref="ReferenceSize"/> waarop het volgende niveau begint.</summary>
        public static readonly float[] Distances = { 150f, 400f, 900f, 4_000f };

        public const float ReferenceSize = 20f;
        public const float Hysteresis = 0.1f;

        public static LodLevel Select(float distance, float buildingSize, float qualityScale, LodLevel current)
        {
            if (!(buildingSize > 0f) || !(qualityScale > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(buildingSize), "Grootte en kwaliteitsfactor zijn groter dan nul.");
            }

            var scale = math.clamp(buildingSize / ReferenceSize, 0.5f, 4f) * qualityScale;
            var level = 0;
            for (var i = 0; i < Distances.Length; i++)
            {
                var limit = Distances[i] * scale;

                // Op het huidige niveau of verder hoeven we de grens pas na de marge te passeren; ervoor al eerder terug.
                if (i < (int)current)
                {
                    limit *= 1f - Hysteresis;
                }
                else if (i == (int)current)
                {
                    limit *= 1f + Hysteresis;
                }

                if (distance >= limit)
                {
                    level = i + 1;
                }
            }

            return (LodLevel)level;
        }
    }
}
