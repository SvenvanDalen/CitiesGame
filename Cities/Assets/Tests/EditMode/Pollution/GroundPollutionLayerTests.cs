using System;
using Cities.Gameplay.Pollution;
using Cities.Simulation.Fields;
using Cities.World.Grid;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Pollution
{
    /// <summary>Gebruikt de NativeArray-lagen van <see cref="WorldMap"/> en draait daarom alleen in Unity, niet in de CI-analyse.</summary>
    [Category("UnityRuntime")]
    public class GroundPollutionLayerTests
    {
        private static readonly WorldDimensions Dimensions = new WorldDimensions(1, 1, 16, 8f);

        [Test]
        public void PublishWritesTheLevelPerBlock()
        {
            using var map = new WorldMap(Dimensions);
            var layout = map.GroundPollution.Layout;
            var soil = new SoilPollution(layout.Width, layout.Depth);
            soil.Add(1, 2, SoilPollution.HalfLevel);

            GroundPollutionLayer.Publish(soil, map);

            Assert.That(map.GroundPollution[1, 2], Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(map.GroundPollution[0, 0], Is.Zero);
        }

        [Test]
        public void RestoreGivesBackTheConcentration()
        {
            using var map = new WorldMap(Dimensions);
            var layout = map.GroundPollution.Layout;
            var soil = new SoilPollution(layout.Width, layout.Depth);
            soil.Add(2, 1, 300f);
            GroundPollutionLayer.Publish(soil, map);

            var loaded = new SoilPollution(layout.Width, layout.Depth);
            GroundPollutionLayer.Restore(loaded, map);

            Assert.That(loaded[2, 1], Is.EqualTo(300f).Within(0.1f));
            Assert.That(loaded[0, 0], Is.Zero);
        }

        [Test]
        public void MismatchedSizesAreRejected()
        {
            using var map = new WorldMap(Dimensions);

            Assert.Throws<ArgumentException>(() => GroundPollutionLayer.Publish(new SoilPollution(1, 1), map));
            Assert.Throws<ArgumentNullException>(() => GroundPollutionLayer.Publish(null, map));
        }
    }
}
