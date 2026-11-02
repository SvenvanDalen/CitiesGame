using System;
using System.Linq;
using Cities.Simulation.Transit;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Transit
{
    public class TransitRouterTests
    {
        // Haltes a-b-c op een rij van 1000 m; lijn 1 rijdt a-b, lijn 2 rijdt b-c; d ligt los.
        private static TransitNetwork Network(out int a, out int b, out int c, out int d)
        {
            var network = new TransitNetwork();
            a = network.AddStop(new float2(0f, 0f));
            b = network.AddStop(new float2(1000f, 0f));
            c = network.AddStop(new float2(2000f, 0f));
            d = network.AddStop(new float2(0f, 5000f));
            network.AddLine("Lijn 1", new[] { a, b }, false, 10, 60f);
            network.AddLine("Lijn 2", new[] { b, c }, false, 10, 60f);
            return network;
        }

        [Test]
        public void ADirectRideCostsWaitingPlusRiding()
        {
            var network = Network(out var a, out var b, out _, out _);

            var journey = TransitRouter.Plan(network, a, b, 10f);

            Assert.That(journey.Legs.Count, Is.EqualTo(1));
            Assert.That(journey.Transfers, Is.Zero);
            Assert.That(journey.Time, Is.EqualTo(30f + 100f).Within(1e-3f), "half de frequentie wachten, 1000 m bij 10 m/s");
        }

        [Test]
        public void ATransferAddsWaitingAndThePenalty()
        {
            var network = Network(out var a, out _, out var c, out _);

            var journey = TransitRouter.Plan(network, a, c, 10f, 120f);

            Assert.That(journey.Legs.Select(l => l.Line).ToArray(), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(journey.Transfers, Is.EqualTo(1));
            Assert.That(journey.Time, Is.EqualTo((30f + 100f) + (120f + 30f + 100f)).Within(1e-3f));
        }

        [Test]
        public void ALineRunsBothWaysUnlessItIsALoop()
        {
            var network = Network(out var a, out var b, out _, out _);
            Assert.That(TransitRouter.Plan(network, b, a, 10f).Legs.Count, Is.EqualTo(1));

            var loopNet = new TransitNetwork();
            var x = loopNet.AddStop(new float2(0f, 0f));
            var y = loopNet.AddStop(new float2(100f, 0f));
            var z = loopNet.AddStop(new float2(100f, 100f));
            loopNet.AddLine("Ring", new[] { x, y, z }, true, 3, 30f);
            var back = TransitRouter.Plan(loopNet, y, x, 10f);

            Assert.That(back.Legs.Count, Is.EqualTo(1), "een ronde rijdt alleen vooruit: y, z, x");
            Assert.That(back.Legs[0].To, Is.EqualTo(x));
            Assert.That(back.Time, Is.GreaterThan(((100f + 141f) / 10f) + 15f - 1f), "de lange weg om, niet terug");
        }

        [Test]
        public void ASlowDirectLineLosesToAFastTransfer()
        {
            var network = new TransitNetwork();
            var a = network.AddStop(new float2(0f, 0f));
            var b = network.AddStop(new float2(1000f, 0f));
            var c = network.AddStop(new float2(2000f, 0f));
            network.AddLine("Snel 1", new[] { a, b }, false, 20, 10f);
            network.AddLine("Snel 2", new[] { b, c }, false, 20, 10f);
            network.AddLine("Zeldzaam direct", new[] { a, c }, false, 1, 3600f);

            var journey = TransitRouter.Plan(network, a, c, 10f);

            Assert.That(journey.Transfers, Is.EqualTo(1), "de directe lijn heeft een wachttijd van een half uur");
        }

        [Test]
        public void NoRouteSameStopAndBadInput()
        {
            var network = Network(out var a, out _, out _, out var d);

            Assert.That(TransitRouter.Plan(network, a, d, 10f), Is.Null);
            var same = TransitRouter.Plan(network, a, a, 10f);
            Assert.That(same.Legs, Is.Empty);
            Assert.That(same.Time, Is.Zero);
            Assert.That(same.Transfers, Is.Zero);
            Assert.Throws<ArgumentNullException>(() => TransitRouter.Plan(null, a, d, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => TransitRouter.Plan(network, a, d, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => TransitRouter.Plan(network, a, d, 10f, -1f));
            Assert.Throws<ArgumentException>(() => TransitRouter.Plan(network, a, 99, 10f));
            Assert.Throws<ArgumentNullException>(() => new TransitJourney(null));
        }
    }
}
