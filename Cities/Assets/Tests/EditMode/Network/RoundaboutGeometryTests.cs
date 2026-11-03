using System;
using Cities.Simulation.Network;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Network
{
    public class RoundaboutGeometryTests
    {
        [Test]
        public void RadiusAndArmsStayWithinTheLimits()
        {
            Assert.That(RoundaboutGeometry.Validate(20f, 4), Is.Null);
            Assert.That(RoundaboutGeometry.Validate(12f, 2), Is.Null);
            Assert.That(RoundaboutGeometry.Validate(40f, RoadNetwork.MaxArms), Is.Null);
            Assert.That(RoundaboutGeometry.Validate(11.9f, 4), Does.Contain("straal"));
            Assert.That(RoundaboutGeometry.Validate(40.1f, 4), Does.Contain("straal"));
            Assert.That(RoundaboutGeometry.Validate(float.NaN, 4), Does.Contain("straal"));
            Assert.That(RoundaboutGeometry.Validate(20f, 1), Does.Contain("armen"));
            Assert.That(RoundaboutGeometry.Validate(20f, RoadNetwork.MaxArms + 1), Does.Contain("armen"));
        }

        [Test]
        public void ArmsLieEvenlyOnTheCircleCounterClockwise()
        {
            var center = new float3(100f, 5f, 200f);

            var first = RoundaboutGeometry.Point(center, 20f, RoundaboutGeometry.Angle(0, 4, 0f));
            var second = RoundaboutGeometry.Point(center, 20f, RoundaboutGeometry.Angle(1, 4, 0f));

            Assert.That(math.distance(first, new float3(120f, 5f, 200f)), Is.LessThan(1e-3f));
            Assert.That(math.distance(second, new float3(100f, 5f, 220f)), Is.LessThan(1e-3f), "na een kwartslag naar +Z: tegen de klok in");
        }

        [Test]
        public void AnArcStaysOnTheCircleAndHasTheRightLength()
        {
            var center = new float3(0f, 3f, 0f);
            var arc = RoundaboutGeometry.Arc(center, 25f, 0f, math.PI / 2f);

            for (var t = 0f; t <= 1f; t += 0.125f)
            {
                Assert.That(math.distance(arc.Position(t).xz, center.xz), Is.EqualTo(25f).Within(0.05f));
            }

            Assert.That(arc.Length(), Is.EqualTo(math.PI / 2f * 25f).Within(0.05f));
            Assert.That(arc.Direction(0f).z, Is.GreaterThan(0.99f), "begint tegen de klok in");
        }

        [Test]
        public void ACircleOfTwoHalfArcsStillFits()
        {
            var arc = RoundaboutGeometry.Arc(float3.zero, 12f, 0f, math.PI);

            Assert.That(math.distance(arc.Position(0.5f).xz, float2.zero), Is.EqualTo(12f).Within(0.2f));
            Assert.Throws<ArgumentException>(() => RoundaboutGeometry.Arc(float3.zero, 12f, 1f, 1f));
        }
    }
}
