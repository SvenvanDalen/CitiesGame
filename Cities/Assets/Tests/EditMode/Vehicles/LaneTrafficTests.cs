using System;
using Cities.Simulation.Vehicles;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Vehicles
{
    public class LaneTrafficTests
    {
        // Personenauto: 50 km/u, 2,5 m/s² optrekken, 3 m/s² comfortabel remmen.
        private static readonly Idm Car = new Idm(50f / 3.6f, 2.5f, 3f);

        private static VehicleAgent Agent(float position, float speed) =>
            new VehicleAgent { Position = position, Speed = speed, Length = 4.5f, Driver = Car };

        private static void Run(VehicleAgent[] vehicles, float seconds, bool endBlocked = false, float length = 1000f)
        {
            for (var t = 0f; t < seconds; t += 0.1f)
            {
                LaneTraffic.Step(vehicles, length, endBlocked, 0.1f);
            }
        }

        [Test]
        public void ACarOnAFreeRoadAcceleratesToTheDesiredSpeedAndStaysThere()
        {
            var cars = new[] { Agent(10f, 0f) };

            Run(cars, 60f);

            Assert.That(cars[0].Speed, Is.EqualTo(Car.DesiredSpeed).Within(0.1f));
            Assert.That(Car.Acceleration(0f), Is.EqualTo(2.5f), "vanuit stilstand volle versnelling");
        }

        [Test]
        public void AFollowerKeepsAGapAndMatchesTheLeaderSpeed()
        {
            var cars = new[] { Agent(60f, 8f), Agent(10f, 8f) };
            cars[0].Driver = new Idm(8f, 2.5f, 3f);

            Run(cars, 90f);

            var gap = cars[0].Position - cars[0].Length - cars[1].Position;
            Assert.That(cars[1].Speed, Is.EqualTo(cars[0].Speed).Within(0.2f));
            Assert.That(gap, Is.GreaterThan(Car.MinGap));
            Assert.That(gap, Is.EqualTo(Car.DesiredGap(8f, 0f)).Within(1.5f), "evenwichtsafstand: minimale opening plus tijdsafstand");
        }

        [Test]
        public void ACarBrakesToAStopBeforeAClosedEndWithoutOvershooting()
        {
            var cars = new[] { Agent(0f, 50f / 3.6f) };

            Run(cars, 60f, endBlocked: true, length: 300f);

            Assert.That(cars[0].Speed, Is.LessThan(0.05f));
            Assert.That(cars[0].Position, Is.LessThan(300f));
            Assert.That(cars[0].Position, Is.GreaterThan(250f), "stopt vlak voor het einde, niet ver ervoor");
        }

        [Test]
        public void BrakingIsLimitedAndTheCarNeverReverses()
        {
            var stopped = new[] { Agent(100f, 0f), Agent(99f, 13f) };

            LaneTraffic.Step(stopped, 1000f, false, 0.1f);

            Assert.That(Car.Acceleration(13f, 0.1f, 0f), Is.EqualTo(-Idm.MaxBraking));
            Assert.That(stopped[1].Speed, Is.GreaterThanOrEqualTo(0f));
            Assert.That(stopped[0].Speed, Is.GreaterThan(0f), "de voorste rijdt vrij weg");
        }

        [Test]
        public void ATrafficJamDissolvesInOrderAfterTheEndOpens()
        {
            var cars = new[] { Agent(300f, 0f), Agent(290f, 0f), Agent(280f, 0f), Agent(270f, 0f) };

            Run(cars, 30f);

            for (var i = 1; i < cars.Length; i++)
            {
                Assert.That(cars[i].Position, Is.LessThan(cars[i - 1].Position - cars[i - 1].Length), "geen botsing");
            }

            Assert.That(cars[3].Speed, Is.GreaterThan(5f));
        }

        [Test]
        public void TheSpecAndSpeedLimitSetTheDesiredSpeed()
        {
            var spec = new VehicleSpec { MaxSpeedKmh = 120f, AccelerationMs2 = 2f, BrakingMs2 = 3f };

            Assert.That(Idm.For(spec, 14f).DesiredSpeed, Is.EqualTo(14f));
            Assert.That(Idm.For(spec, 100f).DesiredSpeed, Is.EqualTo(120f / 3.6f).Within(1e-4f));
        }

        [Test]
        public void BadInputIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Idm(0f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Idm(10f, 1f, 1f, -1f));
            Assert.Throws<ArgumentNullException>(() => LaneTraffic.Step(null, 10f, false, 0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => LaneTraffic.Step(new VehicleAgent[0], 10f, false, 0f));
        }
    }
}
