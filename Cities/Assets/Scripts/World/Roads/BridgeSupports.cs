using System;
using System.Collections.Generic;
using Cities.Simulation.Network;
using Cities.World.Terrain;
using Unity.Mathematics;

namespace Cities.World.Roads
{
    /// <summary>Een pijler: staat op het terrein (<see cref="Base"/>) en draagt het brugdek (<see cref="Top"/>, onderkant dek).</summary>
    public readonly struct BridgePillar
    {
        public BridgePillar(float3 baseAt, float3 top, float radius)
        {
            Base = baseAt;
            Top = top;
            Radius = radius;
        }

        public float3 Base { get; }

        public float3 Top { get; }

        public float Radius { get; }

        public float Height => Top.y - Base.y;
    }

    /// <summary>Een tunnelmond op de plek waar het wegdek het terrein snijdt; <see cref="Into"/> wijst de tunnel in.</summary>
    public readonly struct TunnelPortal
    {
        public TunnelPortal(float3 position, float3 into)
        {
            Position = position;
            Into = into;
        }

        public float3 Position { get; }

        public float3 Into { get; }
    }

    /// <summary>
    /// Pijlers en tunnelmonden (E04-06b), het vervolg op <see cref="BridgeDeck"/>. <see cref="Plan"/> zet pijlers onder het brugdek waar het hoog
    /// genoeg boven het terrein ligt, <see cref="FindPortals"/> vindt waar een tunnel het terrein in of uit gaat, en de <c>Build</c>-methodes
    /// maken er meshes van in dezelfde <see cref="RoadMeshData"/> als de weg (oppervlak <see cref="RoadSurface.Curb"/> is het beton).
    /// </summary>
    public static class BridgeSupports
    {
        /// <summary>Kleinste hoogte (m) waarvoor een pijler nodig is; lager dan dit ligt het dek op de landhoofden.</summary>
        public const float MinPillarHeight = 2.5f;

        /// <summary>Gewenste afstand (m) tussen pijlers langs het wegdek.</summary>
        public const float Spacing = 30f;

        public const float PillarRadius = 0.9f;

        /// <summary>Vrije hoogte (m) in een tunnelmond en dikte van de kopmuur.</summary>
        public const float PortalClearance = 5f;

        public const float PortalHeadHeight = 1.5f;

        public const float PortalDepth = 1.2f;

        private const int PillarSides = 8;
        private const int Steps = 64;
        private const int Bisections = 10;

        public static IReadOnlyList<BridgePillar> Plan(Bezier3 deck, ITerrainHeight ground, float spacing = Spacing)
        {
            if (ground is null)
            {
                throw new ArgumentNullException(nameof(ground));
            }

            if (!(spacing > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(spacing), spacing, "De afstand tussen pijlers moet positief zijn.");
            }

            var length = deck.Length();
            var count = math.max(1, (int)math.round(length / spacing));
            var pillars = new List<BridgePillar>();
            for (var i = 1; i < count; i++)
            {
                var at = deck.Position(i / (float)count);
                var top = new float3(at.x, at.y - RoadMeshBuilder.DeckThickness, at.z);
                var ground3 = new float3(at.x, ground.SurfaceHeight(at.xz), at.z);
                if (top.y - ground3.y >= MinPillarHeight)
                {
                    pillars.Add(new BridgePillar(ground3, top, PillarRadius));
                }
            }

            return pillars;
        }

        /// <summary>Plekken waar het wegdek van boven naar onder het terrein gaat of omgekeerd, in volgorde langs de curve.</summary>
        public static IReadOnlyList<TunnelPortal> FindPortals(Bezier3 deck, ITerrainHeight ground)
        {
            if (ground is null)
            {
                throw new ArgumentNullException(nameof(ground));
            }

            var portals = new List<TunnelPortal>();
            var previousBelow = Clearance(deck, ground, 0f) < -BridgeDeck.Tolerance;
            var previousT = 0f;
            for (var i = 1; i <= Steps; i++)
            {
                var t = i / (float)Steps;
                var below = Clearance(deck, ground, t) < -BridgeDeck.Tolerance;
                if (below != previousBelow)
                {
                    var crossing = Bisect(deck, ground, previousT, t);
                    var forward = deck.Direction(crossing);
                    portals.Add(new TunnelPortal(deck.Position(crossing), below ? forward : -forward));
                }

                previousBelow = below;
                previousT = t;
            }

            return portals;
        }

        /// <summary>Ronde pijlers als achtkant zonder deksels (boven het dek, onder het terrein).</summary>
        public static RoadMeshData BuildPillars(IReadOnlyList<BridgePillar> pillars)
        {
            if (pillars is null)
            {
                throw new ArgumentNullException(nameof(pillars));
            }

            var data = new RoadMeshData();
            foreach (var pillar in pillars)
            {
                AddPillar(data, pillar);
            }

            return data;
        }

        /// <summary>Kopmuur boven de tunnelingang en twee stijlen aan de zijkanten, over de breedte van de lanes.</summary>
        public static RoadMeshData BuildPortal(TunnelPortal portal, IReadOnlyList<LaneSpec> lanes)
        {
            if (lanes is null)
            {
                throw new ArgumentNullException(nameof(lanes));
            }

            var left = float.MaxValue;
            var right = float.MinValue;
            foreach (var lane in lanes)
            {
                left = math.min(left, lane.Offset - (lane.Width * 0.5f));
                right = math.max(right, lane.Offset + (lane.Width * 0.5f));
            }

            if (left > right)
            {
                throw new ArgumentException("Een tunnelmond heeft minstens één lane nodig.", nameof(lanes));
            }

            var forward = math.normalizesafe(new float3(portal.Into.x, 0f, portal.Into.z), new float3(0f, 0f, 1f));
            var side = new float3(forward.z, 0f, -forward.x);
            var data = new RoadMeshData();
            var postWidth = 0.8f;
            var height = PortalClearance + PortalHeadHeight;
            AddBox(data, portal.Position + (side * (left - (postWidth * 0.5f))), side, forward, postWidth, PortalDepth, 0f, height);
            AddBox(data, portal.Position + (side * (right + (postWidth * 0.5f))), side, forward, postWidth, PortalDepth, 0f, height);
            var middle = (left + right) * 0.5f;
            AddBox(data, portal.Position + (side * middle), side, forward, (right - left) + (postWidth * 2f), PortalDepth, PortalClearance, height);
            return data;
        }

        private static float Clearance(Bezier3 deck, ITerrainHeight ground, float t)
        {
            var at = deck.Position(t);
            return at.y - ground.SurfaceHeight(at.xz);
        }

        private static float Bisect(Bezier3 deck, ITerrainHeight ground, float low, float high)
        {
            var lowBelow = Clearance(deck, ground, low) < -BridgeDeck.Tolerance;
            for (var i = 0; i < Bisections; i++)
            {
                var middle = (low + high) * 0.5f;
                if ((Clearance(deck, ground, middle) < -BridgeDeck.Tolerance) == lowBelow)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            return (low + high) * 0.5f;
        }

        private static void AddPillar(RoadMeshData data, BridgePillar pillar)
        {
            var first = data.Vertices.Count;
            for (var i = 0; i < PillarSides; i++)
            {
                var angle = i * math.PI * 2f / PillarSides;
                var around = new float3(math.cos(angle), 0f, math.sin(angle));
                var u = i / (float)PillarSides;
                data.Vertices.Add(pillar.Base + (around * pillar.Radius));
                data.Normals.Add(around);
                data.Uvs.Add(new float2(u, 0f));
                data.Vertices.Add(new float3(pillar.Top.x, pillar.Top.y, pillar.Top.z) + (around * pillar.Radius));
                data.Normals.Add(around);
                data.Uvs.Add(new float2(u, 1f));
            }

            var triangles = data.Triangles[(int)RoadSurface.Curb];
            for (var i = 0; i < PillarSides; i++)
            {
                var a = first + (i * 2);
                var b = first + (((i + 1) % PillarSides) * 2);
                triangles.Add(a);
                triangles.Add(a + 1);
                triangles.Add(b);
                triangles.Add(b);
                triangles.Add(a + 1);
                triangles.Add(b + 1);
            }
        }

        // Doos met de voet op `center`, breedte langs `side`, diepte langs `forward`, van hoogte `bottom` tot `top` boven die voet.
        private static void AddBox(RoadMeshData data, float3 center, float3 side, float3 forward, float width, float depth, float bottom, float top)
        {
            var up = new float3(0f, 1f, 0f);
            var halfW = side * (width * 0.5f);
            var halfD = forward * (depth * 0.5f);
            var low = center + (up * bottom);
            var high = center + (up * top);
            AddQuad(data, low - halfW - halfD, low + halfW - halfD, high + halfW - halfD, high - halfW - halfD, -forward);
            AddQuad(data, low + halfW + halfD, low - halfW + halfD, high - halfW + halfD, high + halfW + halfD, forward);
            AddQuad(data, low - halfW + halfD, low - halfW - halfD, high - halfW - halfD, high - halfW + halfD, -side);
            AddQuad(data, low + halfW - halfD, low + halfW + halfD, high + halfW + halfD, high + halfW - halfD, side);
            AddQuad(data, high - halfW - halfD, high + halfW - halfD, high + halfW + halfD, high - halfW + halfD, up);
        }

        private static void AddQuad(RoadMeshData data, float3 a, float3 b, float3 c, float3 d, float3 normal)
        {
            var first = data.Vertices.Count;
            foreach (var corner in new[] { a, b, c, d })
            {
                data.Vertices.Add(corner);
                data.Normals.Add(normal);
            }

            data.Uvs.Add(new float2(0f, 0f));
            data.Uvs.Add(new float2(1f, 0f));
            data.Uvs.Add(new float2(1f, 1f));
            data.Uvs.Add(new float2(0f, 1f));
            var triangles = data.Triangles[(int)RoadSurface.Curb];
            triangles.Add(first);
            triangles.Add(first + 2);
            triangles.Add(first + 1);
            triangles.Add(first);
            triangles.Add(first + 3);
            triangles.Add(first + 2);
        }
    }
}
