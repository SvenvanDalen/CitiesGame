using System;
using Cities.Simulation.Utilities;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Utilities
{
    public class PylonRouteTests
    {
        private static bool Everywhere(float2 p) => true;

        // Meer tussen x = 400 en x = 600 en een rots tussen 1000 en 1100.
        private static bool Terrain(float2 p) => !(p.x > 400f && p.x < 600f) && !(p.x > 1000f && p.x < 1100f);

        [Test]
        public void ShortLineNeedsOnlyTheTwoEnds()
        {
            var route = PylonRoute.Plan(float2.zero, new float2(200f, 0f), 250f, Everywhere);

            Assert.That(route, Has.Count.EqualTo(2));
        }

        [Test]
        public void OpenLandGetsPylonsAtMaximumSpan()
        {
            var route = PylonRoute.Plan(float2.zero, new float2(1000f, 0f), 250f, Everywhere);

            Assert.That(route, Has.Count.EqualTo(5));
            Assert.That(route[1].x, Is.EqualTo(250f).Within(1e-3f));
            Assert.That(route[4].x, Is.EqualTo(1000f));
        }

        [Test]
        public void LineSpansWaterAndMountainWithinReachAndNeverStandsInThem()
        {
            var route = PylonRoute.Plan(float2.zero, new float2(1300f, 0f), 250f, Terrain);

            Assert.That(route, Is.Not.Null);
            Assert.That(route, Has.All.Matches<float2>(Terrain));
            for (var i = 1; i < route.Count; i++)
            {
                Assert.That(math.distance(route[i - 1], route[i]), Is.LessThanOrEqualTo(250f + 1e-3f));
            }
        }

        [Test]
        public void GapWiderThanTheSpanCannotBeBridged()
        {
            Assert.That(PylonRoute.Plan(float2.zero, new float2(1300f, 0f), 150f, Terrain), Is.Null);
        }

        [Test]
        public void EndsInWaterAreRefusedAndBadArgumentsRejected()
        {
            Assert.That(PylonRoute.Plan(float2.zero, new float2(500f, 0f), 250f, Terrain), Is.Null);
            Assert.Throws<ArgumentOutOfRangeException>(() => PylonRoute.Plan(float2.zero, float2.zero, 0f, Everywhere));
            Assert.Throws<ArgumentNullException>(() => PylonRoute.Plan(float2.zero, float2.zero, 100f, null));
        }

        [Test]
        public void TransformerPassesTheLoadUpToItsRatingMinusLosses()
        {
            Assert.That(PylonRoute.TransformerOutput(50f, 100f), Is.EqualTo(49f).Within(1e-4f));
            Assert.That(PylonRoute.TransformerOutput(500f, 100f), Is.EqualTo(98f).Within(1e-4f));
            Assert.Throws<ArgumentOutOfRangeException>(() => PylonRoute.TransformerOutput(-1f, 100f));
            Assert.Throws<ArgumentOutOfRangeException>(() => PylonRoute.TransformerOutput(1f, 100f, 1.5f));
        }
    }
}
