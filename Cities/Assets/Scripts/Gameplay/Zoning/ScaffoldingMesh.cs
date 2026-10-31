using System.Collections.Generic;
using UnityEngine;

namespace Cities.Gameplay.Zoning
{
    /// <summary>Bakt de balken van <see cref="ScaffoldingLayout"/> tot één mesh (E05-06).</summary>
    public static class ScaffoldingMesh
    {
        public static Mesh Build(List<(Vector3 Min, Vector3 Max)> boxes)
        {
            var vertices = new List<Vector3>(boxes.Count * 24);
            var normals = new List<Vector3>(boxes.Count * 24);
            var triangles = new List<int>(boxes.Count * 36);
            foreach (var (min, max) in boxes)
            {
                AddFace(vertices, normals, triangles, new Vector3(min.x, min.y, min.z), new Vector3(0, max.y - min.y, 0), new Vector3(max.x - min.x, 0, 0), Vector3.back);
                AddFace(vertices, normals, triangles, new Vector3(max.x, min.y, max.z), new Vector3(0, max.y - min.y, 0), new Vector3(min.x - max.x, 0, 0), Vector3.forward);
                AddFace(vertices, normals, triangles, new Vector3(min.x, min.y, max.z), new Vector3(0, max.y - min.y, 0), new Vector3(0, 0, min.z - max.z), Vector3.left);
                AddFace(vertices, normals, triangles, new Vector3(max.x, min.y, min.z), new Vector3(0, max.y - min.y, 0), new Vector3(0, 0, max.z - min.z), Vector3.right);
                AddFace(vertices, normals, triangles, new Vector3(min.x, max.y, min.z), new Vector3(0, 0, max.z - min.z), new Vector3(max.x - min.x, 0, 0), Vector3.up);
                AddFace(vertices, normals, triangles, new Vector3(min.x, min.y, max.z), new Vector3(0, 0, min.z - max.z), new Vector3(max.x - min.x, 0, 0), Vector3.down);
            }

            var mesh = new Mesh { name = "Steiger" };
            mesh.indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // shortcut: een grijs URP Lit-materiaal zonder asset; vervang door een eigen materiaal via de inspector.
        public static Material DefaultMaterial()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Steiger" };
            material.SetColor("_BaseColor", new Color(0.55f, 0.57f, 0.6f));
            return material;
        }

        private static void AddFace(List<Vector3> vertices, List<Vector3> normals, List<int> triangles, Vector3 origin, Vector3 up, Vector3 right, Vector3 normal)
        {
            var start = vertices.Count;
            vertices.Add(origin);
            vertices.Add(origin + up);
            vertices.Add(origin + up + right);
            vertices.Add(origin + right);
            for (var i = 0; i < 4; i++)
            {
                normals.Add(normal);
            }

            // Unity ziet de kloksgewijze kant als voorkant, dus de driehoek draait tegen de rechterhandsnormaal in.
            var flip = Vector3.Dot(Vector3.Cross(up, right), normal) > 0f;
            triangles.AddRange(flip ? new[] { start, start + 1, start + 2, start, start + 2, start + 3 } : new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
        }
    }
}
