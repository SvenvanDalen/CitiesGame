using System.Collections.Generic;
using Unity.Mathematics;

namespace Cities.World.Fauna
{
    /// <summary>Hoekpunten, normalen en driehoeken van een model; de renderer maakt er een mesh van.</summary>
    public sealed class FaunaShape
    {
        public FaunaShape(float3[] vertices, float3[] normals, int[] triangles)
        {
            Vertices = vertices;
            Normals = normals;
            Triangles = triangles;
        }

        public float3[] Vertices { get; }

        public float3[] Normals { get; }

        public int[] Triangles { get; }

        /// <summary>Kleinste en grootste hoek van het omhullende blok.</summary>
        public (float3 Min, float3 Max) Bounds()
        {
            var min = new float3(float.MaxValue);
            var max = new float3(float.MinValue);
            foreach (var v in Vertices)
            {
                min = math.min(min, v);
                max = math.max(max, v);
            }

            return (min, max);
        }
    }

    /// <summary>
    /// De modellen van de dieren op ware grootte (meters, +z is vooruit, de oorsprong ligt onder de buik op de grond;
    /// bij vogels in het midden van het lijf). Ze zijn opgebouwd uit ellipsoïden, buizen en platte vlakken en hebben
    /// weinig driehoeken, omdat er honderden tegelijk getekend worden.
    /// </summary>
    public static class FaunaShapes
    {
        /// <summary>Lijf, kop, snavel en staart van een meeuw (1,3 m spanwijdte met de vleugels erbij).</summary>
        public static FaunaShape BirdBody()
        {
            var b = new Builder();
            b.Ellipsoid(new float3(0f, 0f, 0f), new float3(0.075f, 0.07f, 0.26f), 10, 14);
            b.Ellipsoid(new float3(0f, 0.04f, 0.27f), new float3(0.045f, 0.045f, 0.055f), 8, 10);
            b.Tube(new float3(0f, 0.035f, 0.32f), new float3(0f, 0.025f, 0.38f), 0.014f, 0.003f, 6);
            b.Plate(new[] { new float3(-0.05f, 0f, -0.2f), new float3(0.05f, 0f, -0.2f), new float3(0.07f, 0f, -0.38f), new float3(-0.07f, 0f, -0.38f) });
            return b.Build();
        }

        /// <summary>De rechtervleugel (+x); de linker is de spiegel, de renderer draait hem om de z-as.</summary>
        public static FaunaShape BirdWing()
        {
            var b = new Builder();
            b.Plate(new[]
            {
                new float3(0.05f, 0f, 0.10f),
                new float3(0.35f, 0.01f, 0.13f),
                new float3(0.72f, 0.02f, -0.02f),
                new float3(0.70f, 0.02f, -0.10f),
                new float3(0.35f, 0.01f, -0.10f),
                new float3(0.05f, 0f, -0.14f),
            });
            return b.Build();
        }

        /// <summary>Een voorn van zo'n 30 cm: lijf, staartvin en rugvin.</summary>
        public static FaunaShape Fish()
        {
            var b = new Builder();
            b.Ellipsoid(float3.zero, new float3(0.035f, 0.06f, 0.15f), 8, 12);
            b.Plate(new[] { new float3(0f, 0f, -0.12f), new float3(0f, 0.07f, -0.24f), new float3(0f, -0.07f, -0.24f) });
            b.Plate(new[] { new float3(0f, 0.05f, 0.04f), new float3(0f, 0.11f, -0.05f), new float3(0f, 0.05f, -0.07f) });
            return b.Build();
        }

        /// <summary>Een ree van 1,1 m lang en 0,75 m schofthoogte, met de oorsprong op de grond onder de buik.</summary>
        public static FaunaShape Deer()
        {
            var b = new Builder();
            b.Ellipsoid(new float3(0f, 0.62f, 0f), new float3(0.14f, 0.17f, 0.40f), 10, 16);
            b.Tube(new float3(0f, 0.70f, 0.30f), new float3(0f, 0.93f, 0.50f), 0.075f, 0.045f, 8);
            b.Ellipsoid(new float3(0f, 0.95f, 0.58f), new float3(0.045f, 0.055f, 0.11f), 8, 10);
            b.Plate(new[] { new float3(0.03f, 1.00f, 0.52f), new float3(0.10f, 1.10f, 0.50f), new float3(0.05f, 0.98f, 0.46f) });
            b.Plate(new[] { new float3(-0.03f, 1.00f, 0.52f), new float3(-0.10f, 1.10f, 0.50f), new float3(-0.05f, 0.98f, 0.46f) });
            b.Tube(new float3(0f, 0.72f, -0.38f), new float3(0f, 0.62f, -0.46f), 0.03f, 0.015f, 5);

            // Poten: voor onder de schouder, achter onder de heup, slank naar de hoef.
            foreach (var side in new[] { -1f, 1f })
            {
                b.Tube(new float3(0.07f * side, 0.50f, 0.28f), new float3(0.07f * side, 0f, 0.28f), 0.04f, 0.014f, 6);
                b.Tube(new float3(0.08f * side, 0.55f, -0.30f), new float3(0.08f * side, 0f, -0.34f), 0.05f, 0.014f, 6);
            }

            return b.Build();
        }

        private sealed class Builder
        {
            private readonly List<float3> _vertices = new List<float3>();
            private readonly List<float3> _normals = new List<float3>();
            private readonly List<int> _triangles = new List<int>();

            public FaunaShape Build() => new FaunaShape(_vertices.ToArray(), _normals.ToArray(), _triangles.ToArray());

            public void Ellipsoid(float3 center, float3 radii, int rings, int segments)
            {
                var start = _vertices.Count;
                for (var r = 0; r <= rings; r++)
                {
                    var polar = math.PI * r / rings;
                    for (var s = 0; s <= segments; s++)
                    {
                        var azimuth = 2f * math.PI * s / segments;
                        var unit = new float3(math.sin(polar) * math.cos(azimuth), math.cos(polar), math.sin(polar) * math.sin(azimuth));

                        // De lange as ligt langs z: wissel y en z, zodat radii.z de lengte is.
                        var direction = unit.xzy;
                        _vertices.Add(center + (direction * radii));
                        _normals.Add(math.normalize(direction / radii));
                    }
                }

                Grid(start, rings, segments);
            }

            public void Tube(float3 from, float3 to, float radiusFrom, float radiusTo, int segments)
            {
                var axis = math.normalize(to - from);
                var side = math.normalize(math.cross(axis, math.abs(axis.y) > 0.9f ? new float3(0f, 0f, 1f) : new float3(0f, 1f, 0f)));
                var up = math.cross(axis, side);
                var start = _vertices.Count;
                for (var ring = 0; ring <= 1; ring++)
                {
                    for (var s = 0; s <= segments; s++)
                    {
                        var angle = 2f * math.PI * s / segments;
                        var radial = (side * math.cos(angle)) + (up * math.sin(angle));
                        _vertices.Add(math.lerp(from, to, ring) + (radial * (ring == 0 ? radiusFrom : radiusTo)));
                        _normals.Add(radial);
                    }
                }

                Grid(start, 1, segments, true);
            }

            /// <summary>Vlak veelvlak (convex, in volgorde rond de rand), dubbelzijdig getekend.</summary>
            public void Plate(float3[] corners)
            {
                var normal = math.normalize(math.cross(corners[1] - corners[0], corners[2] - corners[0]));
                var front = _vertices.Count;
                foreach (var corner in corners)
                {
                    _vertices.Add(corner);
                    _normals.Add(normal);
                }

                var back = _vertices.Count;
                foreach (var corner in corners)
                {
                    _vertices.Add(corner);
                    _normals.Add(-normal);
                }

                for (var i = 1; i < corners.Length - 1; i++)
                {
                    _triangles.AddRange(new[] { front, front + i, front + i + 1, back, back + i + 1, back + i });
                }
            }

            private void Grid(int start, int rings, int segments, bool flip = false)
            {
                var stride = segments + 1;
                for (var r = 0; r < rings; r++)
                {
                    for (var s = 0; s < segments; s++)
                    {
                        var a = start + (r * stride) + s;
                        var b = a + stride;
                        _triangles.AddRange(flip ? new[] { a, a + 1, b, a + 1, b + 1, b } : new[] { a, b, a + 1, a + 1, b, b + 1 });
                    }
                }
            }
        }
    }
}
