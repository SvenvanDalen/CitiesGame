using System.Linq;
using Cities.Gameplay.Zoning;
using Cities.World.Grid;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Zoning
{
    public class ZoneShapesTests
    {
        [Test]
        public void ADiscHoldsTheCellsWhoseCentreIsInside()
        {
            var cells = ZoneShapes.Disc(new float2(16f, 16f), 8f, 8f);

            Assert.That(cells, Has.Member(new CellCoord(1, 1)), "cel rond het midden (12..20)");
            Assert.That(cells, Has.No.Member(new CellCoord(3, 3)), "ver buiten de kwast");
            Assert.That(cells.Select(c => c.X).Max(), Is.LessThanOrEqualTo(2));
        }

        [Test]
        public void ARectangleIncludesBothCornersInAnyOrder()
        {
            var cells = ZoneShapes.Rectangle(new CellCoord(4, 2), new CellCoord(2, 3));

            Assert.That(cells, Has.Count.EqualTo(6));
            Assert.That(cells, Has.Member(new CellCoord(2, 2)));
            Assert.That(cells, Has.Member(new CellCoord(4, 3)));
        }

        [Test]
        public void FillFollowsConnectedCellsAndStopsAtTheLimit()
        {
            bool Strip(CellCoord c) => c.Z == 0 && c.X >= 0 && c.X < 10;

            Assert.That(ZoneShapes.Fill(new CellCoord(3, 0), Strip, 100), Has.Count.EqualTo(10));
            Assert.That(ZoneShapes.Fill(new CellCoord(3, 0), Strip, 4), Has.Count.EqualTo(4));
            Assert.That(ZoneShapes.Fill(new CellCoord(3, 5), Strip, 100), Is.Empty, "startcel voldoet niet");
        }
    }
}
