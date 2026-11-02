using System;
using System.Collections.Generic;
using Cities.Core.Randomness;
using Unity.Mathematics;

namespace Cities.Simulation.Disasters
{
    /// <summary>
    /// Tornado (E15-04): een windbuis die met <see cref="SpeedPerSecond"/> over de kaart trekt en onderweg een beetje van koers afwijkt
    /// (deterministisch uit de seed). Elke stap legt een punt op het pad vast; de schade aan een plek hangt af van de afstand tot dat pad,
    /// de breedte van de buis en de kracht (1 tot 5, zoals de EF-schaal). Bij kracht 5 stort de kern van het pad in.
    /// </summary>
    public sealed class Tornado
    {
        public const int MaxStrength = 5;

        /// <summary>Voortgang van de buis in meter per seconde.</summary>
        public const float SpeedPerSecond = 15f;

        /// <summary>Straal van de schadezone per krachtpunt, in meter.</summary>
        public const float RadiusPerStrength = 50f;

        /// <summary>Grootste koersafwijking per stap, in radialen.</summary>
        public const float MaxWobble = 0.15f;

        // shortcut: platte schadezone zonder rekening te houden met gebouwen of terrein; koppelen aan de gebouwen (en het Ledger) zodra die in de wereld staan.
        /// <summary>Een tornado komt zonder waarschuwing, is snel voorbij en laat een korte nasleep achter.</summary>
        public static DisasterSpec Spec { get; } = new DisasterSpec("Tornado", 0.0003f, 0, 1, 5, 50_000, 10_000);

        private readonly List<float2> _path = new List<float2>();
        private readonly ulong _seed;
        private float2 _heading;
        private int _steps;

        public Tornado(float2 start, float2 heading, int strength, ulong seed)
        {
            if (strength < 1 || strength > MaxStrength)
            {
                throw new ArgumentOutOfRangeException(nameof(strength), strength, "De kracht ligt tussen 1 en 5.");
            }

            if (!(math.lengthsq(heading) > 0f))
            {
                throw new ArgumentException("De koers heeft een richting nodig.", nameof(heading));
            }

            _heading = math.normalize(heading);
            _seed = seed;
            Strength = strength;
            _path.Add(start);
        }

        public int Strength { get; }

        public float Radius => RadiusPerStrength * Strength;

        public float2 Position => _path[_path.Count - 1];

        public float2 Heading => _heading;

        /// <summary>De punten waar de buis langs is gekomen, het eerste is de start.</summary>
        public IReadOnlyList<float2> Path => _path;

        /// <summary>Laat de tornado <paramref name="seconds"/> seconden voortbewegen met een kleine koersafwijking.</summary>
        public void Step(float seconds)
        {
            if (!(seconds >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "De tijd kan niet negatief zijn.");
            }

            var unit = ((SeedHash.Combine(_seed, (ulong)_steps) >> 40) / (float)(1 << 24) * 2f) - 1f;
            _steps++;
            math.sincos(unit * MaxWobble, out var sin, out var cos);
            _heading = new float2((_heading.x * cos) - (_heading.y * sin), (_heading.x * sin) + (_heading.y * cos));
            _path.Add(Position + (_heading * SpeedPerSecond * seconds));
        }

        /// <summary>Afstand van <paramref name="point"/> tot het afgelegde pad.</summary>
        public float DistanceToPath(float2 point)
        {
            var nearest = math.distance(point, _path[0]);
            for (var i = 1; i < _path.Count; i++)
            {
                nearest = math.min(nearest, DistanceToSegment(point, _path[i - 1], _path[i]));
            }

            return nearest;
        }

        /// <summary>Schade van 0 (niets) tot 1 (ingestort) op een plek: neemt af naar de rand van de zone en groeit met de kracht.</summary>
        public float DamageAt(float2 point) =>
            math.saturate((1f - (DistanceToPath(point) / Radius)) * (0.4f + (0.2f * Strength)));

        private static float DistanceToSegment(float2 point, float2 from, float2 to)
        {
            var segment = to - from;
            var length = math.lengthsq(segment);
            var t = length > 0f ? math.saturate(math.dot(point - from, segment) / length) : 0f;
            return math.distance(point, from + (segment * t));
        }
    }
}
