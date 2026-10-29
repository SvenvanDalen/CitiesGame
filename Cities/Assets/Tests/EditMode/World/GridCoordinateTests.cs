using Cities.World.Grid;
using NUnit.Framework;

namespace Cities.Tests.EditMode.World
{
    public class GridCoordinateTests
    {
        [Test]
        public void NeighboursFollowCompassDirections()
        {
            var cell = new CellCoord(4, 4);

            Assert.That(cell.North, Is.EqualTo(new CellCoord(4, 5)));
            Assert.That(cell.South, Is.EqualTo(new CellCoord(4, 3)));
            Assert.That(cell.East, Is.EqualTo(new CellCoord(5, 4)));
            Assert.That(cell.West, Is.EqualTo(new CellCoord(3, 4)));
        }

        [Test]
        public void Distances()
        {
            var a = new CellCoord(1, 1);
            var b = new CellCoord(4, -1);

            Assert.That(a.ManhattanDistance(b), Is.EqualTo(5));
            Assert.That(a.ChebyshevDistance(b), Is.EqualTo(3));
        }

        [Test]
        public void CellCoordEquality()
        {
            var a = new CellCoord(2, 3);

            Assert.That(a == new CellCoord(2, 3), Is.True);
            Assert.That(a != new CellCoord(3, 2), Is.True);
            Assert.That(a.Equals((object)new CellCoord(2, 3)), Is.True);
            Assert.That(a.Equals("(2, 3)"), Is.False);
            Assert.That(a.GetHashCode(), Is.Not.EqualTo(new CellCoord(3, 2).GetHashCode()));
            Assert.That(a.ToString(), Is.EqualTo("(2, 3)"));
        }

        [Test]
        public void TileCoordEquality()
        {
            var a = new TileCoord(1, 2);

            Assert.That(a == new TileCoord(1, 2), Is.True);
            Assert.That(a != new TileCoord(2, 1), Is.True);
            Assert.That(a.Equals((object)new TileCoord(1, 2)), Is.True);
            Assert.That(a.Equals(null), Is.False);
            Assert.That(a.GetHashCode(), Is.EqualTo(new TileCoord(1, 2).GetHashCode()));
            Assert.That(a.ToString(), Is.EqualTo("Tile(1, 2)"));
        }

        [Test]
        public void EmptyZoneHasNoDensity()
        {
            var cell = new ZoneCell(ZoneType.None, ZoneDensity.High);

            Assert.That(cell, Is.EqualTo(ZoneCell.Empty));
            Assert.That(cell.IsZoned, Is.False);
            Assert.That(cell.ToString(), Is.EqualTo("None"));
        }

        [Test]
        public void ZoneCellEquality()
        {
            var a = new ZoneCell(ZoneType.Residential, ZoneDensity.High);

            Assert.That(a.IsZoned, Is.True);
            Assert.That(a == new ZoneCell(ZoneType.Residential, ZoneDensity.High), Is.True);
            Assert.That(a != new ZoneCell(ZoneType.Residential, ZoneDensity.Low), Is.True);
            Assert.That(a.Equals((object)new ZoneCell(ZoneType.Residential, ZoneDensity.High)), Is.True);
            Assert.That(a.Equals(1), Is.False);
            Assert.That(a.GetHashCode(), Is.Not.EqualTo(new ZoneCell(ZoneType.Office, ZoneDensity.High).GetHashCode()));
            Assert.That(a.ToString(), Is.EqualTo("Residential (High)"));
        }
    }
}
