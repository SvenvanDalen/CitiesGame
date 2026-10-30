using System;
using System.Linq;
using Cities.World.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Cities.Tests.EditMode.Terrain
{
    public class TerrainChunkTopologyTests
    {
        /// <summary>Posities zoals de mesh-job ze zet: een vlak raster op hoogte 0 met de rok 1 m lager.</summary>
        private static Vector3[] FlatPositions(int resolution)
        {
            var n = resolution + 1;
            var positions = new Vector3[TerrainChunkTopology.VertexCount(resolution)];
            for (var z = 0; z < n; z++)
            {
                for (var x = 0; x < n; x++)
                {
                    positions[(z * n) + x] = new Vector3(x, 0f, z);
                }
            }

            for (var s = 0; s < 4 * n; s++)
            {
                positions[TerrainChunkTopology.GridVertexCount(resolution) + s] = positions[TerrainChunkTopology.SkirtSource(resolution, s)] + Vector3.down;
            }

            return positions;
        }

        [TestCase(32)]
        [TestCase(4)]
        public void CountsMatchTheIndexBuffer(int resolution)
        {
            var indices = TerrainChunkTopology.BuildIndices(resolution);

            Assert.That(indices.Length, Is.EqualTo(TerrainChunkTopology.IndexCount(resolution)));
            Assert.That(indices.Max(), Is.EqualTo(TerrainChunkTopology.VertexCount(resolution) - 1));
            Assert.That(TerrainChunkTopology.VertexCount(32), Is.EqualTo((33 * 33) + 132));
        }

        [TestCase(32)]
        [TestCase(8)]
        public void GridFacesUpAndSkirtsFaceOutward(int resolution)
        {
            var positions = FlatPositions(resolution);
            var indices = TerrainChunkTopology.BuildIndices(resolution);
            var gridTriangles = resolution * resolution * 2;
            var center = new Vector3(resolution * 0.5f, 0f, resolution * 0.5f);
            for (var t = 0; t < indices.Length / 3; t++)
            {
                var a = positions[indices[t * 3]];
                var b = positions[indices[(t * 3) + 1]];
                var c = positions[indices[(t * 3) + 2]];

                // Unity's voorkant is met de klok mee; dan wijst Cross(b - a, c - a) naar de kijker.
                var normal = Vector3.Cross(b - a, c - a);
                Assert.That(normal.magnitude, Is.GreaterThan(0f), $"driehoek {t} is ontaard");
                if (t < gridTriangles)
                {
                    Assert.That(normal.y, Is.GreaterThan(0f), $"rasterdriehoek {t}");
                }
                else
                {
                    var outward = ((a + b + c) / 3f) - center;
                    outward.y = 0f;
                    Assert.That(Vector3.Dot(normal, outward), Is.GreaterThan(0f), $"rokdriehoek {t}");
                }
            }
        }

        [Test]
        public void SkirtsHangUnderEveryEdgeVertex()
        {
            const int resolution = 4;
            var sources = Enumerable.Range(0, 4 * (resolution + 1)).Select(s => TerrainChunkTopology.SkirtSource(resolution, s)).Distinct().ToList();

            // 16 randpunten, de vier hoeken komen elk op twee randen voor.
            Assert.That(sources, Has.Count.EqualTo(16));
            Assert.That(sources, Has.All.Matches<int>(i => i % 5 == 0 || i % 5 == 4 || i < 5 || i >= 20));
        }

        [Test]
        public void InvalidArgumentsAreRejected()
        {
            Assert.That(() => TerrainChunkTopology.SkirtSource(4, 20), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => TerrainChunkTopology.SkirtSource(4, -1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => TerrainChunkTopology.BuildIndices(0), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => TerrainChunkTopology.BuildIndices(400), Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
