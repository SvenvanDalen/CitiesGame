using System.Collections.Generic;
using Cities.World.Grid;
using Cities.World.Terrain;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Tests.EditMode.Terrain
{
    [Category("UnityRuntime")]
    public class TerrainChunkMeshJobTests
    {
        private const float SkirtDepth = 12f;

        /// <summary>Twee tegels van 64 cellen naast elkaar: chunk 1 ligt in tegel 0, chunk 2 in tegel 1.</summary>
        private static readonly WorldDimensions Dimensions = new WorldDimensions(2, 1, 64, 8f);

        [Test]
        public void NeighboursOnDifferentLodsMeetExactlyAcrossTheTileEdge()
        {
            using (var map = CreateHillyMap())
            {
                // De fijne chunk weet dat zijn oosterbuur een stap van 4 hoekpunten heeft.
                var fine = Build(map, new TerrainChunkRequest(new int2(32, 0), 0, new int4(0, 4, 1, 1)));
                var coarse = Build(map, new TerrainChunkRequest(new int2(64, 0), 2, new int4(0, 0, 4, 1)));

                // De rand x = 64: in de fijne chunk lokaal x = 256 m, in de grove x = 0. Elk grof punt valt op een fijn punt.
                var fineEdge = EdgeAt(fine, 256f);
                var coarseEdge = EdgeAt(coarse, 0f);
                Assert.That(coarseEdge, Has.Count.EqualTo(9));
                foreach (var pair in coarseEdge)
                {
                    Assert.That(fineEdge.ContainsKey(pair.Key), Is.True, $"z = {pair.Key}");
                    Assert.That(fineEdge[pair.Key].Position.y, Is.EqualTo(pair.Value.Position.y), "zelfde hoogte");
                    Assert.That(math.distance(fineEdge[pair.Key].Normal, pair.Value.Normal), Is.LessThan(1e-6f), "zelfde normaal");
                }

                // Tussen twee grove punten ligt de fijne rand precies op de rechte lijn van de grove: geen kier.
                foreach (var pair in fineEdge)
                {
                    var below = math.floor(pair.Key / 32f) * 32f;
                    var above = math.min(below + 32f, 256f);
                    var t = (pair.Key - below) / 32f;
                    var line = math.lerp(coarseEdge[below].Position.y, coarseEdge[above].Position.y, t);
                    Assert.That(pair.Value.Position.y, Is.EqualTo(line).Within(1e-4f), $"z = {pair.Key}");
                }

                // Zonder grovere buur volgt de rand gewoon de hoogtekaart.
                var unstitched = EdgeAt(Build(map, new TerrainChunkRequest(new int2(32, 0), 0, new int4(0, 1, 1, 1))), 256f);
                Assert.That(unstitched[8f].Position.y, Is.EqualTo(map.Height[64, 1]));
            }
        }

        [Test]
        public void MeshHasTheTopologyAndBoundsOfItsLod()
        {
            using (var map = CreateHillyMap())
            {
                var mesh = BuildMesh(map, new TerrainChunkRequest(new int2(0, 0), 1, new int4(1, 1, 1, 1)), out var range);
                try
                {
                    Assert.That(mesh.vertexCount, Is.EqualTo(TerrainChunkTopology.VertexCount(16)));
                    Assert.That(mesh.GetIndexCount(0), Is.EqualTo((uint)TerrainChunkTopology.IndexCount(16)));
                    var bounds = mesh.GetSubMesh(0).bounds;
                    Assert.That(bounds.min.y, Is.EqualTo(range.x).Within(1e-3f));
                    Assert.That(bounds.max.y, Is.EqualTo(range.y).Within(1e-3f));
                    Assert.That(bounds.size.x, Is.EqualTo(256f));
                }
                finally
                {
                    Object.DestroyImmediate(mesh);
                }
            }
        }

        private static WorldMap CreateHillyMap()
        {
            var map = new WorldMap(Dimensions);
            var layout = map.Height.Layout;
            var settings = TerrainShapeSettings.Default;
            settings.RiverCenterX = -10000f;
            TerrainGenerator.Generate(map, settings);

            // Een steile heuvel op de tegelgrens, zodat de grove rand echt afwijkt.
            for (var z = 0; z < layout.Depth; z++)
            {
                for (var x = 0; x < layout.Width; x++)
                {
                    map.Height[x, z] += 60f * math.exp(-math.lengthsq(new float2(x - 64, z - 30) / 9f));
                }
            }

            return map;
        }

        private static TerrainVertex[] Build(WorldMap map, TerrainChunkRequest request)
        {
            var mesh = BuildMesh(map, request, out _);
            try
            {
                var positions = mesh.vertices;
                var normals = mesh.normals;
                var result = new TerrainVertex[positions.Length];
                for (var i = 0; i < positions.Length; i++)
                {
                    result[i] = new TerrainVertex { Position = positions[i], Normal = normals[i] };
                }

                return result;
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        private static Mesh BuildMesh(WorldMap map, TerrainChunkRequest request, out float2 range)
        {
            using (var builder = new TerrainMeshBuilder(SkirtDepth))
            using (var requests = new NativeArray<TerrainChunkRequest>(new[] { request }, Allocator.TempJob))
            using (var heightRanges = new NativeArray<float2>(1, Allocator.TempJob))
            {
                var data = Mesh.AllocateWritableMeshData(1);
                builder.Build(map, requests, data, heightRanges);
                var mesh = new Mesh();
                Mesh.ApplyAndDisposeWritableMeshData(data, mesh);
                range = heightRanges[0];
                return mesh;
            }
        }

        /// <summary>Rasterpunten (zonder rok) op lokale x, per z.</summary>
        private static Dictionary<float, TerrainVertex> EdgeAt(TerrainVertex[] vertices, float x)
        {
            var edge = new Dictionary<float, TerrainVertex>();
            foreach (var vertex in vertices)
            {
                if (Mathf.Approximately(vertex.Position.x, x) && !edge.ContainsKey(vertex.Position.z))
                {
                    edge.Add(vertex.Position.z, vertex);
                }
            }

            return edge;
        }
    }
}
