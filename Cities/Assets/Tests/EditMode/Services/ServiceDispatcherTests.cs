using System;
using Cities.Simulation.Services;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Services
{
    public class ServiceDispatcherTests
    {
        private static ServiceStation Fire(float x, int vehicles = 1, float radius = 100f, long upkeep = 1000) =>
            new ServiceStation(ServiceKind.Fire, new float2(x, 0f), radius, vehicles, upkeep);

        [Test]
        public void CoverageFollowsTheRangeAndTheKind()
        {
            var dispatcher = new ServiceDispatcher(10f);
            dispatcher.Add(Fire(0f));

            Assert.That(dispatcher.IsCovered(ServiceKind.Fire, new float2(100f, 0f)), Is.True, "op de rand");
            Assert.That(dispatcher.IsCovered(ServiceKind.Fire, new float2(101f, 0f)), Is.False);
            Assert.That(dispatcher.IsCovered(ServiceKind.Police, float2.zero), Is.False);
            Assert.That(dispatcher.Coverage(ServiceKind.Fire, new[] { float2.zero, new float2(500f, 0f) }), Is.EqualTo(0.5f));
            Assert.That(dispatcher.Coverage(ServiceKind.Fire, Array.Empty<float2>()), Is.EqualTo(1f));
        }

        [Test]
        public void AnIncidentGoesToTheNearestCoveringStationWithAFreeVehicle()
        {
            var dispatcher = new ServiceDispatcher(10f);
            var far = Fire(0f);
            var near = Fire(60f);
            dispatcher.Add(far);
            dispatcher.Add(near);

            var incident = dispatcher.Report(ServiceKind.Fire, new float2(50f, 0f));

            Assert.That(incident.Handler, Is.SameAs(near));
            Assert.That(near.FreeVehicles, Is.Zero);
            Assert.That(far.FreeVehicles, Is.EqualTo(1));
        }

        [Test]
        public void WithoutCoverageTheIncidentWaitsAndStaysOpen()
        {
            var dispatcher = new ServiceDispatcher(10f);
            dispatcher.Add(Fire(0f));

            var incident = dispatcher.Report(ServiceKind.Fire, new float2(1000f, 0f));
            dispatcher.Tick(10000f);

            Assert.That(incident.Handler, Is.Null);
            Assert.That(dispatcher.Open, Has.Count.EqualTo(1));
        }

        [Test]
        public void ABusyStationQueuesTheNextIncidentUntilItsVehicleIsBack()
        {
            var dispatcher = new ServiceDispatcher(10f);
            var station = Fire(0f);
            dispatcher.Add(station);
            var first = dispatcher.Report(ServiceKind.Fire, new float2(50f, 0f));
            var second = dispatcher.Report(ServiceKind.Fire, new float2(-50f, 0f));
            Assert.That(second.Handler, Is.Null, "één voertuig, dus de tweede wacht");

            var trip = (2f * 50f / 10f) + ServiceDispatcher.HandlingSeconds;
            Assert.That(dispatcher.Tick(trip - 1f), Is.Empty);
            var done = dispatcher.Tick(1f);

            Assert.That(done, Is.EqualTo(new[] { first }));
            Assert.That(second.Handler, Is.SameAs(station), "het teruggekeerde voertuig rukt meteen weer uit");
            Assert.That(dispatcher.Open, Is.EqualTo(new[] { second }));
        }

        [Test]
        public void MoreVehiclesHandleMoreIncidentsAtOnce()
        {
            var dispatcher = new ServiceDispatcher(10f);
            dispatcher.Add(Fire(0f, vehicles: 2));

            var a = dispatcher.Report(ServiceKind.Fire, new float2(10f, 0f));
            var b = dispatcher.Report(ServiceKind.Fire, new float2(20f, 0f));
            var c = dispatcher.Report(ServiceKind.Fire, new float2(30f, 0f));

            Assert.That(a.Handler, Is.Not.Null);
            Assert.That(b.Handler, Is.Not.Null);
            Assert.That(c.Handler, Is.Null);
        }

        [Test]
        public void RemovingAStationSendsItsIncidentsBackToTheQueue()
        {
            var dispatcher = new ServiceDispatcher(10f);
            var first = Fire(0f);
            var second = Fire(10f);
            dispatcher.Add(first);
            dispatcher.Add(second);
            var incident = dispatcher.Report(ServiceKind.Fire, new float2(1f, 0f));
            Assert.That(incident.Handler, Is.SameAs(first));

            Assert.That(dispatcher.Remove(first), Is.True);
            dispatcher.Tick(0f);

            Assert.That(incident.Handler, Is.SameAs(second));
            Assert.That(dispatcher.Remove(first), Is.False);
        }

        [Test]
        public void UpkeepIsTheSumOfAllStations()
        {
            var dispatcher = new ServiceDispatcher(10f);
            dispatcher.Add(Fire(0f, upkeep: 1000));
            dispatcher.Add(new ServiceStation(ServiceKind.Police, float2.zero, 50f, 2, 2500));

            Assert.That(dispatcher.UpkeepPerMonth, Is.EqualTo(3500));
        }

        [Test]
        public void BadInputIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServiceDispatcher(0f));
            Assert.Throws<ArgumentNullException>(() => new ServiceDispatcher(1f).Add(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => Fire(0f, radius: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Fire(0f, vehicles: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Fire(0f, upkeep: -1));
        }
    }
}
