using Unity.Mathematics;

namespace Cities.World.Picking
{
    /// <summary>
    /// Straaltesten voor picking. De straal heeft een genormaliseerde richting; afstanden zijn in meters langs de straal.
    /// </summary>
    public static class PickGeometry
    {
        private const float Epsilon = 1e-6f;

        /// <summary>Straal tegen een asgerichte doos (slab-methode). Begint de straal in de doos, dan is de afstand 0.</summary>
        public static bool RayAabb(float3 origin, float3 direction, float3 min, float3 max, float maxDistance, out float distance)
        {
            var inverse = 1f / direction;
            var t0 = (min - origin) * inverse;
            var t1 = (max - origin) * inverse;

            // Een richting van 0 op een as geeft ±oneindig; NaN (0 * oneindig) ontstaat alleen als de oorsprong precies op
            // het vlak ligt en telt dan als binnen.
            var near = math.select(math.min(t0, t1), float.NegativeInfinity, math.isnan(t0) | math.isnan(t1));
            var far = math.select(math.max(t0, t1), float.PositiveInfinity, math.isnan(t0) | math.isnan(t1));
            var enter = math.max(math.cmax(near), 0f);
            var exit = math.min(math.cmin(far), maxDistance);
            distance = enter;
            return enter <= exit;
        }

        /// <summary>
        /// Straal tegen een doos die om de verticale as gedraaid staat, zoals een gebouw. <paramref name="center"/> is
        /// het midden van de doos, <paramref name="yaw"/> de draaiing in radialen zoals Unity om de Y-as draait (positief
        /// is met de klok mee van boven gezien: lokaal +X wijst dan naar wereld -Z bij 90 graden).
        /// </summary>
        public static bool RayBox(float3 origin, float3 direction, float3 center, float3 halfExtents, float yaw, float maxDistance, out float distance)
        {
            math.sincos(yaw, out var sin, out var cos);
            return RayBox(origin, direction, center, halfExtents, new float2(sin, cos), maxDistance, out distance);
        }

        /// <summary>Als <see cref="RayBox(float3, float3, float3, float3, float, float, out float)"/> met vooraf berekende (sinus, cosinus) van de draaiing.</summary>
        public static bool RayBox(float3 origin, float3 direction, float3 center, float3 halfExtents, float2 rotation, float maxDistance, out float distance)
        {
            // Wereld naar lokaal is de inverse van Unity's draaiing om Y.
            var sin = rotation.x;
            var cos = rotation.y;
            var local = origin - center;
            var localOrigin = new float3((local.x * cos) - (local.z * sin), local.y, (local.x * sin) + (local.z * cos));
            var localDirection = new float3((direction.x * cos) - (direction.z * sin), direction.y, (direction.x * sin) + (direction.z * cos));
            return RayAabb(localOrigin, localDirection, -halfExtents, halfExtents, maxDistance, out distance);
        }

        /// <summary>Straal tegen een bol.</summary>
        public static bool RaySphere(float3 origin, float3 direction, float3 center, float radius, float maxDistance, out float distance)
        {
            var offset = origin - center;
            var b = math.dot(offset, direction);
            var c = math.lengthsq(offset) - (radius * radius);
            distance = 0f;
            if (c <= 0f)
            {
                return true;
            }

            var discriminant = (b * b) - c;
            if (b > 0f || discriminant < 0f)
            {
                return false;
            }

            distance = -b - math.sqrt(discriminant);
            return distance <= maxDistance;
        }

        /// <summary>Straal tegen een capsule (lijnstuk <paramref name="a"/>–<paramref name="b"/> met straal), zoals een stuk weg.</summary>
        public static bool RayCapsule(float3 origin, float3 direction, float3 a, float3 b, float radius, float maxDistance, out float distance)
        {
            var axis = b - a;
            var axisLengthSq = math.lengthsq(axis);
            if (axisLengthSq < Epsilon)
            {
                return RaySphere(origin, direction, a, radius, maxDistance, out distance);
            }

            // Eerst de oneindige cilinder; ligt de treffer buiten het lijnstuk, dan een van de eindbollen.
            var offset = origin - a;
            var axisDotDirection = math.dot(axis, direction);
            var axisDotOffset = math.dot(axis, offset);
            var qa = axisLengthSq - (axisDotDirection * axisDotDirection);
            var qb = (axisLengthSq * math.dot(offset, direction)) - (axisDotOffset * axisDotDirection);
            var qc = (axisLengthSq * math.lengthsq(offset)) - (axisDotOffset * axisDotOffset) - (radius * radius * axisLengthSq);
            if (qa > Epsilon)
            {
                var discriminant = (qb * qb) - (qa * qc);
                if (discriminant >= 0f)
                {
                    var t = math.max((-qb - math.sqrt(discriminant)) / qa, 0f);
                    var along = axisDotOffset + (t * axisDotDirection);
                    if (along >= 0f && along <= axisLengthSq && (qc <= 0f || t > 0f))
                    {
                        distance = t;
                        return t <= maxDistance;
                    }
                }
            }

            var hitA = RaySphere(origin, direction, a, radius, maxDistance, out var distanceA);
            var hitB = RaySphere(origin, direction, b, radius, maxDistance, out var distanceB);
            if (hitA && hitB)
            {
                distance = math.min(distanceA, distanceB);
                return true;
            }

            distance = hitA ? distanceA : distanceB;
            return hitA || hitB;
        }

        /// <summary>
        /// Kleinste afstand tussen de straal (vanaf <c>t = 0</c> tot <paramref name="maxDistance"/>) en het lijnstuk
        /// <paramref name="a"/>–<paramref name="b"/>; <paramref name="along"/> is de afstand langs de straal van dat punt.
        /// </summary>
        public static float RaySegmentDistance(float3 origin, float3 direction, float3 a, float3 b, float maxDistance, out float along)
        {
            var axis = b - a;
            var offset = origin - a;
            var axisLengthSq = math.lengthsq(axis);
            var axisDotDirection = math.dot(axis, direction);
            var directionDotOffset = math.dot(direction, offset);
            var axisDotOffset = math.dot(axis, offset);
            var denominator = axisLengthSq - (axisDotDirection * axisDotDirection);

            // Parameters van de dichtstbijzijnde punten op de straal (t) en het lijnstuk (s, 0..1).
            var s = 0f;
            if (axisLengthSq >= Epsilon && denominator > Epsilon)
            {
                s = math.saturate((axisDotOffset - (axisDotDirection * directionDotOffset)) / denominator);
            }

            var t = math.clamp(math.dot(a + (axis * s) - origin, direction), 0f, maxDistance);
            if (axisLengthSq >= Epsilon)
            {
                s = math.saturate(math.dot(origin + (direction * t) - a, axis) / axisLengthSq);
                t = math.clamp(math.dot(a + (axis * s) - origin, direction), 0f, maxDistance);
            }

            along = t;
            return math.distance(origin + (direction * t), a + (axis * s));
        }
    }
}
