using System;
using Cities.World.Grid;
using NUnit.Framework;
using UnityEngine;

namespace Cities.Tests.EditMode.World
{
    public class WorldDimensionsTests
    {
        private static readonly WorldDimensions Small = new WorldDimensions(3, 2, 16, 8f);

        [Test]
        public void DefaultMatchesBacklogTarget()
        {
            var world = WorldDimensions.Default;

            Assert.That(world.TileCount, Is.EqualTo(81));
            Assert.That(world.CellsX, Is.EqualTo(2304));
            Assert.That(world.TileSize, Is.EqualTo(2048f));
            Assert.That(world.SizeX, Is.EqualTo(18432f));
            Assert.That(world.CellCount, Is.EqualTo(2304 * 2304));
        }

        [Test]
        public void DerivedSizes()
        {
            Assert.That(Small.CellsX, Is.EqualTo(48));
            Assert.That(Small.CellsZ, Is.EqualTo(32));
            Assert.That(Small.SizeZ, Is.EqualTo(256f));
            Assert.That(Small.ToString(), Does.Contain("3×2"));
        }

        [TestCase(0, 1, 1, 1f)]
        [TestCase(1, 0, 1, 1f)]
        [TestCase(1, 1, 0, 1f)]
        [TestCase(1, 1, 1, 0f)]
        [TestCase(1, 1, 1, -2f)]
        [TestCase(1, 1, 1, float.NaN)]
        [TestCase(1, 1, 1, float.PositiveInfinity)]
        [TestCase(1000, 1000, 1024, 1f)]
        public void InvalidDimensionsAreRejected(int tilesX, int tilesZ, int cellsPerTile, float cellSize)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new WorldDimensions(tilesX, tilesZ, cellsPerTile, cellSize));
        }

        [Test]
        public void WorldToCellAndBack()
        {
            var cell = Small.WorldToCell(new Vector2(20f, 9.9f));

            Assert.That(cell, Is.EqualTo(new CellCoord(2, 1)));
            Assert.That(Small.CellCenter(cell), Is.EqualTo(new Vector2(20f, 12f)));
            Assert.That(Small.CellMin(cell), Is.EqualTo(new Vector2(16f, 8f)));
        }

        [Test]
        public void NegativePositionsFloorToNegativeCells()
        {
            Assert.That(Small.WorldToCell(new Vector2(-0.1f, -8.1f)), Is.EqualTo(new CellCoord(-1, -2)));
            Assert.That(Small.Contains(new Vector2(-0.1f, 0f)), Is.False);
        }

        [Test]
        public void ContainsChecksBounds()
        {
            Assert.That(Small.Contains(new CellCoord(47, 31)), Is.True);
            Assert.That(Small.Contains(new CellCoord(48, 0)), Is.False);
            Assert.That(Small.Contains(new CellCoord(0, -1)), Is.False);
            Assert.That(Small.Contains(new TileCoord(2, 1)), Is.True);
            Assert.That(Small.Contains(new TileCoord(3, 0)), Is.False);
            Assert.That(Small.Contains(new Vector2(383.9f, 255.9f)), Is.True);
            Assert.That(Small.Contains(new Vector2(384f, 0f)), Is.False);
        }

        [Test]
        public void CellToTileAndOrigin()
        {
            Assert.That(Small.CellToTile(new CellCoord(31, 16)), Is.EqualTo(new TileCoord(1, 1)));
            Assert.That(Small.CellToTile(new CellCoord(-1, 0)), Is.EqualTo(new TileCoord(-1, 0)));
            Assert.That(Small.TileOrigin(new TileCoord(2, 1)), Is.EqualTo(new CellCoord(32, 16)));
        }

        [Test]
        public void CellIndexRoundTrips()
        {
            var cell = new CellCoord(5, 7);
            var index = Small.CellIndex(cell);

            Assert.That(index, Is.EqualTo((7 * 48) + 5));
            Assert.That(Small.IndexToCell(index), Is.EqualTo(cell));
            Assert.Throws<ArgumentOutOfRangeException>(() => Small.CellIndex(new CellCoord(48, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Small.IndexToCell(Small.CellCount));
        }

        [Test]
        public void EqualityComparesAllFields()
        {
            Assert.That(Small == new WorldDimensions(3, 2, 16, 8f), Is.True);
            Assert.That(Small != new WorldDimensions(3, 2, 16, 4f), Is.True);
            Assert.That(Small.Equals((object)new WorldDimensions(3, 2, 16, 8f)), Is.True);
            Assert.That(Small.Equals("world"), Is.False);
            Assert.That(Small.GetHashCode(), Is.EqualTo(new WorldDimensions(3, 2, 16, 8f).GetHashCode()));
        }
    }
}
