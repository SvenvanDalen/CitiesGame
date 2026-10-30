using Cities.Gameplay.Tools;
using Cities.World.Grid;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Tools
{
    public class GridSnapTests
    {
        [Test]
        public void OddFootprintCentersOnTheCellUnderThePoint()
        {
            var footprint = GridSnap.Footprint(new float3(21f, 0f, 3f), 3, 1, 0, 8f);

            Assert.That(footprint.Origin, Is.EqualTo(new CellCoord(1, 0)));
            Assert.That(footprint.Center(WorldDimensions.Default).x, Is.EqualTo(20f));
        }

        [Test]
        public void EvenFootprintCentersOnTheNearestGridLine()
        {
            Assert.That(GridSnap.Footprint(new float3(19f, 0f, 19f), 2, 2, 0, 8f).Origin, Is.EqualTo(new CellCoord(1, 1)));
            Assert.That(GridSnap.Footprint(new float3(21f, 0f, 21f), 2, 2, 0, 8f).Origin, Is.EqualTo(new CellCoord(2, 2)));
        }

        [Test]
        public void QuarterTurnSwapsWidthAndDepth()
        {
            var footprint = GridSnap.Footprint(new float3(40f, 0f, 40f), 4, 2, 1, 8f);

            Assert.That(footprint.SizeX, Is.EqualTo(2));
            Assert.That(footprint.SizeZ, Is.EqualTo(4));
            Assert.That(footprint.Origin, Is.EqualTo(new CellCoord(4, 3)));
            Assert.That(footprint.YawDegrees, Is.EqualTo(90f));
        }

        [Test]
        public void NegativeTurnsWrapAround()
        {
            Assert.That(new Footprint(default, 1, 1, -1).QuarterTurns, Is.EqualTo(3));
            Assert.That(new Footprint(default, 1, 1, 6).QuarterTurns, Is.EqualTo(2));
        }

        [Test]
        public void PointsLeftOfTheMapGiveNegativeCells()
        {
            Assert.That(GridSnap.Footprint(new float3(-3f, 0f, 4f), 1, 1, 0, 8f).Origin, Is.EqualTo(new CellCoord(-1, 0)));
        }

        [Test]
        public void OverlapAndContainment()
        {
            var a = new Footprint(new CellCoord(0, 0), 2, 2, 0);
            var touching = new Footprint(new CellCoord(2, 0), 2, 2, 0);
            var overlapping = new Footprint(new CellCoord(1, 1), 2, 2, 0);

            Assert.That(a.Overlaps(touching), Is.False);
            Assert.That(a.Overlaps(overlapping), Is.True);
            Assert.That(a.Contains(new CellCoord(1, 1)), Is.True);
            Assert.That(a.Contains(new CellCoord(2, 1)), Is.False);
            Assert.That(a.Max, Is.EqualTo(new CellCoord(1, 1)));
        }

        [Test]
        public void EqualityIncludesShapeAndTurns()
        {
            var a = new Footprint(new CellCoord(3, 4), 2, 1, 0);

            Assert.That(a, Is.EqualTo(new Footprint(new CellCoord(3, 4), 2, 1, 4)));
            Assert.That(a == new Footprint(new CellCoord(3, 4), 2, 1, 1), Is.False);
            Assert.That(a != new Footprint(new CellCoord(3, 4), 1, 2, 0), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(new Footprint(new CellCoord(3, 4), 2, 1, 0).GetHashCode()));
            Assert.That(a.Equals((object)"x"), Is.False);
            Assert.That(a.ToString(), Does.Contain("2×1"));
        }

        [Test]
        public void EmptyFootprintIsRejected()
        {
            Assert.That(() => new Footprint(default, 0, 1, 0), Throws.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => new Footprint(default, 1, 0, 0), Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }
    }
}
