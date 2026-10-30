using Unity.Mathematics;

namespace Cities.Simulation.Network
{
    /// <summary>Kubische Bezier-curve in wereldmeters; de vorm van een wegsegment, een lane of een verbinding.</summary>
    public readonly struct Bezier3
    {
        public Bezier3(float3 start, float3 startHandle, float3 endHandle, float3 end)
        {
            P0 = start;
            P1 = startHandle;
            P2 = endHandle;
            P3 = end;
        }

        public float3 P0 { get; }

        public float3 P1 { get; }

        public float3 P2 { get; }

        public float3 P3 { get; }

        public Bezier3 Reversed => new Bezier3(P3, P2, P1, P0);

        public static Bezier3 Straight(float3 from, float3 to) => new Bezier3(from, math.lerp(from, to, 1f / 3f), math.lerp(from, to, 2f / 3f), to);

        /// <summary>
        /// Curve die bij <paramref name="from"/> in <paramref name="startDirection"/> vertrekt en bij <paramref name="to"/>
        /// in <paramref name="endDirection"/> aankomt; de handvatten zijn een derde van de afstand, wat een cirkelboog
        /// van 90° goed benadert.
        /// </summary>
        public static Bezier3 FromTangents(float3 from, float3 startDirection, float3 to, float3 endDirection)
        {
            var handle = math.distance(from, to) / 3f;
            return new Bezier3(from, from + (math.normalizesafe(startDirection) * handle), to - (math.normalizesafe(endDirection) * handle), to);
        }

        public float3 Position(float t)
        {
            var u = 1f - t;
            return (u * u * u * P0) + (3f * u * u * t * P1) + (3f * u * t * t * P2) + (t * t * t * P3);
        }

        /// <summary>Afgeleide naar t; de richting van de curve (niet genormaliseerd).</summary>
        public float3 Derivative(float t)
        {
            var u = 1f - t;
            return (3f * u * u * (P1 - P0)) + (6f * u * t * (P2 - P1)) + (3f * t * t * (P3 - P2));
        }

        /// <summary>Eenheidsrichting op <paramref name="t"/>; bij samenvallende handvatten de richting van begin naar eind.</summary>
        public float3 Direction(float t)
        {
            var derivative = Derivative(t);
            return math.lengthsq(derivative) > 1e-8f ? math.normalize(derivative) : math.normalizesafe(P3 - P0);
        }

        /// <summary>Lengte langs de curve, benaderd met <paramref name="steps"/> rechte stukjes.</summary>
        public float Length(int steps = 16)
        {
            var length = 0f;
            var previous = P0;
            for (var i = 1; i <= steps; i++)
            {
                var point = Position(i / (float)steps);
                length += math.distance(previous, point);
                previous = point;
            }

            return length;
        }

        /// <summary>
        /// Evenwijdige curve op <paramref name="offset"/> meter rechts van de rijrichting (links bij een negatieve
        /// waarde), in het horizontale vlak. Elk punt schuift langs de normaal van het dichtstbijzijnde uiteinde; dat
        /// is voor wegbochten nauwkeurig genoeg en houdt de uiteinden precies op de goede plek.
        /// </summary>
        public Bezier3 Offset(float offset)
        {
            var shiftA = Right(Direction(0f)) * offset;
            var shiftB = Right(Direction(1f)) * offset;
            return new Bezier3(P0 + shiftA, P1 + shiftA, P2 + shiftB, P3 + shiftB);
        }

        /// <summary>Horizontale eenheidsvector rechts van <paramref name="direction"/>.</summary>
        public static float3 Right(float3 direction) => math.normalizesafe(math.cross(math.up(), direction));
    }
}
