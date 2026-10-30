using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cities.World.Roads
{
    public static class RoadMeshExtensions
    {
        /// <summary>Vult <paramref name="mesh"/> met de geometrie, met een submesh per <see cref="RoadSurface"/>.</summary>
        public static void CopyTo(this RoadMeshData data, Mesh mesh)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (mesh == null)
            {
                throw new ArgumentNullException(nameof(mesh));
            }

            var vertices = new Vector3[data.Vertices.Count];
            var normals = new Vector3[data.Vertices.Count];
            var uvs = new Vector2[data.Vertices.Count];
            for (var i = 0; i < vertices.Length; i++)
            {
                vertices[i] = data.Vertices[i];
                normals[i] = data.Normals[i];
                uvs[i] = data.Uvs[i];
            }

            mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = data.Triangles.Length;
            for (var i = 0; i < data.Triangles.Length; i++)
            {
                mesh.SetTriangles(data.Triangles[i], i, calculateBounds: false);
            }

            mesh.RecalculateBounds();
        }
    }
}
