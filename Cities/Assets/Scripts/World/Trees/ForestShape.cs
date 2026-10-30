using System;
using Cities.World.Grid;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.World.Trees
{
    /// <summary>Instellingen voor <see cref="ForestShape"/>; maten in meters, hellingen in graden.</summary>
    [Serializable]
    public struct ForestSettings
    {
        public uint Seed;

        [Tooltip("Gemiddelde maat van een bos.")]
        public float PatchSize;

        [Tooltip("Deel van het land dat bos is (0 tot 1).")]
        public float Coverage;

        [Tooltip("Boven deze hoogte groeien geen bomen meer (boomgrens).")]
        public float TreeLine;

        [Tooltip("Op steilere hellingen groeien geen bomen.")]
        public float MaxSlope;

        [Tooltip("Dichtheid van losse bomen en houtwallen buiten de bossen (0 tot 255).")]
        public float Scattered;

        public static ForestSettings Default => new ForestSettings
        {
            Seed = 1,
            PatchSize = 900f,
            Coverage = 0.3f,
            TreeLine = 230f,
            MaxSlope = 34f,
            Scattered = 2f,
        };
    }

    /// <summary>
    /// Bosdichtheid voor een plek in het landschap: grillige bossen met een zachte bosrand, losse bomen in het open
    /// land, en geen bomen op akkers, bebouwing, water, kale rots of boven de boomgrens.
    /// </summary>
    public static class ForestShape
    {
        public static byte Density(float2 position, float height, float slopeDegrees, float waterDepth, GroundCover cover, ForestSettings settings)
        {
            if (cover != GroundCover.Natural || waterDepth > 0.05f)
            {
                return 0;
            }

            var offset = new float2(settings.Seed % 997, settings.Seed % 541) * 37.1f;
            var p = (position / settings.PatchSize) + offset;
            var noise = (0.65f * Unity.Mathematics.noise.snoise(p)) + (0.25f * Unity.Mathematics.noise.snoise(p * 2.7f)) + (0.1f * Unity.Mathematics.noise.snoise(p * 9f));

            // Ruisdrempel zodat ongeveer Coverage van het land bos is; de bosrand loopt over een smalle band.
            var threshold = 0.55f - (settings.Coverage * 1.1f);
            var forest = math.smoothstep(threshold, threshold + 0.12f, noise);
            var habitat = (1f - math.smoothstep(settings.TreeLine - 40f, settings.TreeLine, height)) *
                          (1f - math.smoothstep(settings.MaxSlope - 6f, settings.MaxSlope, slopeDegrees));
            return (byte)math.round(math.saturate(math.max(forest, settings.Scattered / 255f) * habitat) * 255f);
        }
    }
}
