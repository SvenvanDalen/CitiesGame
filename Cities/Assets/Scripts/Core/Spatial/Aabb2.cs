using System;
using Unity.Mathematics;

namespace Cities.Core.Spatial
{
    /// <summary>
    /// Asgebonden rechthoek in het grondvlak. <c>x</c> is wereld-X, <c>y</c> is wereld-Z.
    /// </summary>
    public readonly struct Aabb2 : IEquatable<Aabb2>
    {
        public Aabb2(float2 min, float2 max)
        {
            Min = math.min(min, max);
            Max = math.max(min, max);
        }

        public float2 Min { get; }

        public float2 Max { get; }

        public float2 Center => (Min + Max) * 0.5f;

        public float2 Size => Max - Min;

        public static Aabb2 FromCenter(float2 center, float2 halfExtents) => new Aabb2(center - halfExtents, center + halfExtents);

        public static Aabb2 FromCircle(float2 center, float radius) => FromCenter(center, new float2(radius));

        public bool Contains(float2 point) => math.all(point >= Min) && math.all(point <= Max);

        public bool Overlaps(Aabb2 other) => math.all(Min <= other.Max) && math.all(other.Min <= Max);

        /// <summary>Kwadratische afstand van een punt tot de rechthoek; 0 als het punt erin ligt.</summary>
        public float DistanceSq(float2 point)
        {
            var nearest = math.clamp(point, Min, Max);
            return math.distancesq(point, nearest);
        }

        public bool OverlapsCircle(float2 center, float radius) => DistanceSq(center) <= radius * radius;

        /// <summary>
        /// Snijpunt van een straal met de rechthoek (slab-methode). <paramref name="distance"/> is de afstand langs de
        /// straal tot het eerste raakpunt, 0 als de oorsprong binnen de rechthoek ligt.
        /// </summary>
        /// <param name="direction">Genormaliseerde richting.</param>
        public bool Raycast(float2 origin, float2 direction, float maxDistance, out float distance)
        {
            var inverse = 1f / direction;
            var t0 = (Min - origin) * inverse;
            var t1 = (Max - origin) * inverse;
            var tNear = math.min(t0, t1);
            var tFar = math.max(t0, t1);

            // Een richtingcomponent van 0 geeft ±oneindig of NaN; dan bepaalt alleen de andere as.
            var enter = math.cmax(math.select(tNear, float.NegativeInfinity, math.isnan(tNear)));
            var exit = math.cmin(math.select(tFar, float.PositiveInfinity, math.isnan(tFar)));
            distance = math.max(enter, 0f);
            return exit >= distance && distance <= maxDistance;
        }

        public Aabb2 Encapsulate(Aabb2 other) => new Aabb2(math.min(Min, other.Min), math.max(Max, other.Max));

        public bool Equals(Aabb2 other) => math.all(Min == other.Min) && math.all(Max == other.Max);

        public override bool Equals(object obj) => obj is Aabb2 other && Equals(other);

        public override int GetHashCode() => (int)math.hash(new float4(Min, Max));

        public override string ToString() => $"[{Min.x}, {Min.y}]–[{Max.x}, {Max.y}]";

        public static bool operator ==(Aabb2 left, Aabb2 right) => left.Equals(right);

        public static bool operator !=(Aabb2 left, Aabb2 right) => !left.Equals(right);
    }
}
