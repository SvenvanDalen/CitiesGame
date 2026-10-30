using System;
using Cities.World.Grid;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.World.Terrain
{
    /// <summary>Grenzen voor <see cref="TerrainSplat"/>; hellingen in graden, hoogtes in meters.</summary>
    [Serializable]
    public struct TerrainSplatSettings
    {
        [Tooltip("Vanaf deze helling begint kale rots.")]
        public float RockSlopeStart;

        [Tooltip("Vanaf deze helling is het helemaal rots.")]
        public float RockSlopeFull;

        [Tooltip("Boven deze hoogte groeit steeds minder gras (boomgrens).")]
        public float RockHeightStart;

        [Tooltip("Boven deze hoogte is vlak terrein ook rots.")]
        public float RockHeightFull;

        [Tooltip("Lager dan dit ligt zand: stranden op zeeniveau.")]
        public float SandHeight;

        [Tooltip("Over zoveel meter gaat zand over in gras.")]
        public float SandFade;

        [Tooltip("Op steilere hellingen blijft geen zand liggen.")]
        public float SandMaxSlope;

        [Tooltip("Vanaf deze waterdiepte in de buurt is de oever zand.")]
        public float ShoreWaterDepth;

        public static TerrainSplatSettings Default => new TerrainSplatSettings
        {
            RockSlopeStart = 30f,
            RockSlopeFull = 44f,
            RockHeightStart = 240f,
            RockHeightFull = 330f,
            SandHeight = 2f,
            SandFade = 3f,
            SandMaxSlope = 14f,
            ShoreWaterDepth = 0.05f,
        };
    }

    /// <summary>
    /// Kiest per cel hoeveel zand, rots, akker en bebouwde grond er ligt; de rest is gras. Natuurlijke grond volgt
    /// helling, hoogte en water; akker en bebouwde grond komen uit
    /// <see cref="GroundCover"/>.
    /// </summary>
    public static class TerrainSplat
    {
        /// <summary>Gewichten (zand, rots, akker, bebouwd), samen hoogstens 1; gras is het restant.</summary>
        /// <param name="waterDepth">Grootste waterdiepte in de buurt van de cel.</param>
        /// <param name="settings">Bewust geen <c>in</c>: een <c>in</c>-verwijzing naar een jobveld crasht in Burst zonder safety checks.</param>
        public static float4 Weights(float slopeDegrees, float height, float waterDepth, GroundCover cover, TerrainSplatSettings settings)
        {
            if (cover == GroundCover.Built)
            {
                return new float4(0f, 0f, 0f, 1f);
            }

            var rock = math.max(
                math.smoothstep(settings.RockSlopeStart, settings.RockSlopeFull, slopeDegrees),
                math.smoothstep(settings.RockHeightStart, settings.RockHeightFull, height));
            if (cover == GroundCover.Field)
            {
                return new float4(0f, rock, 1f - rock, 0f);
            }

            var low = 1f - math.smoothstep(settings.SandHeight, settings.SandHeight + settings.SandFade, height);
            var shore = waterDepth > settings.ShoreWaterDepth ? 1f : 0f;
            var flat = 1f - math.smoothstep(settings.SandMaxSlope - 4f, settings.SandMaxSlope + 4f, slopeDegrees);
            var sand = math.max(low, shore) * flat * (1f - rock);
            return new float4(sand, rock, 0f, 0f);
        }

        /// <summary>Helling van een cel uit de hoogtes van de vier hoeken (zuidwest, zuidoost, noordwest, noordoost).</summary>
        public static float SlopeDegrees(float h00, float h10, float h01, float h11, float cellSize)
        {
            var dx = ((h10 + h11) - (h00 + h01)) / (2f * cellSize);
            var dz = ((h01 + h11) - (h00 + h10)) / (2f * cellSize);
            return math.degrees(math.atan(math.sqrt((dx * dx) + (dz * dz))));
        }

        /// <summary>Gewichten als RGBA-bytes voor de splatmap.</summary>
        public static Color32 Encode(float4 weights)
        {
            var bytes = (int4)math.round(math.saturate(weights) * 255f);
            return new Color32((byte)bytes.x, (byte)bytes.y, (byte)bytes.z, (byte)bytes.w);
        }
    }
}
