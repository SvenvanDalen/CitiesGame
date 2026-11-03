using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Simulation.Transit;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Transit
{
    public class MetroTests
    {
        private TransitNetwork _network;
        private MetroNetwork _metro;

        [SetUp]
        public void SetUp()
        {
            _network = new TransitNetwork();
            _metro = new MetroNetwork(_network);
        }

        [Test]
        public void ADepthMustLieBetweenTheLimits()
        {
            Assert.That(Metro.IsDepthOk(Metro.MinDepth - 0.1f), Is.False);
            Assert.That(Metro.IsDepthOk(Metro.MinDepth), Is.True);
            Assert.That(Metro.IsDepthOk(Metro.MaxDepth), Is.True);
            Assert.That(Metro.IsDepthOk(Metro.MaxDepth + 0.1f), Is.False);
        }

        [Test]
        public void StationsMustBeFarEnoughApart()
        {
            Assert.That(Metro.IsSpacingOk(float2.zero, new float2(599f, 0f)), Is.False);
            Assert.That(Metro.IsSpacingOk(float2.zero, new float2(0f, 600f)), Is.True);
        }

        [Test]
        public void ATunnelMayNotBeSteeperThanTheLimit()
        {
            Assert.That(Metro.IsGradeOk(10f, 10f, 1000f), Is.True);
            Assert.That(Metro.IsGradeOk(10f, 50f, 1000f), Is.True, "4% is nog toegestaan");
            Assert.That(Metro.IsGradeOk(10f, 51f, 1000f), Is.False);
            Assert.That(Metro.IsGradeOk(51f, 10f, 1000f), Is.False, "ook omlaag");
            Assert.Throws<ArgumentOutOfRangeException>(() => Metro.IsGradeOk(10f, 10f, 0f));
        }

        [Test]
        public void ReachingAPlatformTakesLongerTheDeeperItIs()
        {
            Assert.That(Metro.AccessTime(Metro.MinDepth), Is.EqualTo(Metro.WalkTime + Metro.MinDepth / Metro.EscalatorSpeed).Within(1e-4f));
            Assert.That(Metro.AccessTime(30f), Is.GreaterThan(Metro.AccessTime(10f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Metro.AccessTime(1f));
        }

        [Test]
        public void ARideReachesTopSpeedOnlyOnALongStretch()
        {
            Assert.That(Metro.TravelTime(0f), Is.Zero);

            var shortRide = Metro.TravelTime(100f);
            Assert.That(shortRide, Is.EqualTo(2f * math.sqrt(100f / Metro.Acceleration)).Within(1e-4f));

            var longDistance = 5000f;
            Assert.That(Metro.TravelTime(longDistance), Is.EqualTo(longDistance / Metro.TopSpeed + Metro.TopSpeed / Metro.Acceleration).Within(1e-3f));

            var ramp = Metro.TopSpeed * Metro.TopSpeed / Metro.Acceleration;
            Assert.That(Metro.TravelTime(ramp - 1f), Is.EqualTo(Metro.TravelTime(ramp)).Within(0.1f), "geen sprong bij de grens");
            Assert.Throws<ArgumentOutOfRangeException>(() => Metro.TravelTime(-1f));
        }

        [Test]
        public void AStationIsAStopWithADepth()
        {
            var stop = _metro.AddStation(new float2(0f, 0f), 12f);

            Assert.That(_metro.IsStation(stop), Is.True);
            Assert.That(_metro.DepthOf(stop), Is.EqualTo(12f));
            Assert.That(_network.HasStop(stop), Is.True);
            Assert.That(_metro.AccessTime(stop), Is.EqualTo(Metro.AccessTime(12f)));
            Assert.That(_metro.Stations.Select(s => s.Key), Is.EqualTo(new[] { stop }));
            Assert.Throws<ArgumentException>(() => _metro.DepthOf(99));
            Assert.That(_metro.Network, Is.SameAs(_network));
        }

        [Test]
        public void AStationNeedsAValidDepthAndRoom()
        {
            _metro.AddStation(new float2(0f, 0f), 12f);

            Assert.Throws<ArgumentOutOfRangeException>(() => _metro.AddStation(new float2(1000f, 0f), 2f));
            Assert.Throws<ArgumentException>(() => _metro.AddStation(new float2(100f, 0f), 12f));
            Assert.That(_network.Stops.Count(), Is.EqualTo(1), "een mislukt station laat geen halte achter");
        }

        [Test]
        public void ALineNeedsStationsAndATunnelThatIsNotTooSteep()
        {
            var a = _metro.AddStation(new float2(0f, 0f), 10f);
            var b = _metro.AddStation(new float2(1000f, 0f), 30f);
            var surface = _network.AddStop(new float2(3000f, 0f));

            var line = _metro.AddLine("M1", new[] { a, b }, false, 2, 120f);
            Assert.That(_network.GetLine(line).Stops, Is.EqualTo(new[] { a, b }));

            Assert.Throws<ArgumentException>(() => _metro.AddLine("M2", new[] { b, surface }, false, 1, 60f), "geen metrostation");
            Assert.Throws<ArgumentNullException>(() => _metro.CheckLine(null));
            Assert.Throws<ArgumentNullException>(() => new MetroNetwork(null));
            Assert.Throws<ArgumentNullException>(() => new MetroSection(null));
        }

        [Test]
        public void ALoopIsCheckedIncludingTheWayBack()
        {
            var a = _metro.AddStation(new float2(0f, 0f), 10f);
            var b = _metro.AddStation(new float2(1000f, 0f), 20f);
            var c = _metro.AddStation(new float2(1000f, 1000f), 30f);

            Assert.DoesNotThrow(() => _metro.AddLine("ring", new[] { a, b, c }, true, 3, 90f));

            var farA = _metro.AddStation(new float2(5000f, 0f), 10f);
            var deep = _metro.AddStation(new float2(5700f, 0f), 40f);
            Assert.Throws<ArgumentException>(() => _metro.AddLine("steil", new[] { farA, deep }, false, 1, 60f), "30 m over 700 m is 4,3%");
        }

        [Test]
        public void ARideBetweenStationsFollowsTheDistance()
        {
            var a = _metro.AddStation(new float2(0f, 0f), 10f);
            var b = _metro.AddStation(new float2(1000f, 0f), 20f);

            Assert.That(_metro.RideTime(a, b), Is.EqualTo(Metro.TravelTime(1000f)).Within(1e-3f));
            Assert.Throws<ArgumentException>(() => _metro.RideTime(a, 99));
        }

        [Test]
        public void AStationInUseCannotBeRemoved()
        {
            var a = _metro.AddStation(new float2(0f, 0f), 10f);
            var b = _metro.AddStation(new float2(1000f, 0f), 10f);
            _metro.AddLine("M1", new[] { a, b }, false, 1, 60f);

            Assert.That(_metro.TryRemoveStation(a), Is.False);
            Assert.That(_metro.TryRemoveStation(99), Is.False);
            _network.RemoveLine(1);
            Assert.That(_metro.TryRemoveStation(a), Is.True);
            Assert.That(_metro.IsStation(a), Is.False);
            Assert.That(_network.HasStop(a), Is.False);
        }

        [Test]
        public void TheSectionRoundTripsTheDepths()
        {
            var a = _metro.AddStation(new float2(0f, 0f), 10f);
            var b = _metro.AddStation(new float2(1000f, 0f), 25.5f);
            var section = new MetroSection(_metro);
            var bytes = SaveBytes.Write(new SaveMetadata(), section);

            var restored = new MetroNetwork(new TransitNetwork());
            restored.Restore(new[] { new KeyValuePair<int, float>(77, 12f) });
            SaveBytes.Read(bytes, new MetroSection(restored));

            Assert.That(restored.Stations.ToArray(), Is.EqualTo(new[] { new KeyValuePair<int, float>(a, 10f), new KeyValuePair<int, float>(b, 25.5f) }));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new MetroSection(restored)), Is.EqualTo(bytes));
            Assert.That(section.Id, Is.EqualTo(MetroSection.SectionId));
            Assert.That(MetroSection.SectionId, Is.EqualTo("METR"));
            Assert.That(section.Version, Is.EqualTo(1));
        }

        [Test]
        public void TheSectionRejectsImpossibleData()
        {
            var section = new MetroSection(_metro);

            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => w.Write(-1)), 1), "negatief aantal");
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => { w.Write(1); w.Write(1); w.Write(1f); }), 1), "te ondiep");
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => { w.Write(2); w.Write(1); w.Write(10f); w.Write(1); w.Write(10f); }), 1), "dubbel station");
            Assert.That(_metro.Stations, Is.Empty);
        }
    }
}
