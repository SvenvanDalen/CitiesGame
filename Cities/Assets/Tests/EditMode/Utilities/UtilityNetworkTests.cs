using System;
using Cities.Simulation.Utilities;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Utilities
{
    public class UtilityNetworkTests
    {
        [Test]
        public void ALoneConsumerGetsNothingAndALoneProducerServesNobody()
        {
            var net = new UtilityNetwork(0f);
            var consumer = net.Add(float2.zero, 0f, 10f);
            var producer = net.Add(new float2(500f, 0f), 50f, 0f);

            net.Tick();

            Assert.That(net.NetworkCount, Is.EqualTo(2));
            Assert.That(net.Satisfaction(consumer), Is.Zero);
            Assert.That(net.Supplied(consumer), Is.Zero);
            Assert.That(net.Satisfaction(producer), Is.EqualTo(1f), "geen vraag is altijd tevreden");
        }

        [Test]
        public void ALineConnectsAcrossAnyDistance()
        {
            var net = new UtilityNetwork(0f);
            var plant = net.Add(float2.zero, 100f, 0f);
            var home = net.Add(new float2(5000f, 0f), 0f, 40f);

            Assert.That(net.Link(plant, home), Is.True);
            Assert.That(net.Link(home, plant), Is.False, "al verbonden");
            net.Tick();

            Assert.That(net.NetworkOf(plant), Is.EqualTo(net.NetworkOf(home)));
            Assert.That(net.Supplied(home), Is.EqualTo(40f));
            Assert.That(net.Supply(net.NetworkOf(home)), Is.EqualTo(100f));
            Assert.That(net.Demand(net.NetworkOf(home)), Is.EqualTo(40f));
        }

        [Test]
        public void NearbyNodesConnectWithinTheRadiusOnly()
        {
            var net = new UtilityNetwork(20f);
            var plant = net.Add(float2.zero, 100f, 0f);
            var near = net.Add(new float2(15f, 0f), 0f, 10f);
            var chained = net.Add(new float2(30f, 10f), 0f, 10f);
            var far = net.Add(new float2(-200f, -200f), 0f, 10f);

            net.Tick();

            Assert.That(net.NetworkOf(near), Is.EqualTo(net.NetworkOf(plant)));
            Assert.That(net.NetworkOf(chained), Is.EqualTo(net.NetworkOf(plant)), "via de buurman");
            Assert.That(net.NetworkOf(far), Is.Not.EqualTo(net.NetworkOf(plant)));
            Assert.That(net.Satisfaction(far), Is.Zero);
        }

        [Test]
        public void AShortageIsSharedProportionally()
        {
            var net = new UtilityNetwork(0f);
            var plant = net.Add(float2.zero, 60f, 0f);
            var a = net.Add(float2.zero, 0f, 40f);
            var b = net.Add(float2.zero, 0f, 80f);
            net.Link(plant, a);
            net.Link(plant, b);

            net.Tick();

            Assert.That(net.Satisfaction(a), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(net.Supplied(a) + net.Supplied(b), Is.EqualTo(60f).Within(1e-4f), "alles wordt verdeeld, niets meer");
        }

        [Test]
        public void UnlinkRemoveAndSetChangeTheNextTick()
        {
            var net = new UtilityNetwork(0f);
            var plant = net.Add(float2.zero, 100f, 0f);
            var home = net.Add(float2.zero, 0f, 40f);
            net.Link(plant, home);
            net.Tick();
            Assert.That(net.Satisfaction(home), Is.EqualTo(1f));

            net.Set(plant, 10f, 0f);
            net.Tick();
            Assert.That(net.Satisfaction(home), Is.EqualTo(0.25f).Within(1e-5f), "centrale levert minder");

            Assert.That(net.Unlink(plant, home), Is.True);
            Assert.That(net.Unlink(plant, home), Is.False);
            net.Tick();
            Assert.That(net.Satisfaction(home), Is.Zero);

            net.Link(plant, home);
            net.Remove(plant);
            net.Tick();
            Assert.That(net.NetworkCount, Is.EqualTo(1), "alleen het huis is nog over");
            Assert.That(net.Satisfaction(home), Is.Zero);
            Assert.Throws<ArgumentException>(() => net.Supplied(plant));
        }

        [Test]
        public void InvalidInputIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new UtilityNetwork(-1f));
            var net = new UtilityNetwork(0f);
            var a = net.Add(float2.zero, 1f, 1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => net.Add(float2.zero, -1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => net.Set(a, 0f, -1f));
            Assert.Throws<ArgumentException>(() => net.Link(a, a));
            Assert.Throws<ArgumentException>(() => net.Link(a, 99));
            Assert.Throws<ArgumentException>(() => net.Unlink(-1, a));
            Assert.Throws<ArgumentException>(() => net.NetworkOf(7));
            Assert.Throws<ArgumentException>(() => net.Satisfaction(7));
        }
    }
}
