using Cities.World.Grid;
using Cities.World.Trees;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Trees
{
    public class TreePlacementTests
    {
        [Test]
        public void CountFollowsTheDensityOnAverage()
        {
            Assert.That(TreePlacement.Count(3, 4, 0), Is.EqualTo(0));
            Assert.That(TreePlacement.Count(3, 4, 255), Is.EqualTo(TreePlacement.MaxPerCell));

            var total = 0;
            for (var x = 0; x < 100; x++)
            {
                for (var z = 0; z < 100; z++)
                {
                    total += TreePlacement.Count(x, z, 64);
                }
            }

            // 64/255 × 2 = 0,5 boom per cel.
            Assert.That(total / 10000f, Is.EqualTo(0.5f).Within(0.03f));
        }

        [Test]
        public void TreesAreDeterministicAndStayInTheirHalfOfTheCell()
        {
            var first = TreePlacement.Tree(10, 20, 0, 8f, 0.5f);
            var second = TreePlacement.Tree(10, 20, 1, 8f, 0.5f);

            Assert.That(TreePlacement.Tree(10, 20, 0, 8f, 0.5f).Position, Is.EqualTo(first.Position));
            Assert.That(first.Position.x, Is.InRange(80f, 84f));
            Assert.That(second.Position.x, Is.InRange(84f, 88f));
            Assert.That(first.Position.y, Is.InRange(160.8f, 167.2f));
            Assert.That(first.Scale, Is.InRange(0.75f, 1.25f));
            Assert.That(first.YawDegrees, Is.InRange(0f, 360f));
        }

        [Test]
        public void SpeciesFollowTheConiferShare()
        {
            var conifers = 0;
            for (var x = 0; x < 200; x++)
            {
                if (TreePlacement.Tree(x, 7, 0, 8f, 0.25f).Species == TreeSpecies.Conifer)
                {
                    conifers++;
                }
            }

            Assert.That(conifers / 200f, Is.EqualTo(0.25f).Within(0.08f));
            Assert.That(TreePlacement.Tree(1, 1, 0, 8f, 0f).Species, Is.EqualTo(TreeSpecies.Broadleaf));
            Assert.That(TreePlacement.Tree(1, 1, 0, 8f, 1f).Species, Is.EqualTo(TreeSpecies.Conifer));
        }

        [Test]
        public void HighlandsHaveMoreConifers()
        {
            Assert.That(TreePlacement.ConiferShare(30f), Is.EqualTo(0.15f).Within(1e-4f));
            Assert.That(TreePlacement.ConiferShare(250f), Is.EqualTo(0.95f).Within(1e-4f));
        }

        [Test]
        public void ForestsSkipFieldsBuildingsWaterRockAndPeaks()
        {
            var settings = ForestSettings.Default;
            var dense = new float2(0f, 0f);
            for (var x = 0f; x < 20000f && ForestShape.Density(dense, 40f, 3f, 0f, GroundCover.Natural, settings) < 255; x += 37f)
            {
                dense = new float2(x, x * 0.7f);
            }

            Assert.That(ForestShape.Density(dense, 40f, 3f, 0f, GroundCover.Natural, settings), Is.EqualTo(255), "er is ergens dicht bos");
            Assert.That(ForestShape.Density(dense, 40f, 3f, 0f, GroundCover.Field, settings), Is.EqualTo(0));
            Assert.That(ForestShape.Density(dense, 40f, 3f, 0f, GroundCover.Built, settings), Is.EqualTo(0));
            Assert.That(ForestShape.Density(dense, 40f, 3f, 1f, GroundCover.Natural, settings), Is.EqualTo(0), "water");
            Assert.That(ForestShape.Density(dense, 40f, 45f, 0f, GroundCover.Natural, settings), Is.EqualTo(0), "steile wand");
            Assert.That(ForestShape.Density(dense, 300f, 3f, 0f, GroundCover.Natural, settings), Is.EqualTo(0), "boven de boomgrens");
        }

        [Test]
        public void AboutTheConfiguredShareOfLandIsForest()
        {
            var settings = ForestSettings.Default;
            var forest = 0;
            var open = 0;
            for (var x = 0; x < 120; x++)
            {
                for (var z = 0; z < 120; z++)
                {
                    var density = ForestShape.Density(new float2(x, z) * 150f, 40f, 3f, 0f, GroundCover.Natural, settings);
                    forest += density > 128 ? 1 : 0;
                    open += density <= settings.Scattered + 1 ? 1 : 0;
                }
            }

            Assert.That(forest / 14400f, Is.EqualTo(settings.Coverage).Within(0.1f));
            Assert.That(open, Is.GreaterThan(0), "open land houdt losse bomen");
        }
    }
}
