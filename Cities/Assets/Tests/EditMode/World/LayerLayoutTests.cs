using System;
using Cities.World.Grid;
using NUnit.Framework;
using UnityEngine;

namespace Cities.Tests.EditMode.World
{
    public class LayerLayoutTests
    {
        private static readonly WorldDimensions World = new WorldDimensions(2, 2, 8, 8f);

        [Test]
        public void CellLayoutMatchesWorld()
        {
            var layout = LayerLayout.ForCells(World);

            Assert.That(layout.Width, Is.EqualTo(16));
            Assert.That(layout.Depth, Is.EqualTo(16));
            Assert.That(layout.Spacing, Is.EqualTo(8f));
            Assert.That(layout.Length, Is.EqualTo(256));
            Assert.That(layout.Sampling, Is.EqualTo(LayerSampling.CellCentered));
        }

        [Test]
        public void CoarseLayoutCoversBlocksOfCells()
        {
            var layout = LayerLayout.ForCells(World, 4);

            Assert.That(layout.Width, Is.EqualTo(4));
            Assert.That(layout.Spacing, Is.EqualTo(32f));
            Assert.That(layout.IndexOfCell(new CellCoord(7, 5)), Is.EqualTo(layout.Index(1, 1)));
            Assert.That(layout.IndexOfCell(new CellCoord(4, 4)), Is.EqualTo(layout.IndexOfCell(new CellCoord(7, 7))));
        }

        [TestCase(0)]
        [TestCase(3)]
        [TestCase(16)]
        public void CoarseFactorMustDivideTile(int factor)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => LayerLayout.ForCells(World, factor));
        }

        [Test]
        public void CornerLayoutHasOneExtraSample()
        {
            var layout = LayerLayout.ForCorners(World);

            Assert.That(layout.Width, Is.EqualTo(17));
            Assert.That(layout.Depth, Is.EqualTo(17));
            Assert.That(layout.SamplePosition(16, 16), Is.EqualTo(new Vector2(128f, 128f)));
        }

        [Test]
        public void CellSamplesSitAtCellCentres()
        {
            Assert.That(LayerLayout.ForCells(World, 2).SamplePosition(1, 0), Is.EqualTo(new Vector2(24f, 8f)));
        }

        [Test]
        public void IndexIsRowMajorAndChecked()
        {
            var layout = LayerLayout.ForCells(World);

            Assert.That(layout.Index(3, 2), Is.EqualTo(35));
            Assert.That(layout.Contains(15, 15), Is.True);
            Assert.That(layout.Contains(16, 0), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => layout.Index(-1, 0));
        }

        [Test]
        public void BilinearOnCornersInterpolatesBetweenCorners()
        {
            var layout = LayerLayout.ForCorners(World);

            var sample = layout.GetBilinear(new Vector2(12f, 20f));

            Assert.That(sample.Index00, Is.EqualTo(layout.Index(1, 2)));
            Assert.That(sample.Index10, Is.EqualTo(layout.Index(2, 2)));
            Assert.That(sample.Index01, Is.EqualTo(layout.Index(1, 3)));
            Assert.That(sample.Index11, Is.EqualTo(layout.Index(2, 3)));
            Assert.That(sample.Tx, Is.EqualTo(0.5f));
            Assert.That(sample.Tz, Is.EqualTo(0.5f));
            Assert.That(sample.Interpolate(0f, 2f, 4f, 6f), Is.EqualTo(3f));
        }

        [Test]
        public void BilinearClampsOutsideAndAtFarEdge()
        {
            var layout = LayerLayout.ForCorners(World);

            var outside = layout.GetBilinear(new Vector2(-50f, 500f));
            Assert.That(outside.Index00, Is.EqualTo(layout.Index(0, 15)));
            Assert.That(outside.Tx, Is.EqualTo(0f));
            Assert.That(outside.Tz, Is.EqualTo(1f));

            var farCorner = layout.GetBilinear(new Vector2(128f, 128f));
            Assert.That(farCorner.Index11, Is.EqualTo(layout.Index(16, 16)));
            Assert.That(farCorner.Interpolate(0f, 0f, 0f, 9f), Is.EqualTo(9f));
        }

        [Test]
        public void BilinearOnCellsUsesCellCentres()
        {
            var layout = LayerLayout.ForCells(World, 4);

            var atCentre = layout.GetBilinear(new Vector2(48f, 16f));

            Assert.That(atCentre.Index00, Is.EqualTo(layout.Index(1, 0)));
            Assert.That(atCentre.Tx, Is.EqualTo(0f));
            Assert.That(atCentre.Tz, Is.EqualTo(0f));
        }

        [Test]
        public void SingleSampleLayoutDoesNotOverflow()
        {
            var layout = LayerLayout.ForCells(new WorldDimensions(1, 1, 4, 8f), 4);

            var sample = layout.GetBilinear(new Vector2(100f, 100f));

            Assert.That(sample.Index00, Is.EqualTo(0));
            Assert.That(sample.Index11, Is.EqualTo(0));
        }

        [Test]
        public void LayoutEquality()
        {
            var a = LayerLayout.ForCorners(World);

            Assert.That(a == LayerLayout.ForCorners(World), Is.True);
            Assert.That(a != LayerLayout.ForCells(World), Is.True);
            Assert.That(a.Equals((object)LayerLayout.ForCorners(World)), Is.True);
            Assert.That(a.Equals(0), Is.False);
            Assert.That(a.GetHashCode(), Is.EqualTo(LayerLayout.ForCorners(World).GetHashCode()));
            Assert.That(a.ToString(), Is.EqualTo("17×17 Corners @ 8 m"));
        }
    }
}
