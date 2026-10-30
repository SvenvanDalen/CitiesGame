using Cities.Simulation.Network;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Network
{
    public class Bezier3Tests
    {
        [Test]
        public void StraightCurveRunsEvenlyFromStartToEnd()
        {
            var curve = Bezier3.Straight(new float3(0f, 5f, 0f), new float3(30f, 5f, 0f));

            Assert.That(curve.Position(0.5f), Is.EqualTo(new float3(15f, 5f, 0f)));
            Assert.That(curve.Length(), Is.EqualTo(30f).Within(1e-3f));
            Assert.That(curve.Direction(0.2f), Is.EqualTo(new float3(1f, 0f, 0f)));
            Assert.That(curve.Reversed.P0, Is.EqualTo(curve.P3));
        }

        [Test]
        public void QuarterTurnFromTangentsIsCloseToACircle()
        {
            // Bocht van 90° met straal 50 m: lengte π/2 × 50 ≈ 78,5 m.
            var curve = Bezier3.FromTangents(new float3(0f, 0f, 0f), new float3(0f, 0f, 1f), new float3(50f, 0f, 50f), new float3(1f, 0f, 0f));

            Assert.That(curve.Direction(0f), Is.EqualTo(new float3(0f, 0f, 1f)));
            Assert.That(curve.Direction(1f).x, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(curve.Length(), Is.EqualTo(78.5f).Within(2f));
        }

        [Test]
        public void OffsetShiftsToTheRightOfTheDirection()
        {
            var curve = Bezier3.Straight(new float3(0f, 0f, 0f), new float3(0f, 0f, 20f));

            var right = curve.Offset(3f);
            var left = curve.Offset(-3f);

            Assert.That(right.P0, Is.EqualTo(new float3(3f, 0f, 0f)), "rijdend naar +z is rechts +x");
            Assert.That(right.P3, Is.EqualTo(new float3(3f, 0f, 20f)));
            Assert.That(left.P0, Is.EqualTo(new float3(-3f, 0f, 0f)));
        }

        [Test]
        public void DegenerateHandlesStillHaveADirection()
        {
            var point = new float3(1f, 2f, 3f);
            var curve = new Bezier3(point, point, new float3(1f, 2f, 13f), new float3(1f, 2f, 13f));

            Assert.That(curve.Direction(0f), Is.EqualTo(new float3(0f, 0f, 1f)));
            Assert.That(curve.Derivative(0.5f).z, Is.GreaterThan(0f));
        }

        [Test]
        public void SliceIsTheSamePartOfTheCurve()
        {
            var curve = Bezier3.FromTangents(float3.zero, new float3(0f, 0f, 1f), new float3(30f, 4f, 30f), new float3(1f, 0f, 0f));

            var slice = curve.Slice(0.25f, 0.75f);

            Assert.That(math.distance(slice.P0, curve.Position(0.25f)), Is.LessThan(1e-3f));
            Assert.That(math.distance(slice.P3, curve.Position(0.75f)), Is.LessThan(1e-3f));
            Assert.That(math.distance(slice.Position(0.5f), curve.Position(0.5f)), Is.LessThan(1e-3f));
            Assert.That(math.distance(slice.Position(0.25f), curve.Position(0.375f)), Is.LessThan(1e-3f));
            Assert.That(slice.Length(), Is.LessThan(curve.Length()));
        }

    }
}
