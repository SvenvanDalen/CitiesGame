using System;
using Cities.Core.Randomness;
using Unity.Mathematics;

namespace Cities.Simulation.Fields
{
    /// <summary>Wind op één moment: de richting waarheen hij waait (radialen, 0 is +x/oost, tegen de klok in naar +z/noord) en de snelheid in m/s.</summary>
    public readonly struct Wind
    {
        public Wind(float direction, float speed)
        {
            Direction = direction;
            Speed = math.max(0f, speed);
        }

        public float Direction { get; }

        public float Speed { get; }

        /// <summary>Kracht van 0 (windstil) tot 1 (<see cref="WindModel.MaxSpeed"/> of meer).</summary>
        public float Strength => math.saturate(Speed / WindModel.MaxSpeed);

        /// <summary>Richting met lengte <see cref="Strength"/>; dit is de invoer van <c>FireSpread.Step</c>.</summary>
        public float2 Vector
        {
            get
            {
                math.sincos(Direction, out var sin, out var cos);
                return new float2(cos, sin) * Strength;
            }
        }

        /// <summary>De verschuiving per diffusiestap voor <see cref="DiffusionSettings"/>: bij kracht 1 schuift de waarde <paramref name="maxCellsPerStep"/> cellen mee.</summary>
        public float2 CellsPerStep(float maxCellsPerStep) => Vector * maxCellsPerStep;
    }

    /// <summary>
    /// Wind als wereldwaarde (E12-07). De wind is een zuivere functie van wereldseed en tick, dus er is niets op te slaan: de seed staat al in
    /// sectie <c>RAND</c> en na laden waait dezelfde wind. Om de <see cref="KnotTicks"/> ticks hoort er een steunpunt met een richting rond de
    /// heersende windrichting van deze wereld en een snelheid (vaker zacht dan stormachtig); ertussen loopt de wind vloeiend. De getallen zijn schattingen.
    /// </summary>
    public static class WindModel
    {
        /// <summary>Snelheid in m/s waarbij de kracht 1 is.</summary>
        public const float MaxSpeed = 20f;

        /// <summary>Spreiding (radialen) van de richting rond de heersende windrichting.</summary>
        public const float DirectionSpread = 0.9f;

        /// <summary>Ticks tussen twee steunpunten; bij 100 ticks per dag is dat drie dagen.</summary>
        public const int KnotTicks = 300;

        private static readonly RandomStreamKey PrevailingStream = RandomStreamKey.Of("Wind.Prevailing");
        private static readonly RandomStreamKey KnotStream = RandomStreamKey.Of("Wind.Knot");

        /// <summary>De windrichting (radialen, 0 tot 2 pi) die in deze wereld het vaakst waait.</summary>
        public static float PrevailingDirection(ulong seed) =>
            new RandomSource(seed, PrevailingStream).At(0).NextFloat(0f, 2f * math.PI);

        public static Wind At(ulong seed, long tick)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "De tick is negatief.");
            }

            var knot = tick / KnotTicks;
            var t = (tick % KnotTicks) / (float)KnotTicks;
            t = t * t * (3f - (2f * t));
            var from = Knot(seed, knot);
            var to = Knot(seed, knot + 1);

            // Kortste weg tussen de twee richtingen, zodat de wind niet om de verkeerde kant draait.
            var turn = math.fmod(to.Direction - from.Direction + math.PI, 2f * math.PI);
            if (turn < 0f)
            {
                turn += 2f * math.PI;
            }

            turn -= math.PI;
            return new Wind(from.Direction + (turn * t), math.lerp(from.Speed, to.Speed, t));
        }

        private static Wind Knot(ulong seed, long knot)
        {
            var random = new RandomSource(seed, KnotStream).At(knot);
            var direction = PrevailingDirection(seed) + random.NextGaussian(0f, DirectionSpread);
            var u = random.NextFloat();
            return new Wind(direction, MaxSpeed * 0.8f * u * u);
        }
    }
}
