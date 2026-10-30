using Cities.World.Picking;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Picking
{
    public class PickGeometryTests
    {
        private static readonly float3 Down = new float3(0f, -1f, 0f);

        [Test]
        public void AabbIsHitFromAboveAndFromInside()
        {
            Assert.That(PickGeometry.RayAabb(new float3(0f, 100f, 0f), Down, new float3(-1f), new float3(1f), 1000f, out var distance), Is.True);
            Assert.That(distance, Is.EqualTo(99f).Within(1e-4f));

            Assert.That(PickGeometry.RayAabb(float3.zero, Down, new float3(-1f), new float3(1f), 1000f, out distance), Is.True);
            Assert.That(distance, Is.Zero, "Binnen de doos");
        }

        [Test]
        public void AabbMissesBesideBehindAndBeyondTheLimit()
        {
            Assert.That(PickGeometry.RayAabb(new float3(5f, 100f, 0f), Down, new float3(-1f), new float3(1f), 1000f, out _), Is.False, "Ernaast");
            Assert.That(PickGeometry.RayAabb(new float3(0f, -10f, 0f), Down, new float3(-1f), new float3(1f), 1000f, out _), Is.False, "Erachter");
            Assert.That(PickGeometry.RayAabb(new float3(0f, 100f, 0f), Down, new float3(-1f), new float3(1f), 50f, out _), Is.False, "Te ver");
        }

        [Test]
        public void AabbHandlesRaysAlongAFace()
        {
            // Precies op het vlak x = 1, evenwijdig eraan: telt als raak.
            Assert.That(PickGeometry.RayAabb(new float3(1f, 100f, 0f), Down, new float3(-1f), new float3(1f), 1000f, out var distance), Is.True);
            Assert.That(distance, Is.EqualTo(99f).Within(1e-4f));
        }

        [Test]
        public void RotatedBoxFollowsUnitysYaw()
        {
            // 90 graden om Y: lokaal X (lang, 10 m) ligt langs wereld Z.
            var center = new float3(0f, 5f, 0f);
            var half = new float3(10f, 5f, 1f);
            var yaw = math.radians(90f);

            Assert.That(PickGeometry.RayBox(new float3(0f, 100f, 9f), Down, center, half, yaw, 1000f, out var distance), Is.True);
            Assert.That(distance, Is.EqualTo(90f).Within(1e-3f));
            Assert.That(PickGeometry.RayBox(new float3(9f, 100f, 0f), Down, center, half, yaw, 1000f, out _), Is.False);
            Assert.That(PickGeometry.RayBox(new float3(9f, 100f, 0f), Down, center, half, 0f, 1000f, out _), Is.True, "Zonder draaiing ligt hij langs X");
        }

        [Test]
        public void SphereIsHitAtItsSurface()
        {
            Assert.That(PickGeometry.RaySphere(new float3(0f, 10f, 0f), Down, float3.zero, 2f, 100f, out var distance), Is.True);
            Assert.That(distance, Is.EqualTo(8f).Within(1e-4f));
            Assert.That(PickGeometry.RaySphere(float3.zero, Down, float3.zero, 2f, 100f, out distance), Is.True);
            Assert.That(distance, Is.Zero);
            Assert.That(PickGeometry.RaySphere(new float3(3f, 10f, 0f), Down, float3.zero, 2f, 100f, out _), Is.False, "Ernaast");
            Assert.That(PickGeometry.RaySphere(new float3(0f, -10f, 0f), Down, float3.zero, 2f, 100f, out _), Is.False, "Erachter");
            Assert.That(PickGeometry.RaySphere(new float3(0f, 10f, 0f), Down, float3.zero, 2f, 5f, out _), Is.False, "Te ver");
        }

        [Test]
        public void CapsuleIsHitOnItsSideAndItsEnds()
        {
            var a = new float3(-10f, 0f, 0f);
            var b = new float3(10f, 0f, 0f);

            Assert.That(PickGeometry.RayCapsule(new float3(3f, 10f, 0f), Down, a, b, 2f, 100f, out var side), Is.True);
            Assert.That(side, Is.EqualTo(8f).Within(1e-3f));
            Assert.That(PickGeometry.RayCapsule(new float3(11f, 10f, 0f), Down, a, b, 2f, 100f, out var end), Is.True);
            Assert.That(end, Is.EqualTo(10f - math.sqrt(3f)).Within(1e-3f), "Eindbol");
            Assert.That(PickGeometry.RayCapsule(new float3(13f, 10f, 0f), Down, a, b, 2f, 100f, out _), Is.False);
            Assert.That(PickGeometry.RayCapsule(new float3(0f, 10f, 3f), Down, a, b, 2f, 100f, out _), Is.False);
        }

        [Test]
        public void CapsuleHandlesInsideAlongTheAxisAndDegenerateCases()
        {
            var a = new float3(-10f, 0f, 0f);
            var b = new float3(10f, 0f, 0f);

            Assert.That(PickGeometry.RayCapsule(float3.zero, Down, a, b, 2f, 100f, out var inside), Is.True);
            Assert.That(inside, Is.Zero, "Binnenin");
            Assert.That(PickGeometry.RayCapsule(new float3(-30f, 0f, 0f), new float3(1f, 0f, 0f), a, b, 2f, 100f, out var along), Is.True);
            Assert.That(along, Is.EqualTo(18f).Within(1e-3f), "Langs de as raakt de eerste eindbol");
            Assert.That(PickGeometry.RayCapsule(new float3(0f, 10f, 0f), Down, float3.zero, float3.zero, 1f, 100f, out var point), Is.True);
            Assert.That(point, Is.EqualTo(9f).Within(1e-4f), "Zonder lengte is het een bol");
            Assert.That(PickGeometry.RayCapsule(new float3(3f, 10f, 0f), Down, a, b, 2f, 5f, out _), Is.False, "Te ver");
        }

        [Test]
        public void SegmentDistanceFindsTheClosestApproach()
        {
            var a = new float3(-10f, 0f, 0f);
            var b = new float3(10f, 0f, 0f);

            var gap = PickGeometry.RaySegmentDistance(new float3(3f, 10f, 4f), Down, a, b, 100f, out var along);
            Assert.That(gap, Is.EqualTo(4f).Within(1e-4f));
            Assert.That(along, Is.EqualTo(10f).Within(1e-4f));

            gap = PickGeometry.RaySegmentDistance(new float3(15f, 10f, 0f), Down, a, b, 100f, out along);
            Assert.That(gap, Is.EqualTo(5f).Within(1e-4f), "Voorbij het eind telt het eindpunt");

            gap = PickGeometry.RaySegmentDistance(new float3(0f, 10f, 0f), Down, a, b, 4f, out along);
            Assert.That(along, Is.EqualTo(4f).Within(1e-4f), "Niet voorbij de maximale afstand");
            Assert.That(gap, Is.EqualTo(6f).Within(1e-4f));

            gap = PickGeometry.RaySegmentDistance(new float3(0f, 10f, 2f), Down, float3.zero, float3.zero, 100f, out along);
            Assert.That(gap, Is.EqualTo(2f).Within(1e-4f), "Een punt");

            gap = PickGeometry.RaySegmentDistance(new float3(-20f, 0f, 3f), new float3(1f, 0f, 0f), a, b, 100f, out _);
            Assert.That(gap, Is.EqualTo(3f).Within(1e-4f), "Evenwijdig");
        }
    }
}
