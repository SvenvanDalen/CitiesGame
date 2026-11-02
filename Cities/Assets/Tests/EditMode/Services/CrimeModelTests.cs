using System;
using Cities.Core.Randomness;
using Cities.Simulation.Services;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Services
{
    public class CrimeModelTests
    {
        private static ServiceDispatcher WithStation(int vehicles = 1)
        {
            var dispatcher = new ServiceDispatcher(10f);
            dispatcher.Add(new ServiceStation(ServiceKind.Police, float2.zero, 100f, vehicles, 1000));
            return dispatcher;
        }

        [Test]
        public void ThePoliceStationHalvesTheTargetAndAFullPrisonRaisesIt()
        {
            var prison = new Prison(1);
            var model = new CrimeModel(WithStation(), prison);
            model.Add(1, new float2(10f, 0f), 0.6f);
            model.Add(2, new float2(5000f, 0f), 0.6f);

            Assert.That(model.Target(1), Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(model.Target(2), Is.EqualTo(0.6f).Within(1e-5f));
            prison.Admit();
            Assert.That(model.Target(2), Is.EqualTo(0.9f).Within(1e-5f));
            Assert.That(model.Target(1), Is.EqualTo(0.45f).Within(1e-5f));
        }

        [Test]
        public void CrimeDriftsTowardsTheTarget()
        {
            var model = new CrimeModel(WithStation(), new Prison(5));
            var random = new DeterministicRandom(1);
            model.Add(1, new float2(10f, 0f), 0.6f);

            model.Step(ref random);

            Assert.That(model.CrimeOf(1), Is.EqualTo(0.6f - (0.3f * CrimeModel.AdjustPerDay)).Within(1e-5f));
        }

        [Test]
        public void ASolvedCrimeEndsInAnArrestAndAFullPrisonLetsThemGo()
        {
            var dispatcher = WithStation();
            var prison = new Prison(1);
            var model = new CrimeModel(dispatcher, prison);
            var random = new DeterministicRandom(7);
            model.Add(1, new float2(10f, 0f), 1f);

            for (var day = 0; day < 40; day++)
            {
                model.Step(ref random);
                dispatcher.Tick(1000f);
            }

            Assert.That(model.Arrests, Is.EqualTo(1));
            Assert.That(prison.IsFull, Is.True);
            Assert.That(model.Escaped, Is.GreaterThan(0));
            prison.Release(5);
            Assert.That(prison.Inmates, Is.Zero);
        }

        [Test]
        public void ACrimeWithoutPatrolsStaysUnsolvedAndRaisesTheCrime()
        {
            var dispatcher = new ServiceDispatcher(10f);
            var model = new CrimeModel(dispatcher, new Prison(5));
            var random = new DeterministicRandom(3);
            model.Add(1, float2.zero, 1f);

            for (var day = 0; day < 20; day++)
            {
                model.Step(ref random);
            }

            Assert.That(model.Unsolved, Is.GreaterThan(0));
            Assert.That(model.Arrests, Is.Zero);
            Assert.That(model.CrimeOf(1), Is.EqualTo(1f));
            Assert.That(dispatcher.Open.Count, Is.LessThanOrEqualTo(CrimeModel.UnsolvedAfterDays));
        }

        [Test]
        public void BadInputIsRejected()
        {
            var model = new CrimeModel(WithStation(), new Prison(1));
            model.Add(1, float2.zero, 0.5f);

            Assert.Throws<ArgumentOutOfRangeException>(() => model.Add(2, float2.zero, 1.5f));
            Assert.Throws<ArgumentException>(() => model.Add(1, float2.zero, 0.5f));
            Assert.Throws<ArgumentException>(() => model.CrimeOf(9));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Prison(0));
            Assert.Throws<ArgumentNullException>(() => new CrimeModel(null, new Prison(1)));
            Assert.Throws<ArgumentNullException>(() => new CrimeModel(WithStation(), null));
        }
    }
}
