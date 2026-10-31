using System.Collections.Generic;
using UnityEngine;

namespace Cities.Editor.Models
{
    /// <summary>
    /// Bouwt een mesh uit vlakke vlakken met een submesh per materiaal. Elk vlak krijgt eigen hoekpunten (harde randen)
    /// en UV's in meters langs het vlak, zodat texturen op elke helling dezelfde schaal houden.
    /// </summary>
    internal sealed class ModelMesh
    {
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<int>[] _triangles;

        public ModelMesh(int submeshCount)
        {
            _triangles = new List<int>[submeshCount];
            for (var i = 0; i < submeshCount; i++)
            {
                _triangles[i] = new List<int>();
            }
        }

        /// <summary>Vierhoek in volgorde rond de rand; de draairichting volgt uit <paramref name="outward"/>.</summary>
        public void Quad(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        {
            var normal = Vector3.Cross(b - a, c - a).normalized;
            if (Vector3.Dot(normal, outward) < 0f)
            {
                (b, d) = (d, b);
                normal = -normal;
            }

            var start = Add(normal, a, b, c, d);
            _triangles[submesh].AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }

        public void Triangle(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            var normal = Vector3.Cross(b - a, c - a).normalized;
            if (Vector3.Dot(normal, outward) < 0f)
            {
                (b, c) = (c, b);
                normal = -normal;
            }

            var start = Add(normal, a, b, c);
            _triangles[submesh].AddRange(new[] { start, start + 1, start + 2 });
        }

        /// <summary>
        /// Convex blok uit acht hoeken: 0-3 het ene eindvlak rondom, 4-7 het andere in dezelfde volgorde.
        /// </summary>
        public void Hexahedron(int submesh, IReadOnlyList<Vector3> p)
        {
            var center = Vector3.zero;
            foreach (var corner in p)
            {
                center += corner / 8f;
            }

            Face(submesh, center, p[0], p[1], p[2], p[3]);
            Face(submesh, center, p[4], p[5], p[6], p[7]);
            Face(submesh, center, p[0], p[1], p[5], p[4]);
            Face(submesh, center, p[3], p[2], p[6], p[7]);
            Face(submesh, center, p[0], p[3], p[7], p[4]);
            Face(submesh, center, p[1], p[2], p[6], p[5]);
        }

        /// <summary>Asgericht blok.</summary>
        public void Box(int submesh, Vector3 min, Vector3 max)
        {
            Hexahedron(submesh, new[]
            {
                new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z),
            });
        }

        /// <summary>Staande cilinder (silo, schoorsteen, tank) als veelhoek met <paramref name="segments"/> zijden en een deksel.</summary>
        public void Cylinder(int submesh, Vector3 baseCentre, float radius, float height, int segments = 16)
        {
            var top = baseCentre + (Vector3.up * height);
            for (var i = 0; i < segments; i++)
            {
                var a = Ring(baseCentre, radius, i, segments);
                var b = Ring(baseCentre, radius, i + 1, segments);
                var outward = (a + b) * 0.5f - baseCentre;
                outward.y = 0f;
                Quad(submesh, a, b, b + (Vector3.up * height), a + (Vector3.up * height), outward);
                Triangle(submesh, top, Ring(top, radius, i, segments), Ring(top, radius, i + 1, segments), Vector3.up);
            }
        }

        private static Vector3 Ring(Vector3 centre, float radius, int index, int segments)
        {
            var angle = index * Mathf.PI * 2f / segments;
            return centre + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        /// <summary>
        /// Slaat <paramref name="mesh"/> op als asset, of vult de bestaande asset op <paramref name="path"/> via de
        /// Mesh-API, zodat verwijzingen blijven kloppen. Niet met CopySerialized: dan tekent de GPU tot een herstart de oude vorm.
        /// </summary>
        public static Mesh Save(Mesh mesh, string path)
        {
            var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                UnityEditor.AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            existing.Clear();
            existing.subMeshCount = mesh.subMeshCount;
            existing.SetVertices(mesh.vertices);
            existing.SetNormals(mesh.normals);
            existing.SetTangents(mesh.tangents);
            existing.SetUVs(0, mesh.uv);
            for (var i = 0; i < mesh.subMeshCount; i++)
            {
                existing.SetTriangles(mesh.GetTriangles(i), i);
            }

            existing.RecalculateBounds();
            UnityEditor.EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name, subMeshCount = _triangles.Length };
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            for (var i = 0; i < _triangles.Length; i++)
            {
                mesh.SetTriangles(_triangles[i], i);
            }

            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private void Face(int submesh, Vector3 center, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Quad(submesh, a, b, c, d, ((a + b + c + d) / 4f) - center);
        }

        private int Add(Vector3 normal, params Vector3[] points)
        {
            // Horizontale as langs het vlak en de as omhoog langs de helling, allebei in meters.
            var tangent = Mathf.Abs(normal.y) > 0.99f ? Vector3.right : Vector3.Cross(Vector3.up, normal).normalized;
            var bitangent = Vector3.Cross(normal, tangent);
            if (Mathf.Abs(normal.y) > 0.99f)
            {
                bitangent = Vector3.forward;
            }

            var start = _vertices.Count;
            foreach (var point in points)
            {
                _vertices.Add(point);
                _normals.Add(normal);
                _uvs.Add(new Vector2(Vector3.Dot(point, tangent), Vector3.Dot(point, bitangent)));
            }

            return start;
        }
    }
}
