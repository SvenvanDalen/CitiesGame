using System;
using System.Linq;
using Cities.Core.Diagnostics;
using Cities.World.Terrain;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Terrain
{
    public class ResourceLayersTests
    {
        private const int Side = 40;
        private static readonly TerrainShapeSettings Settings = TerrainShapeSettings.Default;

        private static float2 At(int i) => new float2(1000f + ((i % Side) * 220f), 1000f + ((i / Side) * 220f));

        private static float[] Sample(ResourceKind kind, TerrainShapeSettings settings) =>
            Enumerable.Range(0, Side * Side).Select(i => ResourceLayers.Richness(kind, At(i), settings)).ToArray();

        [TestCase(ResourceKind.Oil)]
        [TestCase(ResourceKind.Ore)]
        [TestCase(ResourceKind.FertileGround)]
        [TestCase(ResourceKind.Forest)]
        public void EveryLayerStaysInRangeHasDepositsAndRepeatsForTheSameSeed(ResourceKind kind)
        {
            var first = Sample(kind, Settings);

            Assert.That(first, Has.All.InRange(0f, 1f));
            Assert.That(first, Has.Some.GreaterThanOrEqualTo(ResourceLayers.Deposit), "er is ergens een afzetting");
            Assert.That(first, Has.Some.LessThan(ResourceLayers.Deposit), "en niet overal");
            Assert.That(Sample(kind, Settings), Is.EqualTo(first));
        }

        [Test]
        public void AnotherSeedGivesAnotherMap()
        {
            var other = Settings;
            other.Seed = 99;

            Assert.That(Sample(ResourceKind.Ore, other), Is.Not.EqualTo(Sample(ResourceKind.Ore, Settings)));
        }

        [Test]
        public void OreSitsHigherThanOilAndFertileGroundHugsTheRiver()
        {
            float MeanWhereRich(ResourceKind kind, Func<float2, float> measure)
            {
                var spots = Enumerable.Range(0, Side * Side).Select(At).Where(p => ResourceLayers.Richness(kind, p, Settings) >= ResourceLayers.Deposit).ToArray();
                return spots.Length == 0 ? float.NaN : spots.Average(measure);
            }

            float Height(float2 p) => TerrainShape.Height(p, Settings);
            float River(float2 p) => TerrainShape.RiverDistance(p, Settings);

            Assert.That(MeanWhereRich(ResourceKind.Ore, Height), Is.GreaterThan(MeanWhereRich(ResourceKind.Oil, Height)));
            var everywhere = Enumerable.Range(0, Side * Side).Select(At).Average(River);
            Assert.That(MeanWhereRich(ResourceKind.FertileGround, River), Is.LessThan(everywhere));
        }

        [Test]
        public void OverlayDrawsCrossesOnlyOnDepositsAndRejectsBadInput()
        {
            var overlay = new ResourceOverlay(ResourceKind.FertileGround, "Vruchtbaar", DebugColor.Good, Settings, new FlatTerrain(40f));
            var geometry = new DebugGeometry();
            var view = new DebugView(new float3(5200f, 800f, 3000f), new float3(5200f, 40f, 3000f), 1000f);

            overlay.Collect(geometry, view);

            Assert.That(overlay.Name, Is.EqualTo("Vruchtbaar"));
            Assert.That(geometry.Lines, Is.Not.Empty);
            Assert.That(geometry.Lines.Count % 2, Is.Zero, "elk kruisje is twee lijnen");
            Assert.Throws<ArgumentNullException>(() => overlay.Collect(null, view));
            Assert.Throws<ArgumentNullException>(() => new ResourceOverlay(ResourceKind.Oil, null, DebugColor.Good, Settings, new FlatTerrain(0f)));
            Assert.Throws<ArgumentNullException>(() => new ResourceOverlay(ResourceKind.Oil, "x", DebugColor.Good, Settings, null));
        }
    }
}
