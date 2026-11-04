using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cities.World.Buildings
{
    /// <summary>Geometrie van een procedureel gebouw: vier submeshes voor muur, raam, deur en dak.</summary>
    public sealed class BuildingMeshData
    {
        public const int WallSubmesh = 0;
        public const int WindowSubmesh = 1;
        public const int DoorSubmesh = 2;
        public const int RoofSubmesh = 3;
        public const int SubmeshCount = 4;

        public BuildingMeshData()
        {
            Submeshes = new List<int>[SubmeshCount];
            for (var i = 0; i < SubmeshCount; i++)
            {
                Submeshes[i] = new List<int>();
            }
        }

        public List<Vector3> Vertices { get; } = new List<Vector3>();

        public List<Vector3> Normals { get; } = new List<Vector3>();

        public List<Vector2> Uvs { get; } = new List<Vector2>();

        public List<int>[] Submeshes { get; }

        public int TriangleCount
        {
            get
            {
                var count = 0;
                foreach (var submesh in Submeshes)
                {
                    count += submesh.Count / 3;
                }

                return count;
            }
        }
    }

    /// <summary>
    /// Zet de plaatsingen van <see cref="ProceduralBuilding"/> om in geometrie (E21-06): elk bouwdeel is een balk, een
    /// dakvlak een gekantelde balk en een gevelpunt een driehoekig prisma. De driehoeken staan met de klok mee gezien
    /// vanaf de buitenkant, zoals Unity wil. Pure berekening zonder Unity-objecten, dus te testen en op een thread te
    /// maken.
    /// </summary>
    public static class ProceduralBuildingMesh
    {
        public static BuildingMeshData Build(ProceduralBuildingSpec spec)
        {
            var data = new BuildingMeshData();
            foreach (var module in ProceduralBuilding.Build(spec))
            {
                AddModule(data, module);
            }

            return data;
        }

        public static int SubmeshFor(BuildingModuleKind kind)
        {
            switch (kind)
            {
                case BuildingModuleKind.Window:
                    return BuildingMeshData.WindowSubmesh;
                case BuildingModuleKind.Door:
                    return BuildingMeshData.DoorSubmesh;
                case BuildingModuleKind.FlatRoof:
                case BuildingModuleKind.RoofSlope:
                case BuildingModuleKind.Gable:
                    return BuildingMeshData.RoofSubmesh;
                default:
                    return BuildingMeshData.WallSubmesh;
            }
        }

        private static void AddModule(BuildingMeshData data, BuildingModule module)
        {
            var yaw = module.YawDegrees * Mathf.Deg2Rad;
            var normal = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            var tangent = new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw));
            var center = new Vector3(module.X, module.Y, module.Z);
            var submesh = data.Submeshes[SubmeshFor(module.Kind)];

            switch (module.Kind)
            {
                case BuildingModuleKind.RoofSlope:
                {
                    var pitch = ProceduralBuilding.RoofPitchDegrees * Mathf.Deg2Rad;
                    var down = (normal * Mathf.Cos(pitch)) - (Vector3.up * Mathf.Sin(pitch));
                    var surface = (normal * Mathf.Sin(pitch)) + (Vector3.up * Mathf.Cos(pitch));
                    AddBox(data, submesh, center, tangent * (module.Width * 0.5f), down * (module.Height * 0.5f), surface * (module.Depth * 0.5f));
                    break;
                }

                case BuildingModuleKind.Gable:
                    AddGable(data, submesh, center, tangent, normal, module);
                    break;

                default:
                    AddBox(data, submesh, center, tangent * (module.Width * 0.5f), Vector3.up * (module.Height * 0.5f), normal * (module.Depth * 0.5f));
                    break;
            }
        }

        /// <summary>Balk met drie halve assen; de zes zijden krijgen normalen naar buiten.</summary>
        private static void AddBox(BuildingMeshData data, List<int> submesh, Vector3 center, Vector3 ax, Vector3 ay, Vector3 az)
        {
            AddFace(data, submesh, center + az, ax, ay);
            AddFace(data, submesh, center - az, -ax, ay);
            AddFace(data, submesh, center + ax, -az, ay);
            AddFace(data, submesh, center - ax, az, ay);
            AddFace(data, submesh, center + ay, ax, -az);
            AddFace(data, submesh, center - ay, ax, az);
        }

        /// <summary>Eén vlak met middelpunt en twee halve assen; de normaal is de kruisrichting, het winding wordt daarop gezet.</summary>
        private static void AddFace(BuildingMeshData data, List<int> submesh, Vector3 center, Vector3 u, Vector3 v)
        {
            var normal = Vector3.Cross(u, v).normalized;
            AddQuad(data, submesh, center - u - v, center - u + v, center + u + v, center + u - v, normal);
        }

        private static void AddQuad(BuildingMeshData data, List<int> submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            var start = data.Vertices.Count;
            data.Vertices.Add(a);
            data.Vertices.Add(b);
            data.Vertices.Add(c);
            data.Vertices.Add(d);
            for (var i = 0; i < 4; i++)
            {
                data.Normals.Add(normal);
            }

            data.Uvs.Add(new Vector2(0f, 0f));
            data.Uvs.Add(new Vector2(0f, 1f));
            data.Uvs.Add(new Vector2(1f, 1f));
            data.Uvs.Add(new Vector2(1f, 0f));
            AddTriangle(data, submesh, start, start + 1, start + 2, normal);
            AddTriangle(data, submesh, start, start + 2, start + 3, normal);
        }

        /// <summary>Zet de volgorde zo dat de kruisrichting van de driehoek met de normaal meewijst (Unity: met de klok mee van buiten).</summary>
        private static void AddTriangle(BuildingMeshData data, List<int> submesh, int i0, int i1, int i2, Vector3 normal)
        {
            var geometric = Vector3.Cross(data.Vertices[i1] - data.Vertices[i0], data.Vertices[i2] - data.Vertices[i0]);
            if (Vector3.Dot(geometric, normal) >= 0f)
            {
                submesh.Add(i0);
                submesh.Add(i1);
                submesh.Add(i2);
            }
            else
            {
                submesh.Add(i0);
                submesh.Add(i2);
                submesh.Add(i1);
            }
        }

        private static void AddGable(BuildingMeshData data, List<int> submesh, Vector3 center, Vector3 tangent, Vector3 normal, BuildingModule module)
        {
            var half = tangent * (module.Width * 0.5f);
            var depth = normal * (module.Depth * 0.5f);
            var baseOffset = Vector3.up * (-module.Height / 3f);
            var apex = center + (Vector3.up * (module.Height * 2f / 3f));
            var left = center + baseOffset - half;
            var right = center + baseOffset + half;

            // Buitenste en binnenste driehoek.
            AddTri(data, submesh, left + depth, right + depth, apex + depth, normal);
            AddTri(data, submesh, left - depth, right - depth, apex - depth, -normal);

            // Onderkant en twee schuine zijden.
            AddQuad(data, submesh, left - depth, right - depth, right + depth, left + depth, Vector3.down);
            AddQuad(data, submesh, left - depth, apex - depth, apex + depth, left + depth, SideNormal(left, apex, depth, Vector3.up - tangent));
            AddQuad(data, submesh, right - depth, apex - depth, apex + depth, right + depth, SideNormal(right, apex, depth, Vector3.up + tangent));
        }

        /// <summary>Normaal van een schuine zijde: loodrecht op de rand en naar buiten en omhoog gericht.</summary>
        private static Vector3 SideNormal(Vector3 bottom, Vector3 apex, Vector3 depth, Vector3 outwardHint)
        {
            var normal = Vector3.Cross((apex - bottom).normalized, depth.normalized);
            return (Vector3.Dot(normal, outwardHint) >= 0f ? normal : -normal).normalized;
        }

        private static void AddTri(BuildingMeshData data, List<int> submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
        {
            var start = data.Vertices.Count;
            data.Vertices.Add(a);
            data.Vertices.Add(b);
            data.Vertices.Add(c);
            for (var i = 0; i < 3; i++)
            {
                data.Normals.Add(normal);
            }

            data.Uvs.Add(new Vector2(0f, 0f));
            data.Uvs.Add(new Vector2(1f, 0f));
            data.Uvs.Add(new Vector2(0.5f, 1f));
            AddTriangle(data, submesh, start, start + 1, start + 2, normal);
        }
    }
}
