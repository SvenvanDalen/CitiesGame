using Cities.Core.Spatial;
using Unity.Mathematics;

namespace Cities.World.Picking
{
    /// <summary>Vorm in een <see cref="SpatialPickLayer{TShape}"/>.</summary>
    public interface IPickShape
    {
        /// <summary>Object waar de vorm bij hoort; meerdere vormen kunnen bij hetzelfde object horen.</summary>
        ulong Id { get; }

        /// <summary>Omhullende in het grondvlak, vergroot met <paramref name="padding"/> voor de pickmarge.</summary>
        Aabb2 Footprint(float padding);

        /// <summary>
        /// Test de straal. <paramref name="score"/> is 0 bij een echte treffer en loopt tot 1 voor een treffer binnen de
        /// marge; lager is beter.
        /// </summary>
        /// <param name="margin">Extra straal per meter afstand (ongeveer de hoek van een paar pixels), begrensd op <paramref name="maxMargin"/>.</param>
        bool Test(in PickRay ray, float maxDistance, float margin, float maxMargin, out float distance, out float score);
    }

    /// <summary>Doos om de verticale as gedraaid, zoals een gebouw.</summary>
    public readonly struct PickBox : IPickShape
    {
        public PickBox(ulong id, float3 center, float3 halfExtents, float yaw)
        {
            Id = id;
            Center = center;
            HalfExtents = halfExtents;
            Yaw = yaw;
            math.sincos(yaw, out var sin, out var cos);
            Rotation = new float2(sin, cos);
        }

        public ulong Id { get; }

        public float3 Center { get; }

        public float3 HalfExtents { get; }

        /// <summary>Draaiing om de verticale as in radialen, zoals <c>transform.eulerAngles.y</c> (omgerekend).</summary>
        public float Yaw { get; }

        /// <summary>Sinus en cosinus van <see cref="Yaw"/>, één keer berekend.</summary>
        private float2 Rotation { get; }

        public Aabb2 Footprint(float padding)
        {
            var sin = Rotation.x;
            var cos = Rotation.y;
            var extents = new float2(
                (math.abs(cos) * HalfExtents.x) + (math.abs(sin) * HalfExtents.z),
                (math.abs(sin) * HalfExtents.x) + (math.abs(cos) * HalfExtents.z));
            return Aabb2.FromCenter(Center.xz, extents + padding);
        }

        public bool Test(in PickRay ray, float maxDistance, float margin, float maxMargin, out float distance, out float score)
        {
            score = 0f;
            return PickGeometry.RayBox(ray.Origin, ray.Direction, Center, HalfExtents, Rotation, maxDistance, out distance);
        }
    }

    /// <summary>
    /// Lijnstuk met een straal, zoals een stuk weg tussen twee punten van de spline of (met <c>A == B</c>) een
    /// knooppunt. Ver weg wordt de straal groter, zodat een smalle weg aanwijsbaar blijft.
    /// </summary>
    public readonly struct PickCapsule : IPickShape
    {
        public PickCapsule(ulong id, float3 a, float3 b, float radius)
        {
            Id = id;
            A = a;
            B = b;
            Radius = radius;
        }

        public ulong Id { get; }

        public float3 A { get; }

        public float3 B { get; }

        public float Radius { get; }

        public Aabb2 Footprint(float padding) =>
            new Aabb2(math.min(A.xz, B.xz) - (Radius + padding), math.max(A.xz, B.xz) + (Radius + padding));

        public bool Test(in PickRay ray, float maxDistance, float margin, float maxMargin, out float distance, out float score) =>
            PickShapeTests.Fuzzy(ray, new CapsuleAxis(A, B, Radius), maxDistance, new float2(margin, maxMargin), out distance, out score);
    }

    /// <summary>Staande capsule van de voeten tot de hoogte, zoals een burger of een voertuig.</summary>
    public readonly struct PickAgent : IPickShape
    {
        public PickAgent(ulong id, float3 position, float radius, float height)
        {
            Id = id;
            Position = position;
            Radius = radius;
            Height = height;
        }

        public ulong Id { get; }

        /// <summary>Punt op de grond.</summary>
        public float3 Position { get; }

        public float Radius { get; }

        public float Height { get; }

        public Aabb2 Footprint(float padding) => Aabb2.FromCircle(Position.xz, Radius + padding);

        public bool Test(in PickRay ray, float maxDistance, float margin, float maxMargin, out float distance, out float score)
        {
            // De as loopt van de grond tot de bovenkant min de straal, zodat de capsule niet onder de grond steekt.
            var bottom = Position + new float3(0f, math.min(Radius, Height * 0.5f), 0f);
            var top = Position + new float3(0f, math.max(Height - Radius, Height * 0.5f), 0f);
            return PickShapeTests.Fuzzy(ray, new CapsuleAxis(bottom, top, Radius), maxDistance, new float2(margin, maxMargin), out distance, out score);
        }
    }

    /// <summary>As en straal van een capsule (geen tuple, zodat Burst het aankan).</summary>
    internal readonly struct CapsuleAxis
    {
        public CapsuleAxis(float3 a, float3 b, float radius)
        {
            A = a;
            B = b;
            Radius = radius;
        }

        public float3 A { get; }

        public float3 B { get; }

        public float Radius { get; }
    }

    internal static class PickShapeTests
    {
        /// <summary>
        /// Capsule met een straal die groeit met de afstand. Een echte treffer heeft score 0; een treffer binnen de
        /// marge krijgt de afstand tot de as gedeeld door de vergrote straal.
        /// </summary>
        /// <param name="margin">Extra straal per meter afstand (x) en de grootste extra straal (y).</param>
        public static bool Fuzzy(in PickRay ray, in CapsuleAxis capsule, float maxDistance, float2 margin, out float distance, out float score)
        {
            var a = capsule.A;
            var b = capsule.B;
            var radius = capsule.Radius;
            if (PickGeometry.RayCapsule(ray.Origin, ray.Direction, a, b, radius, maxDistance, out distance))
            {
                score = 0f;
                return true;
            }

            var gap = PickGeometry.RaySegmentDistance(ray.Origin, ray.Direction, a, b, maxDistance, out var along);
            var enlarged = radius + math.min(along * margin.x, margin.y);
            distance = along;
            score = gap / enlarged;
            return gap <= enlarged && enlarged > radius;
        }
    }
}
