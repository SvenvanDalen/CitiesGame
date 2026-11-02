using System;
using Cities.Simulation.Services;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Services
{
    public class DeathCareTests
    {
        private static void Finish(ServiceDispatcher dispatcher) => dispatcher.Tick(ServiceDispatcher.HandlingSeconds + 100f);

        [Test]
        public void ADeceasedGetsAGraveWhenTheHearseHasBeen()
        {
            var dispatcher = new ServiceDispatcher(10f);
            var care = new DeathCare(dispatcher);
            care.AddCemetery(float2.zero, 2);

            care.Register(1, new float2(50f, 0f));
            care.Register(1, new float2(50f, 0f));
            Assert.That(care.Unburied, Is.EqualTo(1), "dubbele melding telt niet");
            Assert.That(care.Unhappiness, Is.EqualTo(DeathCare.UnhappinessPerBody));
            care.Step();
            Assert.That(care.Unburied, Is.EqualTo(1), "de lijkwagen is er nog niet");

            Finish(dispatcher);
            care.Step();

            Assert.That(care.Buried, Is.EqualTo(1));
            Assert.That(care.FreePlots, Is.EqualTo(1));
            Assert.That(care.Unburied, Is.Zero);
            Assert.That(care.Unhappiness, Is.Zero);
        }

        [Test]
        public void AFullCemeteryWithoutCrematoriumLeavesBodiesAndUpsetsTheCity()
        {
            var dispatcher = new ServiceDispatcher(10f);
            var care = new DeathCare(dispatcher);
            care.AddCemetery(float2.zero, 1);
            care.Register(1, float2.zero);
            care.Register(2, float2.zero);
            Finish(dispatcher);
            care.Step();

            Assert.That(care.Buried, Is.EqualTo(1));
            Assert.That(care.Unburied, Is.EqualTo(1));
            Assert.That(care.Unhappiness, Is.GreaterThan(0f));
        }

        [Test]
        public void ACrematoriumTakesWhatTheCemeteryCannot()
        {
            var dispatcher = new ServiceDispatcher(10f);
            var care = new DeathCare(dispatcher);
            care.AddCemetery(float2.zero, 1);
            care.AddCrematorium(float2.zero);
            care.Register(1, float2.zero);
            care.Register(2, float2.zero);
            Finish(dispatcher);
            care.Step();

            Assert.That(care.Buried, Is.EqualTo(1));
            Assert.That(care.Cremated, Is.EqualTo(1));
            Assert.That(care.Unburied, Is.Zero);
        }

        [Test]
        public void ABodyOutOfReachOfAnyHearseKeepsWaiting()
        {
            var dispatcher = new ServiceDispatcher(10f);
            var care = new DeathCare(dispatcher);
            care.AddCemetery(float2.zero, 1);
            care.Register(1, new float2(9000f, 0f));
            Finish(dispatcher);
            care.Step();

            Assert.That(care.Unburied, Is.EqualTo(1));
            Assert.That(care.Buried, Is.Zero);
        }

        [Test]
        public void UnhappinessIsCapped()
        {
            var care = new DeathCare(new ServiceDispatcher(10f));
            for (var i = 0; i < 100; i++)
            {
                care.Register(i, float2.zero);
            }

            Assert.That(care.Unhappiness, Is.EqualTo(1f));
        }

        [Test]
        public void BadInputIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new DeathCare(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DeathCare(new ServiceDispatcher(10f)).AddCemetery(float2.zero, 0));
        }
    }
}
