using System.Linq;
using Cities.Gameplay.Tools;
using Cities.Gameplay.Zoning;
using Cities.World.Grid;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Zoning
{
    public class BuildingPlacementTests
    {
        [TestCase(0f, -1f, 0)]
        [TestCase(-1f, 0f, 1)]
        [TestCase(0f, 1f, 2)]
        [TestCase(1f, 0f, 3)]
        [TestCase(3f, -5f, 0, TestName = "schuin: de langste as wint")]
        [TestCase(0f, 0f, 0, TestName = "geen richting: zuid")]
        public void TheFrontFacesTheRoad(float x, float z, int expected)
        {
            Assert.That(BuildingPlacement.QuarterTurnsToFace(new float2(x, z)), Is.EqualTo(expected));
        }

        [Test]
        public void TheFrontOfARotatedFootprintPointsAtTheRoad()
        {
            // De voorkant is lokaal -Z; Footprint draait met de klok mee, zoals Unity's Y-rotatie.
            foreach (var (direction, turns) in new[] { (new float2(0f, -1f), 0), (new float2(-1f, 0f), 1), (new float2(0f, 1f), 2), (new float2(1f, 0f), 3) })
            {
                var yaw = math.radians(new Footprint(default, 2, 2, BuildingPlacement.QuarterTurnsToFace(direction)).YawDegrees);
                var front = new float2(-math.sin(yaw), -math.cos(yaw));

                Assert.That(math.distance(front, direction), Is.LessThan(1e-4f), $"{turns} kwartslagen");
            }
        }

        [Test]
        public void EveryCandidateContainsTheAnchorAndKeepsTheRotation()
        {
            var anchor = new CellCoord(10, 10);
            var candidates = BuildingPlacement.Candidates(anchor, 3, 2, 1);

            Assert.That(candidates, Has.Count.EqualTo(6), "2 breed × 3 hoog door de draaiing");
            Assert.That(candidates.All(c => c.Contains(anchor) && c.QuarterTurns == 1 && c.Width == 3), Is.True);
            Assert.That(candidates.Select(c => c.Origin).Distinct().Count(), Is.EqualTo(6));
        }

        [Test]
        public void CellsListsTheWholePlot()
        {
            var cells = BuildingPlacement.Cells(new Footprint(new CellCoord(2, 3), 2, 3, 0)).ToList();

            Assert.That(cells, Has.Count.EqualTo(6));
            Assert.That(cells, Has.Member(new CellCoord(3, 5)));
        }

        [Test]
        public void WeightedPickFollowsTheWeights()
        {
            var items = new[] { 1, 3 };

            Assert.That(BuildingPlacement.PickWeighted(items, i => i, 0.0), Is.EqualTo(1));
            Assert.That(BuildingPlacement.PickWeighted(items, i => i, 0.24), Is.EqualTo(1));
            Assert.That(BuildingPlacement.PickWeighted(items, i => i, 0.26), Is.EqualTo(3));
            Assert.That(BuildingPlacement.PickWeighted(items, i => i, 0.9999), Is.EqualTo(3));
            Assert.That(BuildingPlacement.PickWeighted(items, i => 0, 0.5), Is.EqualTo(0), "niets te kiezen");
        }
    }
}
