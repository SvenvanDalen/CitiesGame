using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Gameplay.Overlays;
using Cities.Simulation.Network;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Overlays
{
    /// <summary>Het wegennet staat in NativeLists; draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class RoadOverlaysTests
    {
        private RoadNetwork _network;

        [SetUp]
        public void SetUp() => _network = new RoadNetwork(16);

        [TearDown]
        public void TearDown() => _network.Dispose();

        [Test]
        public void EverySegmentGetsRibbonsColouredByItsOccupancy()
        {
            var a = _network.AddNode(float3.zero);
            var b = _network.AddNode(new float3(0f, 0f, 60f));
            var segment = _network.AddSegment(a, b, Bezier3.Straight(float3.zero, new float3(0f, 0f, 60f)), LaneProfiles.Road(1, bikeLanes: false, sidewalks: true), 13.9f);
            var source = RoadOverlays.Traffic(_network, id => id == segment ? 0.25f : 0f);
            var values = new List<OverlayEntityValue>();

            source.Collect(values);

            Assert.That(values, Has.Count.EqualTo(3), "60 m in stukken van hoogstens 25 m");
            Assert.That(values.All(v => v.Id == segment.Packed && Math.Abs(v.Value - 25f) < 1e-3f));
            Assert.That(source.Target, Is.EqualTo(OverlayTarget.Segment));
        }

        [Test]
        public void OccupancyIsClampedAndArgumentsAreChecked()
        {
            var a = _network.AddNode(float3.zero);
            var b = _network.AddNode(new float3(0f, 0f, 10f));
            _network.AddSegment(a, b, Bezier3.Straight(float3.zero, new float3(0f, 0f, 10f)), LaneProfiles.Road(1, bikeLanes: false, sidewalks: true), 13.9f);
            var values = new List<OverlayEntityValue>();

            RoadOverlays.Traffic(_network, _ => 3f).Collect(values);

            Assert.That(values.Single().Value, Is.EqualTo(100f));
            Assert.Throws<ArgumentNullException>(() => RoadOverlays.Traffic(null, _ => 0f));
            Assert.Throws<ArgumentNullException>(() => RoadOverlays.Traffic(_network, null));
        }
    }
}
