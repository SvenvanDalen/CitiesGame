using System;
using Cities.Core.Randomness;
using Cities.Simulation.Services;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Services
{
    public class FireModelTests
    {
        private static ServiceDispatcher WithStation(float x = 0f, float radius = 100f)
        {
            var dispatcher = new ServiceDispatcher(10f);
            dispatcher.Add(new ServiceStation(ServiceKind.Fire, new float2(x, 0f), radius, 1, 1000));
            return dispatcher;
        }

        [Test]
        public void RiskScalesWithFlammabilityAndHalvesUnderCoverage()
        {
            var model = new FireModel(WithStation());
            model.Add(1, new float2(10f, 0f), 1f);
            model.Add(2, new float2(5000f, 0f), 1f);
            model.Add(3, new float2(5000f, 0f), 0.5f);
            model.Add(4, new float2(5000f, 0f), 0f);

            Assert.That(model.Risk(2), Is.EqualTo(FireModel.BaseRiskPerDay));
            Assert.That(model.Risk(1), Is.EqualTo(FireModel.BaseRiskPerDay * FireModel.CoveredRiskFactor));
            Assert.That(model.Risk(3), Is.EqualTo(FireModel.BaseRiskPerDay * 0.5f));
            Assert.That(model.Risk(4), Is.Zero);
        }

        [Test]
        public void ArsonSetsTheBuildingAlightAndReportsItOnce()
        {
            var dispatcher = WithStation();
            var model = new FireModel(dispatcher);
            model.Add(1, new float2(50f, 0f), 1f);

            Assert.That(model.Ignite(1), Is.True);
            Assert.That(model.Ignite(1), Is.False, "brandt al");

            Assert.That(model.StateOf(1), Is.EqualTo(FireState.Burning));
            Assert.That(model.Risk(1), Is.Zero);
            Assert.That(dispatcher.Open, Has.Count.EqualTo(1));
            Assert.That(dispatcher.Open[0].Handler, Is.Not.Null, "de kazerne rukt uit");
        }

        [Test]
        public void TheFireBrigadePutsTheFireOut()
        {
            var dispatcher = WithStation();
            var model = new FireModel(dispatcher);
            var random = new DeterministicRandom(1);
            model.Add(1, new float2(50f, 0f), 0f);
            model.Ignite(1);

            dispatcher.Tick(1000f);
            var burned = model.Step(ref random);

            Assert.That(burned, Is.Empty);
            Assert.That(model.StateOf(1), Is.EqualTo(FireState.Safe));
        }

        [Test]
        public void AnUncoveredFireBurnsTheBuildingDownAndCancelsTheReport()
        {
            var dispatcher = WithStation();
            var model = new FireModel(dispatcher);
            var random = new DeterministicRandom(1);
            model.Add(1, new float2(5000f, 0f), 0f);
            model.Ignite(1);

            for (var day = 1; day < FireModel.BurnDays; day++)
            {
                Assert.That(model.Step(ref random), Is.Empty);
            }

            Assert.That(model.Step(ref random), Is.EqualTo(new[] { 1 }));
            Assert.That(model.StateOf(1), Is.EqualTo(FireState.BurnedDown));
            Assert.That(dispatcher.Open, Is.Empty);
            Assert.That(model.Ignite(1), Is.False, "afgebrand gebouw brandt niet opnieuw");
        }

        [Test]
        public void FireSpreadsToFlammableNeighboursInRangeOnly()
        {
            var model = new FireModel(WithStation(5000f, 10f));
            var random = new DeterministicRandom(3);
            model.Add(0, float2.zero, 1f);
            for (var i = 1; i <= 20; i++)
            {
                model.Add(i, new float2(10f, i), 1f);
                model.Add(100 + i, new float2(500f, i), 1f);
                model.Add(200 + i, new float2(10f, -i), 0f);
            }

            model.Ignite(0);
            model.Step(ref random);

            var near = 0;
            for (var i = 1; i <= 20; i++)
            {
                near += model.StateOf(i) == FireState.Burning ? 1 : 0;
                Assert.That(model.StateOf(100 + i), Is.EqualTo(FireState.Safe), "buiten bereik");
                Assert.That(model.StateOf(200 + i), Is.EqualTo(FireState.Safe), "onbrandbaar");
            }

            Assert.That(near, Is.GreaterThan(0));
        }

        [Test]
        public void CancellingAnIncidentFreesTheVehicle()
        {
            var dispatcher = WithStation();
            var incident = dispatcher.Report(ServiceKind.Fire, new float2(10f, 0f));

            Assert.That(dispatcher.Cancel(incident), Is.True);
            Assert.That(dispatcher.Cancel(incident), Is.False);
            Assert.That(dispatcher.Open, Is.Empty);
            Assert.That(dispatcher.Report(ServiceKind.Fire, new float2(10f, 0f)).Handler, Is.Not.Null, "voertuig is weer vrij");
        }

        [Test]
        public void BadInputIsRejected()
        {
            var model = new FireModel(WithStation());
            model.Add(1, float2.zero, 1f);

            Assert.Throws<ArgumentNullException>(() => new FireModel(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Add(2, float2.zero, 1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Add(2, float2.zero, -0.1f));
            Assert.Throws<ArgumentException>(() => model.Add(1, float2.zero, 1f));
            Assert.Throws<ArgumentException>(() => model.Ignite(9));
        }
    }
}
