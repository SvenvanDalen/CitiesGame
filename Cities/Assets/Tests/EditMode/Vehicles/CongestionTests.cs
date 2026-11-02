using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Simulation.Vehicles;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Vehicles
{
    public class CongestionTests
    {
        private static VehicleAgent Car(float position, float speed = 0f) =>
            new VehicleAgent { Position = position, Speed = speed, Length = 4f, Driver = new Idm(14f, 1.5f, 2f) };

        [Test]
        public void OccupancyIsTheFilledShareOfTheLane()
        {
            var cars = new List<VehicleAgent> { Car(50f), Car(40f) };

            Assert.That(Congestion.Occupancy(cars, 100f), Is.EqualTo(0.12f).Within(1e-5f), "twee keer 4 m plus 2 m opening");
            Assert.That(Congestion.Occupancy(cars, 5f), Is.EqualTo(1f), "begrensd op bumper aan bumper");
            Assert.That(Congestion.Occupancy(new List<VehicleAgent>(), 100f), Is.Zero);
        }

        [Test]
        public void SpeedFallsWithOccupancyButNeverStops()
        {
            Assert.That(Congestion.SegmentSpeed(14f, 0f), Is.EqualTo(14f));
            Assert.That(Congestion.SegmentSpeed(14f, 0.5f), Is.EqualTo(7f).Within(1e-4f));
            Assert.That(Congestion.SegmentSpeed(14f, 1f), Is.EqualTo(14f * Congestion.MinSpeedFactor).Within(1e-4f));
            Assert.That(Congestion.SegmentSpeed(14f, 3f), Is.EqualTo(14f * Congestion.MinSpeedFactor).Within(1e-4f));
            Assert.That(Congestion.SegmentSpeed(14f, -1f), Is.EqualTo(14f));
        }

        [Test]
        public void AJamBuildsAtAClosedEndAndDissolvesWhenItOpens()
        {
            var cars = Enumerable.Range(0, 6).Select(i => Car(80f - (i * 13f), 14f)).ToList();

            for (var t = 0; t < 600; t++)
            {
                LaneTraffic.Step(cars, 100f, true, 0.1f);
            }

            Assert.That(cars.All(c => c.Speed < 0.05f), Is.True, "iedereen staat stil achter het dichte einde");
            Assert.That(cars[0].Position, Is.LessThan(100f));

            for (var t = 0; t < 600; t++)
            {
                LaneTraffic.Step(cars, 100f, false, 0.1f);
            }

            Assert.That(cars.All(c => c.Speed > 5f), Is.True, "de file lost op");
        }

        [Test]
        public void AVehicleIsStuckOnlyAfterTheTimeoutWithoutAReason()
        {
            var detector = new GridlockDetector();
            for (var i = 0; i < 59; i++)
            {
                Assert.That(detector.Update(1, 0f, false, 1f), Is.False);
            }

            Assert.That(detector.Update(1, 0f, false, 1f), Is.True);
            Assert.That(detector.Update(1, 0f, false, 1f), Is.False, "de teller begint opnieuw na herstel");
        }

        [Test]
        public void MovingOrLegitimatelyWaitingResetsTheTimer()
        {
            var detector = new GridlockDetector();
            for (var i = 0; i < 50; i++)
            {
                detector.Update(1, 0f, false, 1f);
            }

            Assert.That(detector.Update(1, 5f, false, 1f), Is.False, "rijdt weer");
            for (var i = 0; i < 59; i++)
            {
                Assert.That(detector.Update(1, 0f, false, 1f), Is.False);
            }

            detector.Update(1, 0f, true, 1f);
            Assert.That(detector.Update(1, 0f, false, 59f), Is.False, "rood licht zette de teller terug");
            detector.Forget(1);
        }

        [Test]
        public void BadInputIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => Congestion.Occupancy(null, 100f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Congestion.Occupancy(new List<VehicleAgent>(), 0f));
        }
    }
}
