using System;
using Unity.Mathematics;

namespace Cities.Simulation.Animation
{
    /// <summary>
    /// Wielrotatie van een voertuig (E21-05). De hoek volgt uit de afgelegde afstand en de wielstraal, dus een wiel draait nooit door
    /// op een stilstaand voertuig en nooit te snel of te langzaam voor zijn snelheid. De hoek blijft in [0, 2π) zodat hij met
    /// float-precisie bruikbaar blijft na urenlang rijden. De GPU draait de wielen om hun as met deze hoek (zie <c>CityAgent.shader</c>).
    /// </summary>
    public static class WheelSpin
    {
        public const float FullTurn = 2f * math.PI;

        /// <summary>Nieuwe hoek (radialen) nadat het voertuig <paramref name="distance"/> meter heeft afgelegd; achteruit mag (negatieve afstand).</summary>
        public static float Advance(float angle, float distance, float radius)
        {
            if (radius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "De wielstraal moet positief zijn.");
            }

            return Wrap(angle + (distance / radius));
        }

        /// <summary>Toerental in omwentelingen per seconde bij <paramref name="speed"/> m/s.</summary>
        public static float RevolutionsPerSecond(float speed, float radius)
        {
            if (radius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "De wielstraal moet positief zijn.");
            }

            return speed / (FullTurn * radius);
        }

        public static float Wrap(float angle)
        {
            var wrapped = angle - (FullTurn * math.floor(angle / FullTurn));
            return wrapped >= FullTurn ? 0f : wrapped;
        }
    }

    /// <summary>
    /// Looppas van een burger (E21-05). De fase (0..1) loopt op met de afgelegde afstand gedeeld door de staplengte, zodat de voeten
    /// niet over de grond glijden: bij dubbele snelheid loopt de cyclus dubbel zo snel. Stilstaan houdt de fase vast.
    /// </summary>
    public static class WalkCycle
    {
        /// <summary>Afstand van een volledige cyclus (twee stappen) in meters voor een volwassene.</summary>
        public const float StrideMeters = 1.4f;

        /// <summary>Maximale zwaai van been en arm in radialen (ongeveer 30 graden).</summary>
        public const float MaxSwing = 0.52f;

        public static float Advance(float phase, float distance, float scale = 1f)
        {
            if (scale <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(scale), "De schaal moet positief zijn.");
            }

            var next = phase + (distance / (StrideMeters * scale));
            return next - math.floor(next);
        }

        /// <summary>Zwaaihoek van het rechterbeen; het linker is het spiegelbeeld (fase + 0,5).</summary>
        public static float LegSwing(float phase) => math.sin(phase * WheelSpin.FullTurn) * MaxSwing;

        /// <summary>Armen zwaaien tegengesteld aan de benen en iets minder ver.</summary>
        public static float ArmSwing(float phase) => -LegSwing(phase) * 0.7f;

        /// <summary>Hoogte van het lichaam: twee keer per cyclus een kleine dip, het laagst als beide voeten op de grond staan.</summary>
        public static float BodyBob(float phase, float amplitude = 0.02f) => ((math.cos(phase * 2f * WheelSpin.FullTurn) * 0.5f) - 0.5f) * amplitude;
    }

    /// <summary>Twee naastliggende frames van een gebakken clip en de weging tussen beide.</summary>
    public readonly struct FrameBlend : IEquatable<FrameBlend>
    {
        public FrameBlend(int from, int to, float weight)
        {
            From = from;
            To = to;
            Weight = weight;
        }

        public int From { get; }

        public int To { get; }

        /// <summary>0 = alleen <see cref="From"/>, 1 = alleen <see cref="To"/>.</summary>
        public float Weight { get; }

        public bool Equals(FrameBlend other) => From == other.From && To == other.To && Weight.CompareTo(other.Weight) == 0;

        public override bool Equals(object obj) => obj is FrameBlend other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(From, To, Weight);
    }

    /// <summary>
    /// Afspelen van een gebakken vertex-animatie (E21-05): de shader leest hoekpuntposities uit een textuur met één rij per frame.
    /// Dit rekent de twee frames en de weging uit voor een fase (0..1), zodat de GPU zacht tussen frames mengt.
    /// </summary>
    public static class ClipSampler
    {
        /// <summary>Frames voor een fase in een lus: na het laatste frame volgt het eerste.</summary>
        public static FrameBlend Loop(float phase, int frameCount)
        {
            if (frameCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(frameCount), "Een clip heeft minstens één frame.");
            }

            var position = (phase - math.floor(phase)) * frameCount;
            var from = math.min((int)math.floor(position), frameCount - 1);
            return new FrameBlend(from, (from + 1) % frameCount, position - from);
        }

        /// <summary>Zelfde als <see cref="Loop"/>, maar het laatste frame blijft staan (eenmalige animatie).</summary>
        public static FrameBlend Once(float phase, int frameCount)
        {
            if (frameCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(frameCount), "Een clip heeft minstens één frame.");
            }

            if (phase >= 1f)
            {
                return new FrameBlend(frameCount - 1, frameCount - 1, 0f);
            }

            var position = math.max(0f, phase) * (frameCount - 1);
            var from = math.min((int)math.floor(position), frameCount - 1);
            return new FrameBlend(from, math.min(from + 1, frameCount - 1), position - from);
        }
    }

    /// <summary>Afmetingen van de textuur waarin een clip gebakken wordt: breedte = hoekpunten, hoogte = frames.</summary>
    public readonly struct BakedClipLayout
    {
        /// <summary>Grootste textuurzijde die we aannemen (alle doel-GPU's ondersteunen dit).</summary>
        public const int MaxTextureSize = 8192;

        public BakedClipLayout(int vertexCount, int frameCount)
        {
            if (vertexCount < 1 || vertexCount > MaxTextureSize)
            {
                throw new ArgumentOutOfRangeException(nameof(vertexCount), $"Een gebakken mesh heeft 1 tot {MaxTextureSize} hoekpunten.");
            }

            if (frameCount < 1 || frameCount > MaxTextureSize)
            {
                throw new ArgumentOutOfRangeException(nameof(frameCount), $"Een clip heeft 1 tot {MaxTextureSize} frames.");
            }

            VertexCount = vertexCount;
            FrameCount = frameCount;
        }

        public int VertexCount { get; }

        public int FrameCount { get; }

        /// <summary>Geheugen in bytes bij vier halve floats (8 byte) per texel.</summary>
        public long Bytes => (long)VertexCount * FrameCount * 8;

        /// <summary>Index van het texel van een hoekpunt in een frame, rij na rij.</summary>
        public int TexelIndex(int vertex, int frame)
        {
            if (vertex < 0 || vertex >= VertexCount)
            {
                throw new ArgumentOutOfRangeException(nameof(vertex));
            }

            if (frame < 0 || frame >= FrameCount)
            {
                throw new ArgumentOutOfRangeException(nameof(frame));
            }

            return (frame * VertexCount) + vertex;
        }

        /// <summary>Het aantal frames waarmee een cyclus van <paramref name="seconds"/> seconden op <paramref name="framesPerSecond"/> wordt gebakken.</summary>
        public static int FramesFor(float seconds, float framesPerSecond) => math.max(1, (int)math.round(seconds * framesPerSecond));
    }

    /// <summary>
    /// Faseverschil per instance (E21-05), zodat een menigte niet in de pas loopt en elke voetganger een eigen startfase heeft.
    /// Deterministisch uit het id, dus identiek na laden en tussen machines.
    /// </summary>
    public static class AnimationPhase
    {
        public static float Offset(uint id)
        {
            // Wang-hash: goede spreiding, geen state en geen System.Random.
            var h = id;
            h = (h ^ 61u) ^ (h >> 16);
            h *= 9u;
            h ^= h >> 4;
            h *= 0x27d4eb2du;
            h ^= h >> 15;
            return (h & 0xFFFFFFu) / (float)0x1000000;
        }
    }

    /// <summary>Roteert een hoekpunt van een ledemaat om zijn scharnier; gebruikt door de bakker en gespiegeld in de shader.</summary>
    public static class LimbPose
    {
        /// <summary>Draait <paramref name="position"/> om de X-as door het scharnier <paramref name="pivotYz"/> (y, z) over <paramref name="angle"/> radialen.</summary>
        public static float3 RotateAroundX(float3 position, float2 pivotYz, float angle)
        {
            var s = math.sin(angle);
            var c = math.cos(angle);
            var y = position.y - pivotYz.x;
            var z = position.z - pivotYz.y;
            return new float3(position.x, pivotYz.x + (y * c) - (z * s), pivotYz.y + (y * s) + (z * c));
        }
    }
}
