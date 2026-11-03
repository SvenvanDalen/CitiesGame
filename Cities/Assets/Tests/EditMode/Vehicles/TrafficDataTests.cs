using System;
using System.Collections.Generic;
using Cities.Gameplay.Overlays;
using Cities.Simulation.Vehicles;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Vehicles
{
    public class TrafficDataTests
    {
        private const ulong A = 1;
        private const ulong B = 2;

        [Test]
        public void ASegmentWithoutMeasurementsIsEmpty()
        {
            var stats = new TrafficData().Get(A);

            Assert.That(stats.VehiclesLastWindow, Is.Zero);
            Assert.That(stats.VehiclesPerHour, Is.Zero);
            Assert.That(float.IsNaN(stats.MeanTravelTime));
            Assert.That(float.IsNaN(stats.MeanSpeed));
            Assert.That(stats.Bottleneck, Is.False);
            Assert.That(float.IsNaN(new TrafficData().NetworkMeanTravelTime));
        }

        [Test]
        public void CountsBecomeFlowWhenTheWindowCloses()
        {
            var data = new TrafficData(300f);
            for (var i = 0; i < 25; i++)
            {
                data.RecordPass(A, 10f, 100f);
            }

            Assert.That(data.Get(A).VehiclesPerHour, Is.Zero, "het venster loopt nog");

            data.Advance(300f);

            Assert.That(data.Get(A).VehiclesLastWindow, Is.EqualTo(25));
            Assert.That(data.Get(A).VehiclesPerHour, Is.EqualTo(300f).Within(1e-3f), "25 per 5 minuten is 300 per uur");

            data.Advance(300f);

            Assert.That(data.Get(A).VehiclesPerHour, Is.Zero, "een leeg venster zet de intensiteit terug");
        }

        [Test]
        public void TravelTimeAndSpeedAreAveragedOverAllPasses()
        {
            var data = new TrafficData();
            data.RecordPass(A, 10f, 100f);
            data.RecordPass(A, 30f, 100f);
            data.RecordPass(B, 5f, 100f);

            var stats = data.Get(A);

            Assert.That(stats.MeanTravelTime, Is.EqualTo(20f).Within(1e-4f));
            Assert.That(stats.MeanSpeed, Is.EqualTo(5f).Within(1e-4f), "200 m in 40 s");
            Assert.That(data.TotalPasses, Is.EqualTo(3));
            Assert.That(data.NetworkMeanTravelTime, Is.EqualTo(15f).Within(1e-4f));
        }

        [Test]
        public void ABusySegmentBecomesABottleneckOnlyAfterTheDelay()
        {
            var data = new TrafficData();

            data.Sample(A, 0.9f, TrafficData.BottleneckDelay - 1f);
            Assert.That(data.Get(A).Bottleneck, Is.False);

            data.Sample(A, 0.9f, 1f);
            Assert.That(data.Get(A).Bottleneck, Is.True);

            var found = new List<ulong>();
            data.CollectBottlenecks(found);
            Assert.That(found, Is.EqualTo(new[] { A }));
        }

        [Test]
        public void ABottleneckClearsOnlyBelowTheLowerThreshold()
        {
            var data = new TrafficData();
            data.Sample(A, 1f, TrafficData.BottleneckDelay);

            data.Sample(A, 0.6f, 1f);
            Assert.That(data.Get(A).Bottleneck, Is.True, "hysterese: tussen de grenzen blijft het een knelpunt");

            data.Sample(A, 0.2f, 1f);
            Assert.That(data.Get(A).Bottleneck, Is.False);
        }

        [Test]
        public void ABriefPeakDoesNotCountTowardsTheDelayWhenTrafficRelaxesInBetween()
        {
            var data = new TrafficData();
            data.Sample(A, 0.9f, 20f);
            data.Sample(A, 0.1f, 1f);
            data.Sample(A, 0.9f, 20f);

            Assert.That(data.Get(A).Bottleneck, Is.False);
        }

        [Test]
        public void VersionChangesWithMeasurementsAndForgettingASegment()
        {
            var data = new TrafficData();
            var start = data.Version;

            data.RecordPass(A, 1f, 10f);
            var afterPass = data.Version;
            data.Forget(A);

            Assert.That(afterPass, Is.GreaterThan(start));
            Assert.That(data.Version, Is.GreaterThan(afterPass));
            Assert.That(data.Segments, Is.Empty);
        }

        [Test]
        public void InvalidInputIsRejected()
        {
            var data = new TrafficData();

            Assert.Throws<ArgumentOutOfRangeException>(() => new TrafficData(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => data.RecordPass(A, 0f, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => data.RecordPass(A, 1f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => data.Sample(A, 0.5f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => data.Advance(-1f));
            Assert.Throws<ArgumentNullException>(() => data.CollectBottlenecks(null));
        }

        [Test]
        public void TheFlowOverlayColoursSegmentsByIntensityAndSkipsUnknownOnes()
        {
            var data = new TrafficData(300f);
            for (var i = 0; i < 75; i++)
            {
                data.RecordPass(A, 10f, 100f);
            }

            data.RecordPass(B, 10f, 100f);
            data.Advance(300f);
            SegmentLine? Locate(ulong id) => id == A ? new SegmentLine(new float3(0), new float3(10, 0, 0), 6f) : (SegmentLine?)null;
            var overlay = new TrafficFlowOverlay(data, Locate, TrafficFlowMode.Flow);

            var values = new List<OverlayEntityValue>();
            overlay.Collect(values);

            Assert.That(overlay.Target, Is.EqualTo(OverlayTarget.Segment));
            Assert.That(overlay.Id, Is.EqualTo("traffic-flow"));
            Assert.That(overlay.Version, Is.EqualTo(data.Version));
            Assert.That(values, Has.Count.EqualTo(1));
            Assert.That(values[0].Id, Is.EqualTo(A));
            Assert.That(values[0].Value, Is.EqualTo(900f).Within(1e-3f), "75 per 5 minuten");
        }

        [Test]
        public void TheCongestionOverlayShowsOccupancyAndMarksBottlenecksFull()
        {
            var data = new TrafficData();
            data.Sample(A, 0.4f, 1f);
            data.Sample(B, 0.9f, TrafficData.BottleneckDelay);
            SegmentLine? Locate(ulong id) => new SegmentLine(new float3(0), new float3(10, 0, 0), 6f);
            var overlay = new TrafficFlowOverlay(data, Locate, TrafficFlowMode.Congestion);

            var values = new List<OverlayEntityValue>();
            overlay.Collect(values);

            Assert.That(overlay.Id, Is.EqualTo("traffic-congestion"));
            Assert.That(overlay.Name, Is.EqualTo("Drukte"));
            Assert.That(values, Has.Count.EqualTo(2));
            foreach (var v in values)
            {
                Assert.That(v.Value, Is.EqualTo(v.Id == A ? 40f : 100f).Within(1e-3f));
            }
        }

        [Test]
        public void TheOverlayRejectsMissingArguments()
        {
            var data = new TrafficData();
            SegmentLine? Locate(ulong id) => null;

            Assert.Throws<ArgumentNullException>(() => new TrafficFlowOverlay(null, Locate, TrafficFlowMode.Flow));
            Assert.Throws<ArgumentNullException>(() => new TrafficFlowOverlay(data, null, TrafficFlowMode.Flow));
            Assert.Throws<ArgumentNullException>(() => new TrafficFlowOverlay(data, Locate, TrafficFlowMode.Flow).Collect(null));
        }
    }
}
