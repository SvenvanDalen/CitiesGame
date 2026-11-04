using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Cities.World.Buildings
{
    /// <summary>Een ingelezen model (E21-08): hoekpunten, normalen, uv's en per materiaal een lijst driehoeken.</summary>
    public sealed class ObjModel
    {
        public List<Vector3> Vertices { get; } = new List<Vector3>();

        public List<Vector3> Normals { get; } = new List<Vector3>();

        public List<Vector2> Uvs { get; } = new List<Vector2>();

        /// <summary>Materiaalnamen in de volgorde waarin ze voorkomen; <see cref="Submeshes"/> heeft dezelfde volgorde.</summary>
        public List<string> Materials { get; } = new List<string>();

        public List<List<int>> Submeshes { get; } = new List<List<int>>();

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

        /// <summary>Omhullende van de hoekpunten; leeg model geeft nullen.</summary>
        public (Vector3 Min, Vector3 Max) Bounds()
        {
            if (Vertices.Count == 0)
            {
                return (Vector3.zero, Vector3.zero);
            }

            var min = Vertices[0];
            var max = Vertices[0];
            foreach (var v in Vertices)
            {
                min = Vector3.Min(min, v);
                max = Vector3.Max(max, v);
            }

            return (min, max);
        }
    }

    /// <summary>
    /// Leest Wavefront OBJ-tekst (<c>v</c>, <c>vt</c>, <c>vn</c>, <c>f</c>, <c>usemtl</c>). Vlakken met meer dan drie hoeken worden
    /// driehoeken; ontbrekende normalen worden per vlak berekend. OBJ is rechtshandig en Unity linkshandig: de X-as wordt
    /// gespiegeld en de volgorde van de hoeken omgedraaid, zodat het model er in het spel hetzelfde uitziet.
    /// </summary>
    public static class ObjParser
    {
        public const int MaxVertices = 500_000;
        public const string DefaultMaterial = "default";

        public static ObjModel Parse(string text)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var model = new ObjModel();
            var lookup = new Dictionary<(int, int, int), int>();
            var current = -1;
            var lineNumber = 0;
            foreach (var raw in text.Split('\n'))
            {
                lineNumber++;
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                var parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                switch (parts[0])
                {
                    case "v":
                        positions.Add(new Vector3(-Float(parts, 1, lineNumber), Float(parts, 2, lineNumber), Float(parts, 3, lineNumber)));
                        break;
                    case "vn":
                        normals.Add(new Vector3(-Float(parts, 1, lineNumber), Float(parts, 2, lineNumber), Float(parts, 3, lineNumber)));
                        break;
                    case "vt":
                        uvs.Add(new Vector2(Float(parts, 1, lineNumber), Float(parts, 2, lineNumber)));
                        break;
                    case "usemtl":
                        current = Submesh(model, parts.Length > 1 ? parts[1] : DefaultMaterial);
                        break;
                    case "f":
                        if (current < 0)
                        {
                            current = Submesh(model, DefaultMaterial);
                        }

                        AddFace(model, model.Submeshes[current], parts, positions, normals, uvs, lookup, lineNumber);
                        break;
                }
            }

            if (model.Vertices.Count == 0)
            {
                throw new FormatException("Het model bevat geen vlakken.");
            }

            return model;
        }

        private static int Submesh(ObjModel model, string material)
        {
            var index = model.Materials.IndexOf(material);
            if (index >= 0)
            {
                return index;
            }

            model.Materials.Add(material);
            model.Submeshes.Add(new List<int>());
            return model.Materials.Count - 1;
        }

        private static float Float(string[] parts, int index, int lineNumber)
        {
            if (index >= parts.Length || !float.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                || float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new FormatException($"Regel {lineNumber}: ongeldig getal.");
            }

            return value;
        }

        private static void AddFace(ObjModel model, List<int> triangles, string[] parts, List<Vector3> positions, List<Vector3> normals, List<Vector2> uvs,
            Dictionary<(int, int, int), int> lookup, int lineNumber)
        {
            if (parts.Length < 4)
            {
                throw new FormatException($"Regel {lineNumber}: een vlak heeft minstens drie hoeken.");
            }

            var corners = new (int P, int T, int N)[parts.Length - 1];
            for (var i = 1; i < parts.Length; i++)
            {
                var pieces = parts[i].Split('/');
                corners[i - 1] = (
                    Resolve(pieces[0], positions.Count, lineNumber),
                    pieces.Length > 1 && pieces[1].Length > 0 ? Resolve(pieces[1], uvs.Count, lineNumber) : -1,
                    pieces.Length > 2 && pieces[2].Length > 0 ? Resolve(pieces[2], normals.Count, lineNumber) : -1);
            }

            var faceNormal = Vector3.Cross(positions[corners[1].P] - positions[corners[0].P], positions[corners[2].P] - positions[corners[0].P]).normalized;
            var faceId = triangles.Count;
            var indices = new int[corners.Length];
            for (var i = 0; i < corners.Length; i++)
            {
                var corner = corners[i];
                // Zonder normaal deelt een hoekpunt niet met andere vlakken (vlakke schaduw).
                var key = (corner.P, corner.T, corner.N >= 0 ? corner.N : -(faceId + 1));
                if (!lookup.TryGetValue(key, out var index))
                {
                    if (model.Vertices.Count >= MaxVertices)
                    {
                        throw new FormatException($"Het model heeft meer dan {MaxVertices} hoekpunten.");
                    }

                    index = model.Vertices.Count;
                    model.Vertices.Add(positions[corner.P]);
                    model.Uvs.Add(corner.T >= 0 ? uvs[corner.T] : Vector2.zero);
                    model.Normals.Add(corner.N >= 0 ? normals[corner.N] : -faceNormal);
                    lookup.Add(key, index);
                }

                indices[i] = index;
            }

            for (var i = 1; i < indices.Length - 1; i++)
            {
                // De X-as is gespiegeld, dus de driehoek gaat in omgekeerde volgorde.
                triangles.Add(indices[0]);
                triangles.Add(indices[i + 1]);
                triangles.Add(indices[i]);
            }
        }

        private static int Resolve(string text, int count, int lineNumber)
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value == 0)
            {
                throw new FormatException($"Regel {lineNumber}: ongeldige index \"{text}\".");
            }

            var index = value > 0 ? value - 1 : count + value;
            if (index < 0 || index >= count)
            {
                throw new FormatException($"Regel {lineNumber}: index {value} bestaat niet.");
            }

            return index;
        }
    }
}
