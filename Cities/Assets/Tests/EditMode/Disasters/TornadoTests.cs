using System;
using Cities.Simulation.Disasters;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Disasters
{
    public class TornadoTests
    {
        private static float2 Run(ulong seed)
        {
            var tornado = new Tornado(float2.zero, new float2(0f, 1f), 2, seed);
            for (var i = 0; i < 10; i++)
            {
                tornado.Step(5f);
            }

            return tornado.Position;
        }

        [Test]
        public void TheTornadoTravelsRoughlyAlongItsHeading()
        {
            var tornado = new Tornado(float2.zero, new float2(1f, 0f), 3, 7UL);

            for (var i = 0; i < 20; i++)
            {
                tornado.Step(10f);
            }

            Assert.That(tornado.Path.Count, Is.EqualTo(21));
            Assert.That(tornado.Position.x, Is.GreaterThan(2000f), "20 stappen van 150 m, met wat afwijking");
            Assert.That(math.length(tornado.Heading), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void ThePathIsDeterministicPerSeedAndDiffersBetweenSeeds()
        {
            Assert.That(Run(1UL), Is.EqualTo(Run(1UL)));
            Assert.That(Run(1UL), Is.Not.EqualTo(Run(2UL)));
        }

        [Test]
        public void DamageIsHighOnThePathAndFadesToNothingAtTheEdge()
        {
            var tornado = new Tornado(float2.zero, new float2(1f, 0f), 3, 1UL);
            tornado.Step(10f);

            var onPath = tornado.DamageAt(new float2(50f, 0f));
            var nearEdge = tornado.DamageAt(new float2(50f, tornado.Radius * 0.9f));

            Assert.That(onPath, Is.GreaterThan(nearEdge));
            Assert.That(tornado.DamageAt(new float2(50f, tornado.Radius + 1f)), Is.Zero);
            Assert.That(tornado.DamageAt(new float2(-tornado.Radius - 1f, 0f)), Is.Zero, "achter de start");
        }

        [Test]
        public void AStrongerTornadoDoesMoreDamageAndAnEf5CollapsesTheCore()
        {
            var weak = new Tornado(float2.zero, new float2(1f, 0f), 1, 1UL);
            var strong = new Tornado(float2.zero, new float2(1f, 0f), 5, 1UL);

            Assert.That(strong.DamageAt(float2.zero), Is.EqualTo(1f));
            Assert.That(weak.DamageAt(float2.zero), Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(strong.Radius, Is.GreaterThan(weak.Radius));
        }

        [Test]
        public void ATornadoThatHasNotMovedDamagesAroundItsStart()
        {
            var tornado = new Tornado(new float2(10f, 10f), new float2(0f, 1f), 2, 1UL);

            Assert.That(tornado.DistanceToPath(new float2(10f, 40f)), Is.EqualTo(30f).Within(1e-4f));
            Assert.That(tornado.DamageAt(new float2(10f, 10f)), Is.GreaterThan(0f));
        }

        [Test]
        public void AZeroLengthStepStaysPut()
        {
            var tornado = new Tornado(float2.zero, new float2(1f, 0f), 1, 1UL);

            tornado.Step(0f);

            Assert.That(tornado.Position, Is.EqualTo(float2.zero));
            Assert.That(tornado.DistanceToPath(new float2(3f, 4f)), Is.EqualTo(5f).Within(1e-4f));
        }

        [Test]
        public void TheSpecIsValidAndHasNoWarning()
        {
            Assert.That(Tornado.Spec.Name, Is.EqualTo("Tornado"));
            Assert.That(Tornado.Spec.WarningDays, Is.Zero);
        }

        [Test]
        public void BadInputIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Tornado(float2.zero, new float2(1f, 0f), 0, 1UL));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Tornado(float2.zero, new float2(1f, 0f), 6, 1UL));
            Assert.Throws<ArgumentException>(() => new Tornado(float2.zero, float2.zero, 1, 1UL));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Tornado(float2.zero, new float2(1f, 0f), 1, 1UL).Step(-1f));
        }
    }
}
