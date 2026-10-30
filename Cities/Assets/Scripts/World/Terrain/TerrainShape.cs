using System;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.World.Terrain
{
    /// <summary>Instellingen voor <see cref="TerrainShape"/>; alle maten in meters.</summary>
    [Serializable]
    public struct TerrainShapeSettings
    {
        [Tooltip("Zelfde seed, zelfde landschap.")]
        public int Seed;

        [Tooltip("Gemiddelde hoogte van het laagland.")]
        public float BaseHeight;

        [Tooltip("Hoogteverschil van de golvende vlaktes, over kilometers.")]
        public float PlainsRelief;

        [Tooltip("Hoogte van heuvels, over honderden meters.")]
        public float HillHeight;

        [Tooltip("Hoogte van de bergkammen.")]
        public float MountainHeight;

        [Tooltip("Deel van de kaart met bergen, 0 tot 1.")]
        [Range(0f, 1f)]
        public float MountainCoverage;

        [Tooltip("Diepte van de rivierbedding onder het laagland.")]
        public float RiverDepth;

        [Tooltip("Halve breedte van het rivierdal.")]
        public float ValleyWidth;

        [Tooltip("Gemiddelde X van de rivier, die van zuid naar noord loopt.")]
        public float RiverCenterX;

        public static TerrainShapeSettings Default => new TerrainShapeSettings
        {
            Seed = 1,
            BaseHeight = 40f,
            PlainsRelief = 22f,
            HillHeight = 28f,
            MountainHeight = 420f,
            MountainCoverage = 0.35f,
            RiverDepth = 9f,
            ValleyWidth = 420f,
            RiverCenterX = 5200f,
        };
    }

    /// <summary>
    /// Procedureel landschap: golvend laagland met heuvels, een bergrug met scherpe kammen en een meanderend
    /// rivierdal dat de bergen wegsnijdt. Gebaseerd op simplex-ruis met domain warping; dezelfde instellingen geven
    /// op hetzelfde platform dezelfde hoogtes.
    /// </summary>
    public static class TerrainShape
    {
        public static float Height(float2 position, in TerrainShapeSettings settings)
        {
            var offset = SeedOffset(settings.Seed);
            var p = position + offset;

            // Domain warping maakt de vormen grilliger en minder rasterachtig.
            var warp = new float2(Fbm((p / 2600f) + 17.3f, 3), Fbm((p / 2600f) - 41.9f, 3)) * 350f;
            var w = p + warp;

            var plains = Fbm(w / 6500f, 3) * settings.PlainsRelief;
            var hills = Fbm(w / 1100f, 5) * settings.HillHeight;
            hills *= 0.5f + (0.5f * math.saturate((Fbm(w / 4000f, 2) + 0.3f) * 1.5f));

            var mountainMask = math.smoothstep(1f - settings.MountainCoverage - 0.15f, 1f - settings.MountainCoverage + 0.2f, (Fbm(w / 9000f, 2) * 0.5f) + 0.5f);
            var mountains = math.pow(Ridged(w / 3400f, 5), 1.6f) * settings.MountainHeight * mountainMask;

            // Het dal loopt van zuid naar noord en slingert; erbuiten blijven bergen en heuvels heel.
            var distance = RiverDistance(position, settings);
            var outside = math.smoothstep(settings.ValleyWidth * 0.35f, settings.ValleyWidth * 1.6f, distance);
            var bed = (1f - math.smoothstep(0f, settings.ValleyWidth * 0.3f, distance)) * settings.RiverDepth;
            var land = settings.BaseHeight + plains + hills + mountains;

            // De dalbodem ligt nooit hoger dan het land ernaast, zodat de rivier altijd het laagste punt is.
            var valleyFloor = SmoothMin(settings.BaseHeight + (plains * 0.4f), land, 12f) - bed;

            return math.max(0f, math.lerp(valleyFloor, land, outside));
        }

        /// <summary>Horizontale afstand tot de as van de rivier.</summary>
        public static float RiverDistance(float2 position, in TerrainShapeSettings settings)
        {
            var phase = SeedOffset(settings.Seed).x * 0.001f;
            var center = settings.RiverCenterX + (1400f * math.sin((position.y / 3100f) + phase)) + (500f * noise.snoise(new float2(position.y / 1700f, phase)));
            return math.abs(position.x - center);
        }

        /// <summary>Minimum zonder knik (polynomiale smooth-min), anders ontstaat een vouwlijn in het terrein.</summary>
        private static float SmoothMin(float a, float b, float k)
        {
            var h = math.saturate(0.5f + (0.5f * (b - a) / k));
            return math.lerp(b, a, h) - (k * h * (1f - h));
        }

        /// <summary>Ruis van -1 tot 1 opgebouwd uit octaven.</summary>
        private static float Fbm(float2 p, int octaves)
        {
            var sum = 0f;
            var amplitude = 0.5f;
            var norm = 0f;
            for (var i = 0; i < octaves; i++)
            {
                sum += noise.snoise(p) * amplitude;
                norm += amplitude;
                amplitude *= 0.5f;
                p = (p * 2.03f) + 11.7f;
            }

            return sum / norm;
        }

        /// <summary>Kammen van 0 tot 1: scherpe ruggen waar de ruis door nul gaat, met verfijning op de hogere delen.</summary>
        private static float Ridged(float2 p, int octaves)
        {
            var sum = 0f;
            var amplitude = 0.5f;
            var weight = 1f;
            var norm = 0f;
            for (var i = 0; i < octaves; i++)
            {
                // Gladde absolute waarde: een afgeronde kam in plaats van een knik, die op 8 m als een mes zou ogen.
                var n = noise.snoise(p);
                var ridge = 1f - math.sqrt((n * n) + 0.015f);
                ridge *= ridge;
                ridge *= weight;
                weight = math.saturate(ridge * 1.8f);
                sum += ridge * amplitude;
                norm += amplitude;
                amplitude *= 0.35f;
                p = (p * 2.07f) - 7.3f;
            }

            return sum / norm;
        }

        private static float2 SeedOffset(int seed)
        {
            unchecked
            {
                var h = (uint)seed * 2654435761u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return new float2(h & 0xFFFF, (h >> 16) & 0xFFFF) * 1.37f;
            }
        }
    }
}
