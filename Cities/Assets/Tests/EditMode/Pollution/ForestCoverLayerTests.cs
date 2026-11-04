using System;
using Cities.Gameplay.Pollution;
using Cities.World.Grid;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Pollution
{
    /// <summary>Gebruikt de NativeArray-lagen van <see cref="WorldMap"/> en draait daarom alleen in Unity, niet in de CI-analyse.</summary>
    [Category("UnityRuntime")]
    public class ForestCoverLayerTests
    {
        private static readonly WorldDimensions Dimensions = new WorldDimensions(1, 1, 16, 8f);

        [Test]
        public void BuildAveragesTheTreesPerBlock()
        {
            using var map = new WorldMap(Dimensions);
            var layout = map.Trees.Layout;
            map.Trees[0, 0] = 255;

            var cover = ForestCoverLayer.Build(map, layout.Width / 2, layout.Depth / 2);

            Assert.That(cover[0], Is.EqualTo(0.25f).Within(1e-4f));
            Assert.That(cover[1], Is.Zero);
        }

        [Test]
        public void BuildRejectsAMissingMap()
        {
            Assert.Throws<ArgumentNullException>(() => ForestCoverLayer.Build(null, 1, 1));
        }
    }
}
