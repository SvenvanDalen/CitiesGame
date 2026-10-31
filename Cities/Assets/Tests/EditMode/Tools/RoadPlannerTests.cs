using System.Linq;
using Cities.Core.Diagnostics;
using Cities.Gameplay.Tools;
using Cities.Simulation.Network;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Tools
{
    public class RoadPlannerTests
    {
        [Test]
        public void SnapRoundsAngleAndLengthAroundTheStart()
        {
            var start = new float3(100f, 7f, 100f);

            // 33 m onder ongeveer 36° → 35 m onder 30° (hoekstap 15°, lengtestap 5 m).
            var raw = start + new float3(math.sin(math.radians(36f)), 0f, math.cos(math.radians(36f))) * 33f;
            var snapped = RoadPlanner.Snap(start, raw + new float3(0f, 2f, 0f), 15f, 5f);

            Assert.That(math.distance(snapped.xz, start.xz), Is.EqualTo(35f).Within(1e-3f));
            Assert.That(RoadPlanner.Yaw(snapped - start), Is.EqualTo(30f).Within(1e-3f));
            Assert.That(snapped.y, Is.EqualTo(9f), "de hoogte blijft die van het punt");
        }

        [Test]
        public void SnapFollowsTheReferenceYawAndCanBeSwitchedOff()
        {
            var start = float3.zero;

            var along = RoadPlanner.Snap(start, new float3(10f, 0f, 9f), 90f, 0f, referenceYaw: 45f);
            var off = RoadPlanner.Snap(start, new float3(13f, 0f, 4f), 0f, 0f);

            Assert.That(RoadPlanner.Yaw(along), Is.EqualTo(45f).Within(1e-3f), "ten opzichte van de bestaande weg");
            Assert.That(off, Is.EqualTo(new float3(13f, 0f, 4f)));
            Assert.That(RoadPlanner.Snap(start, start, 15f, 5f), Is.EqualTo(start), "zonder afstand valt er niets te snappen");
            Assert.That(math.length(RoadPlanner.Snap(start, new float3(0.2f, 0f, 0f), 15f, 5f)), Is.EqualTo(5f).Within(1e-3f), "minstens één lengtestap");
        }

        [Test]
        public void YawCountsClockwiseFromNorth()
        {
            Assert.That(RoadPlanner.Yaw(new float3(0f, 0f, 1f)), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(RoadPlanner.Yaw(new float3(1f, 0f, 0f)), Is.EqualTo(90f).Within(1e-4f));
            Assert.That(RoadPlanner.Yaw(new float3(-1f, 0f, 0f)), Is.EqualTo(270f).Within(1e-4f));
        }

        [Test]
        public void QuadraticCurveTouchesTheControlPointHalfway()
        {
            var curve = RoadPlanner.Quadratic(new float3(0f, 0f, 0f), new float3(0f, 0f, 40f), new float3(40f, 0f, 40f));

            Assert.That(curve.Position(0f), Is.EqualTo(new float3(0f, 0f, 0f)));
            Assert.That(curve.Position(1f), Is.EqualTo(new float3(40f, 0f, 40f)));
            Assert.That(curve.Position(0.5f), Is.EqualTo(new float3(10f, 0f, 30f)).Using(new Float3Comparer()), "kwadratisch: gemiddelde van begin, 2×buigpunt en eind");
        }

        [Test]
        public void ShortAndLongSegmentsAreRejected()
        {
            Assert.That(RoadPlanner.Check(Bezier3.Straight(float3.zero, new float3(3f, 0f, 0f))), Is.EqualTo("Te kort"));
            Assert.That(RoadPlanner.Check(Bezier3.Straight(float3.zero, new float3(2000f, 0f, 0f))), Is.EqualTo("Te lang"));
            Assert.That(RoadPlanner.Check(Bezier3.Straight(float3.zero, new float3(100f, 0f, 0f))), Is.Null);
        }

        [Test]
        public void ThePreviewOverlayDrawsCurveAndMarkerInTheRightColour()
        {
            var overlay = new RoadPreviewOverlay();
            var view = new DebugView(float3.zero, float3.zero, 1000f);
            var geometry = new DebugGeometry();

            overlay.ShowCurve(Bezier3.Straight(float3.zero, new float3(30f, 0f, 0f)), valid: true);
            overlay.Collect(geometry, view);
            Assert.That(geometry.Lines.Count, Is.EqualTo(24 + 2));
            Assert.That(geometry.Lines.All(l => l.Color == DebugColor.Good), Is.True);

            geometry = new DebugGeometry();
            overlay.ShowMarker(float3.zero, valid: false);
            overlay.Collect(geometry, view);
            Assert.That(geometry.Lines.Select(l => l.Color), Is.All.EqualTo(DebugColor.Bad));
            Assert.That(geometry.Lines.Count, Is.EqualTo(2));

            overlay.Clear();
            geometry = new DebugGeometry();
            overlay.Collect(geometry, view);
            Assert.That(geometry.Lines, Is.Empty);
            Assert.That(overlay.Name, Is.EqualTo("Aanleg"));
            Assert.Throws<System.ArgumentNullException>(() => overlay.Collect(null, view));
        }

        [Test]
        public void DistanceToACurveIsMeasuredToTheNearestPointOnIt()
        {
            var straight = Bezier3.Straight(new float3(0f, 5f, 0f), new float3(100f, 9f, 0f));

            Assert.That(RoadPlanner.DistanceTo(straight, new float3(50f, 0f, 7f)), Is.EqualTo(7f).Within(1e-3f), "zijdelings, hoogte telt niet mee");
            Assert.That(RoadPlanner.DistanceTo(straight, new float3(-10f, 0f, 0f)), Is.EqualTo(10f).Within(1e-3f), "voor het begin");
            Assert.That(RoadPlanner.DistanceTo(straight, new float3(130f, 0f, 40f)), Is.EqualTo(50f).Within(1e-3f), "voorbij het eind");
            Assert.That(RoadPlanner.DistanceTo(Bezier3.Straight(float3.zero, float3.zero), new float3(3f, 0f, 4f)), Is.EqualTo(5f).Within(1e-3f), "een punt");
        }

        [Test]
        public void GridSnapRoundsToTheNearestGridLineAndKeepsTheHeight()
        {
            Assert.That(RoadPlanner.SnapToGrid(new float3(13f, 7f, 27f), 8f), Is.EqualTo(new float3(16f, 7f, 24f)).Using(new Float3Comparer()));
            Assert.That(RoadPlanner.SnapToGrid(new float3(13f, 7f, 27f), 0f), Is.EqualTo(new float3(13f, 7f, 27f)).Using(new Float3Comparer()), "stap 0 snapt niet");
        }

        [Test]
        public void DescribeShowsLengthHeadingAndTheHeightDifference()
        {
            var up = Bezier3.Straight(new float3(0f, 0f, 0f), new float3(0f, 5f, 100f));
            var down = Bezier3.Straight(new float3(0f, 5f, 0f), new float3(100f, 0f, 0f));

            Assert.That(RoadPlanner.Describe(up), Does.StartWith("100.1 m · 0°").And.Contain("+5.0 m (5.0 %)"));
            Assert.That(RoadPlanner.Describe(down), Does.Contain("90°").And.Contain("-5.0 m"));
        }

        [Test]
        public void NearestTFindsWhereOnTheCurveAPointIs()
        {
            var straight = Bezier3.Straight(new float3(0f, 0f, 0f), new float3(100f, 0f, 0f));

            Assert.That(RoadPlanner.NearestT(straight, new float3(25f, 0f, 9f)), Is.EqualTo(0.25f).Within(0.01f));
            Assert.That(RoadPlanner.NearestT(straight, new float3(-30f, 0f, 0f)), Is.EqualTo(0f).Within(1e-4f), "voor het begin");
            Assert.That(RoadPlanner.NearestT(straight, new float3(300f, 0f, 0f)), Is.EqualTo(1f).Within(1e-4f), "voorbij het eind");
        }

        private sealed class Float3Comparer : System.Collections.Generic.IEqualityComparer<float3>
        {
            public bool Equals(float3 x, float3 y) => math.distance(x, y) < 1e-3f;

            public int GetHashCode(float3 obj) => 0;
        }
    }
}
