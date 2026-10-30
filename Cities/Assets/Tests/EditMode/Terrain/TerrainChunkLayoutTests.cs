using System;
using System.Collections.Generic;
using Cities.World.Grid;
using Cities.World.Terrain;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Tests.EditMode.Terrain
{
    public class TerrainChunkLayoutTests
    {
        private static readonly TerrainChunkLayout Layout = new TerrainChunkLayout(new WorldDimensions(2, 2, 64, 8f));

        [Test]
        public void ChunksTileTheWorld()
        {
            Assert.That(Layout.ChunksX, Is.EqualTo(4));
            Assert.That(Layout.ChunkCount, Is.EqualTo(16));
            Assert.That(Layout.ChunksPerTile, Is.EqualTo(2));
            Assert.That(Layout.ChunkSize, Is.EqualTo(256f));
            Assert.That(Layout.OriginCorner(Layout.Index(3, 1)), Is.EqualTo(new Vector2Int(96, 32)));
            Assert.That(Layout.Origin(Layout.Index(1, 2)), Is.EqualTo(new Vector2(256f, 512f)));
            Assert.That(Layout.Coord(7), Is.EqualTo(new Vector2Int(3, 1)));
        }

        [Test]
        public void DefaultMapHasEightByEightChunksPerTile()
        {
            var layout = new TerrainChunkLayout(WorldDimensions.Default);

            Assert.That(layout.ChunksPerTile, Is.EqualTo(8));
            Assert.That(layout.ChunkCount, Is.EqualTo(72 * 72));
        }

        [Test]
        public void TilesMustSplitIntoWholeChunks()
        {
            Assert.That(() => new TerrainChunkLayout(new WorldDimensions(1, 1, 48, 8f)), Throws.ArgumentException);
        }

        [TestCase(0, 32)]
        [TestCase(1, 16)]
        [TestCase(3, 4)]
        public void ResolutionHalvesPerLod(int lod, int expected)
        {
            Assert.That(TerrainChunkLayout.Resolution(lod), Is.EqualTo(expected));
        }

        [Test]
        public void ResolutionRejectsUnknownLod()
        {
            Assert.That(() => TerrainChunkLayout.Resolution(4), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => TerrainChunkLayout.Resolution(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(0f, 0)]
        [TestCase(599f, 0)]
        [TestCase(600f, 1)]
        [TestCase(2000f, 2)]
        [TestCase(99999f, 3)]
        public void LodGrowsWithDistance(float distance, int expected)
        {
            Assert.That(TerrainChunkLayout.ChooseLod(distance, new[] { 600f, 1400f, 3200f }), Is.EqualTo(expected));
        }

        [Test]
        public void LodNeverExceedsTheCoarsestLevel()
        {
            Assert.That(TerrainChunkLayout.ChooseLod(1e6f, new[] { 1f, 2f, 3f, 4f, 5f }), Is.EqualTo(TerrainChunkLayout.LodCount - 1));
            Assert.That(TerrainChunkLayout.ChooseLod(1e6f, Array.Empty<float>()), Is.EqualTo(0));
        }

        [Test]
        public void DistanceIsZeroInsideAndGrowsOutside()
        {
            var chunk = Layout.Index(1, 1);

            Assert.That(Layout.Distance(chunk, new Vector3(300f, 10f, 300f), 0f, 50f), Is.EqualTo(0f));
            Assert.That(Layout.Distance(chunk, new Vector3(300f, 150f, 300f), 0f, 50f), Is.EqualTo(100f));
            Assert.That(Layout.Distance(chunk, new Vector3(-44f, 10f, 300f), 0f, 50f), Is.EqualTo(300f));
            Assert.That(Layout.Distance(chunk, new Vector3(812f, 10f, 812f), 0f, 50f), Is.EqualTo(300f * Mathf.Sqrt(2f)).Within(1e-3f));
        }

        [Test]
        public void NeighbourStepsFollowTheirLodAndAreZeroAtTheMapEdge()
        {
            var lods = new int[Layout.ChunkCount];
            lods[Layout.Index(1, 0)] = 1;
            lods[Layout.Index(2, 1)] = 3;
            lods[Layout.Index(1, 2)] = 2;

            Assert.That(Layout.NeighbourSteps(Layout.Index(1, 1), lods), Is.EqualTo(new int4(2, 8, 4, 1)));
            Assert.That(Layout.NeighbourSteps(Layout.Index(0, 0), lods), Is.EqualTo(new int4(0, 2, 1, 0)));
            Assert.That(Layout.NeighbourSteps(Layout.Index(3, 3), lods), Is.EqualTo(new int4(1, 0, 0, 1)));
        }

        [Test]
        public void ChangedCornerInsideAChunkTouchesOnlyThatChunk()
        {
            var touched = new List<int>();
            Layout.ChunksTouching(new RectInt(40, 40, 1, 1), touched);

            Assert.That(touched, Is.EqualTo(new[] { Layout.Index(1, 1) }));
        }

        [Test]
        public void SharedEdgeCornerTouchesBothNeighbours()
        {
            var touched = new List<int>();
            Layout.ChunksTouching(new RectInt(64, 40, 1, 1), touched);

            Assert.That(touched, Is.EquivalentTo(new[] { Layout.Index(1, 1), Layout.Index(2, 1) }));
        }

        [Test]
        public void CornerNextToAnEdgeAlsoTouchesTheNeighbourForItsNormals()
        {
            var touched = new List<int>();
            Layout.ChunksTouching(new RectInt(65, 40, 1, 1), touched);

            Assert.That(touched, Is.EquivalentTo(new[] { Layout.Index(1, 1), Layout.Index(2, 1) }));
        }

        [Test]
        public void WholeMapTouchesEveryChunkOnceAndEmptyTouchesNone()
        {
            var touched = new List<int>();
            Layout.ChunksTouching(new RectInt(0, 0, 129, 129), touched);
            Assert.That(touched, Is.Unique);
            Assert.That(touched, Has.Count.EqualTo(Layout.ChunkCount));

            Layout.ChunksTouching(new RectInt(5, 5, 0, 3), touched);
            Assert.That(touched, Is.Empty);
        }
    }
}
