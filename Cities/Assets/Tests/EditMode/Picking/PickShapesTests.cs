using Cities.Core.Spatial;
using Cities.World.Picking;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Picking
{
    public class PickShapesTests
    {
        private static readonly PickRay DownAt500 = new PickRay(new float3(500f, 100f, 500f), new float3(0f, -1f, 0f));

        [Test]
        public void BoxFootprintCoversTheRotatedBox()
        {
            var box = new PickBox(1, new float3(100f, 10f, 200f), new float3(10f, 10f, 2f), math.radians(90f));

            var footprint = box.Footprint(1f);

            Assert.That(footprint.Min.x, Is.EqualTo(97f).Within(1e-4f));
            Assert.That(footprint.Max.x, Is.EqualTo(103f).Within(1e-4f));
            Assert.That(footprint.Min.y, Is.EqualTo(189f).Within(1e-4f));
            Assert.That(footprint.Max.y, Is.EqualTo(211f).Within(1e-4f));
        }

        [Test]
        public void BoxesHaveNoMargin()
        {
            var box = new PickBox(1, new float3(500f, 5f, 500f), new float3(2f, 5f, 2f), 0f);
            var beside = new PickRay(new float3(503f, 100f, 500f), new float3(0f, -1f, 0f));

            Assert.That(box.Test(DownAt500, 1000f, 0.1f, 5f, out var distance, out var score), Is.True);
            Assert.That(distance, Is.EqualTo(90f).Within(1e-4f));
            Assert.That(score, Is.Zero);
            Assert.That(box.Test(beside, 1000f, 0.1f, 5f, out _, out _), Is.False);
        }

        [Test]
        public void CapsuleScoresRealAndMarginHits()
        {
            var road = new PickCapsule(2, new float3(400f, 0f, 500f), new float3(600f, 0f, 500f), 3f);
            var near = new PickRay(new float3(500f, 100f, 505f), new float3(0f, -1f, 0f));
            var far = new PickRay(new float3(500f, 100f, 520f), new float3(0f, -1f, 0f));

            Assert.That(road.Test(DownAt500, 1000f, 0.05f, 4f, out var distance, out var score), Is.True);
            Assert.That(score, Is.Zero);
            Assert.That(distance, Is.EqualTo(97f).Within(1e-3f));
            Assert.That(road.Test(near, 1000f, 0.05f, 4f, out _, out score), Is.True, "5 m ernaast valt binnen 3 + 4 m");
            Assert.That(score, Is.EqualTo(5f / 7f).Within(1e-3f));
            Assert.That(road.Test(far, 1000f, 0.05f, 4f, out _, out _), Is.False);
            Assert.That(road.Test(near, 1000f, 0f, 4f, out _, out _), Is.False, "Zonder marge mis");
            Assert.That(road.Footprint(2f), Is.EqualTo(new Aabb2(new float2(395f, 495f), new float2(605f, 505f))));
        }

        [Test]
        public void AgentStandsOnTheGround()
        {
            var citizen = new PickAgent(3, new float3(500f, 20f, 500f), 0.3f, 1.8f);
            var overHead = new PickRay(new float3(500f, 100f, 500f), new float3(0f, -1f, 0f));
            var sideways = new PickRay(new float3(400f, 1f, 500f), new float3(1f, 0f, 0f));

            Assert.That(citizen.Test(overHead, 1000f, 0f, 0f, out var distance, out _), Is.True);
            Assert.That(distance, Is.EqualTo(80f - 1.8f).Within(1e-3f), "Op het hoofd");
            Assert.That(citizen.Test(sideways, 1000f, 0f, 0f, out _, out _), Is.False, "Onder de voeten door");
            Assert.That(citizen.Footprint(1f), Is.EqualTo(Aabb2.FromCircle(new float2(500f, 500f), 1.3f)));
            Assert.That(citizen.Id, Is.EqualTo(3UL));
            Assert.That(citizen.Radius, Is.EqualTo(0.3f));
            Assert.That(citizen.Height, Is.EqualTo(1.8f));
        }

        [Test]
        public void FlatAgentsKeepAValidAxis()
        {
            // Lager dan twee keer de straal: de as krimpt tot een punt halverwege.
            var car = new PickAgent(4, new float3(500f, 0f, 500f), 1f, 1.5f);

            Assert.That(car.Test(DownAt500, 1000f, 0f, 0f, out var distance, out _), Is.True);
            Assert.That(distance, Is.EqualTo(100f - 1.75f).Within(1e-3f));
        }
    }
}
