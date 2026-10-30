using System;
using System.Collections.Generic;
using Cities.Simulation.Network;
using Unity.Mathematics;

namespace Cities.World.Roads
{
    /// <summary>Het soort oppervlak van een deel van het wegdek; elk soort heeft een eigen materiaal (submesh).</summary>
    public enum RoadSurface
    {
        Asphalt,
        Bike,
        Sidewalk,
        Curb,
        MarkingSolid,
        MarkingDashed,
    }

    /// <summary>De geometrie van één weg: punten, normalen, uv's en per <see cref="RoadSurface"/> de driehoeken.</summary>
    public sealed class RoadMeshData
    {
        public RoadMeshData()
        {
            foreach (RoadSurface surface in Enum.GetValues(typeof(RoadSurface)))
            {
                Triangles[(int)surface] = new List<int>();
            }
        }

        public List<float3> Vertices { get; } = new List<float3>();

        public List<float3> Normals { get; } = new List<float3>();

        public List<float2> Uvs { get; } = new List<float2>();

        public List<int>[] Triangles { get; } = new List<int>[Enum.GetValues(typeof(RoadSurface)).Length];
    }

    /// <summary>
    /// Bouwt het wegdek van een segment: per lane een strook op de hoogte van de hartlijn (stoepen een stoeprand hoger,
    /// met opstaande randen en een rok tegen kieren met het terrein) en de markeringen. De uv's lopen over de breedte
    /// van 0 tot 1 en in de lengte per <see cref="TextureMeters"/> meter; de textures herhalen in de lengte.
    /// </summary>
    public static class RoadMeshBuilder
    {
        public const float CurbHeight = 0.15f;
        public const float SkirtDepth = 0.6f;
        public const float TextureMeters = 8f;
        public const float MarkingLift = 0.03f;
        public const float MarkingWidth = 0.14f;
        public const float EdgeLineInset = 0.25f;
        public const float MaxStep = 3f;
        public const float MaxTurnPerStep = 0.1f;
        public const int MaxSamples = 700;

        public static RoadMeshData Build(Bezier3 curve, IReadOnlyList<LaneSpec> lanes)
        {
            if (lanes == null)
            {
                throw new ArgumentNullException(nameof(lanes));
            }

            var data = new RoadMeshData();
            var line = Sample(curve);
            var vehicles = new List<LaneSpec>();
            foreach (var lane in lanes)
            {
                if ((lane.Types & LaneTypes.Pedestrian) != 0)
                {
                    AddSidewalk(data, line, lane);
                    continue;
                }

                var surface = (lane.Types & LaneTypes.Vehicles) != 0 ? RoadSurface.Asphalt : RoadSurface.Bike;
                AddStrip(data, surface, line, lane.Offset - (lane.Width * 0.5f), lane.Offset + (lane.Width * 0.5f), 0f);
                if (surface == RoadSurface.Asphalt)
                {
                    vehicles.Add(lane);
                }
            }

            AddMarkings(data, line, vehicles);
            AddSkirts(data, line, lanes);
            return data;
        }

        /// <summary>Aantal stappen langs de curve: genoeg voor de lengte en voor de bochtigheid.</summary>
        public static int SampleCount(Bezier3 curve)
        {
            var turn = 0f;
            var previous = curve.Direction(0f);
            const int probes = 16;
            for (var i = 1; i <= probes; i++)
            {
                var next = curve.Direction(i / (float)probes);
                turn += math.acos(math.clamp(math.dot(previous, next), -1f, 1f));
                previous = next;
            }

            var steps = math.max(curve.Length() / MaxStep, turn / MaxTurnPerStep);
            return math.clamp((int)math.ceil(steps), 1, MaxSamples - 1) + 1;
        }

        private static Line Sample(Bezier3 curve)
        {
            var count = SampleCount(curve);
            var line = new Line(count);
            for (var i = 0; i < count; i++)
            {
                var t = i / (float)(count - 1);
                var direction = curve.Direction(t);
                line.Center[i] = curve.Position(t);
                line.Right[i] = Bezier3.Right(direction);
                line.Up[i] = math.normalizesafe(math.cross(direction, line.Right[i]), new float3(0f, 1f, 0f));
                line.Distance[i] = i == 0 ? 0f : line.Distance[i - 1] + math.distance(line.Center[i], line.Center[i - 1]);
            }

            return line;
        }

        private static void AddSidewalk(RoadMeshData data, Line line, LaneSpec lane)
        {
            var inner = lane.Offset > 0f ? lane.Offset - (lane.Width * 0.5f) : lane.Offset + (lane.Width * 0.5f);
            var outer = lane.Offset > 0f ? lane.Offset + (lane.Width * 0.5f) : lane.Offset - (lane.Width * 0.5f);
            AddStrip(data, RoadSurface.Sidewalk, line, math.min(inner, outer), math.max(inner, outer), CurbHeight);

            // Stoeprand aan de wegkant, rok aan de buitenkant.
            var towardsRoad = lane.Offset > 0f ? -1f : 1f;
            AddWall(data, line, inner, 0f, CurbHeight, towardsRoad);
            AddWall(data, line, outer, -SkirtDepth, CurbHeight, -towardsRoad);
        }

        /// <summary>Rok langs de buitenste rand van wat geen stoep is, zodat het wegdek niet loskomt van het terrein.</summary>
        private static void AddSkirts(RoadMeshData data, Line line, IReadOnlyList<LaneSpec> lanes)
        {
            var maxRight = float.MinValue;
            var minLeft = float.MaxValue;
            var rightIsSidewalk = false;
            var leftIsSidewalk = false;
            foreach (var lane in lanes)
            {
                var right = lane.Offset + (lane.Width * 0.5f);
                var left = lane.Offset - (lane.Width * 0.5f);
                if (right > maxRight)
                {
                    maxRight = right;
                    rightIsSidewalk = (lane.Types & LaneTypes.Pedestrian) != 0;
                }

                if (left < minLeft)
                {
                    minLeft = left;
                    leftIsSidewalk = (lane.Types & LaneTypes.Pedestrian) != 0;
                }
            }

            if (!rightIsSidewalk)
            {
                AddWall(data, line, maxRight, -SkirtDepth, 0f, 1f);
            }

            if (!leftIsSidewalk)
            {
                AddWall(data, line, minLeft, -SkirtDepth, 0f, -1f);
            }
        }

        private static void AddMarkings(RoadMeshData data, Line line, List<LaneSpec> vehicles)
        {
            if (vehicles.Count == 0)
            {
                return;
            }

            vehicles.Sort((a, b) => a.Offset.CompareTo(b.Offset));
            var left = vehicles[0].Offset - (vehicles[0].Width * 0.5f);
            var right = vehicles[vehicles.Count - 1].Offset + (vehicles[vehicles.Count - 1].Width * 0.5f);
            AddMarking(data, RoadSurface.MarkingSolid, line, left + EdgeLineInset);
            AddMarking(data, RoadSurface.MarkingSolid, line, right - EdgeLineInset);

            for (var i = 1; i < vehicles.Count; i++)
            {
                var boundary = vehicles[i].Offset - (vehicles[i].Width * 0.5f);
                var opposing = vehicles[i - 1].Direction != vehicles[i].Direction;
                AddMarking(data, opposing ? RoadSurface.MarkingSolid : RoadSurface.MarkingDashed, line, boundary);
            }
        }

        private static void AddMarking(RoadMeshData data, RoadSurface surface, Line line, float offset) =>
            AddStrip(data, surface, line, offset - (MarkingWidth * 0.5f), offset + (MarkingWidth * 0.5f), MarkingLift);

        /// <summary>Strook tussen twee zijdelingse standen op hoogte <paramref name="lift"/> boven de hartlijn, met de bovenkant naar boven.</summary>
        private static void AddStrip(RoadMeshData data, RoadSurface surface, Line line, float from, float to, float lift)
        {
            var first = data.Vertices.Count;
            for (var i = 0; i < line.Count; i++)
            {
                var v = line.Distance[i] / TextureMeters;
                data.Vertices.Add(line.Center[i] + (line.Right[i] * from) + (line.Up[i] * lift));
                data.Vertices.Add(line.Center[i] + (line.Right[i] * to) + (line.Up[i] * lift));
                data.Normals.Add(line.Up[i]);
                data.Normals.Add(line.Up[i]);
                data.Uvs.Add(new float2(0f, v));
                data.Uvs.Add(new float2(1f, v));
            }

            Stitch(data, surface, first, line.Count, line.Up[0]);
        }

        /// <summary>Verticale wand langs de rand op zijdelingse stand <paramref name="offset"/>, tussen twee hoogtes, naar <paramref name="facing"/> (-1 links, 1 rechts).</summary>
        private static void AddWall(RoadMeshData data, Line line, float offset, float bottom, float top, float facing)
        {
            var first = data.Vertices.Count;
            for (var i = 0; i < line.Count; i++)
            {
                var v = line.Distance[i] / TextureMeters;
                var edge = line.Center[i] + (line.Right[i] * offset);
                data.Vertices.Add(edge + (line.Up[i] * top));
                data.Vertices.Add(edge + (line.Up[i] * bottom));
                data.Normals.Add(line.Right[i] * facing);
                data.Normals.Add(line.Right[i] * facing);
                data.Uvs.Add(new float2(0f, v));
                data.Uvs.Add(new float2(1f, v));
            }

            Stitch(data, RoadSurface.Curb, first, line.Count, line.Right[0] * facing);
        }

        /// <summary>Verbindt de paren punten (twee per stap) tot vlakken die naar <paramref name="facing"/> kijken.</summary>
        internal static void Stitch(RoadMeshData data, RoadSurface surface, int first, int count, float3 facing)
        {
            var triangles = data.Triangles[(int)surface];
            var a = data.Vertices[first];
            var b = data.Vertices[first + 1];
            var c = data.Vertices[first + 2];
            var flip = math.dot(math.cross(c - a, b - a), facing) < 0f;
            for (var i = 0; i < count - 1; i++)
            {
                var p = first + (i * 2);
                if (flip)
                {
                    triangles.AddRange(new[] { p, p + 1, p + 2, p + 1, p + 3, p + 2 });
                }
                else
                {
                    triangles.AddRange(new[] { p, p + 2, p + 1, p + 1, p + 2, p + 3 });
                }
            }
        }

        private sealed class Line
        {
            public Line(int count)
            {
                Count = count;
                Center = new float3[count];
                Right = new float3[count];
                Up = new float3[count];
                Distance = new float[count];
            }

            public int Count { get; }

            public float3[] Center { get; }

            public float3[] Right { get; }

            public float3[] Up { get; }

            public float[] Distance { get; }
        }
    }
}
