using System;
using Cities.Simulation.Fields;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Fields
{
    public class WindModelTests
    {
        [Test]
        public void TheSameSeedAndTickGiveTheSameWind()
        {
            var a = WindModel.At(42, 1234);
            var b = WindModel.At(42, 1234);

            Assert.That(b.Direction, Is.EqualTo(a.Direction));
            Assert.That(b.Speed, Is.EqualTo(a.Speed));
            Assert.That(WindModel.At(43, 1234).Direction, Is.Not.EqualTo(a.Direction));
        }

        [Test]
        public void TheWindChangesOverTimeButSmoothly()
        {
            var speeds = new System.Collections.Generic.HashSet<float>();
            var previous = WindModel.At(7, 0);
            for (long tick = 1; tick < 3000; tick++)
            {
                var wind = WindModel.At(7, tick);
                speeds.Add(wind.Speed);
                Assert.That(math.abs(wind.Speed - previous.Speed), Is.LessThan(0.5f), "geen sprongen in snelheid");
                var turn = math.abs(math.atan2(math.sin(wind.Direction - previous.Direction), math.cos(wind.Direction - previous.Direction)));
                Assert.That(turn, Is.LessThan(0.1f), "geen sprongen in richting");
                Assert.That(wind.Speed, Is.InRange(0f, WindModel.MaxSpeed));
                previous = wind;
            }

            Assert.That(speeds.Count, Is.GreaterThan(100));
        }

        [Test]
        public void TheWindIsContinuousOverAKnot()
        {
            var before = WindModel.At(9, WindModel.KnotTicks - 1);
            var at = WindModel.At(9, WindModel.KnotTicks);

            Assert.That(math.abs(at.Speed - before.Speed), Is.LessThan(0.1f));
        }

        [Test]
        public void TheWindMostlyBlowsAroundThePrevailingDirection()
        {
            var prevailing = WindModel.PrevailingDirection(5);
            var sum = float2.zero;
            for (long tick = 0; tick < WindModel.KnotTicks * 200; tick += WindModel.KnotTicks)
            {
                var w = WindModel.At(5, tick);
                sum += new float2(math.cos(w.Direction), math.sin(w.Direction));
            }

            var mean = math.atan2(sum.y, sum.x);
            var off = math.abs(math.atan2(math.sin(mean - prevailing), math.cos(mean - prevailing)));
            Assert.That(off, Is.LessThan(0.3f));
        }

        [Test]
        public void VectorAndStrengthFollowSpeedAndDirection()
        {
            var calm = new Wind(1f, 0f);
            var east = new Wind(0f, WindModel.MaxSpeed / 2f);
            var north = new Wind(math.PI / 2f, WindModel.MaxSpeed * 3f);

            Assert.That(calm.Strength, Is.Zero);
            Assert.That(math.length(calm.Vector), Is.Zero);
            Assert.That(east.Strength, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(east.Vector.x, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(east.Vector.y, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(north.Strength, Is.EqualTo(1f), "begrensd");
            Assert.That(north.Vector.y, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(north.CellsPerStep(0.2f).y, Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(new Wind(0f, -3f).Speed, Is.Zero);
        }

        [Test]
        public void TheDiffusionStepAcceptsTheWindAtMaxStrength()
        {
            for (long tick = 0; tick < 3000; tick += 7)
            {
                var wind = WindModel.At(1, tick).CellsPerStep(0.2f);
                Assert.DoesNotThrow(() => _ = new DiffusionSettings(0.05f, 0.01f, wind));
            }
        }

        [Test]
        public void ANegativeTickIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => WindModel.At(1, -1));
        }
    }
}
