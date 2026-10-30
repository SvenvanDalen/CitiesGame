using System;
using System.Linq;
using Cities.Simulation.Network;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Network
{
    public class LaneProfilesTests
    {
        [Test]
        public void TwoWayRoadsDriveOnTheRight()
        {
            var lanes = LaneProfiles.Road(2, bikeLanes: true, sidewalks: true);

            var forward = lanes.Where(l => l.Types == LaneTypes.Vehicles && l.Direction == LaneDirection.Forward).Select(l => l.Offset);
            var backward = lanes.Where(l => l.Types == LaneTypes.Vehicles && l.Direction == LaneDirection.Backward).Select(l => l.Offset);
            Assert.That(forward, Is.EqualTo(new[] { 1.625f, 4.875f }));
            Assert.That(backward, Is.EqualTo(new[] { -1.625f, -4.875f }));
            Assert.That(lanes.Count(l => l.Types == LaneTypes.Bike), Is.EqualTo(2));
            Assert.That(lanes.Where(l => l.Types == LaneTypes.Pedestrian).Select(l => l.Offset), Is.EqualTo(new[] { 9.3f, -9.3f }).Within(1e-4f));
        }

        [Test]
        public void OneWayRoadsAreCentredAndAllForward()
        {
            var lanes = LaneProfiles.Road(2, bikeLanes: true, sidewalks: false, oneWay: true);

            Assert.That(lanes.Where(l => l.Types == LaneTypes.Vehicles).Select(l => l.Offset), Is.EqualTo(new[] { -1.625f, 1.625f }));
            Assert.That(lanes.All(l => l.Direction == LaneDirection.Forward), Is.True);
        }

        [Test]
        public void APathWithoutCarLanesIsAFootpath()
        {
            var lanes = LaneProfiles.Road(0, bikeLanes: false, sidewalks: false);

            Assert.That(lanes.Select(l => l.Types), Is.EqualTo(new[] { LaneTypes.Pedestrian, LaneTypes.Pedestrian }));
            Assert.Throws<ArgumentOutOfRangeException>(() => LaneProfiles.Road(-1, false, false));
        }

        [Test]
        public void LaneSpecsAreValidatedAndComparable()
        {
            var lane = new LaneSpec(LaneTypes.Car, LaneDirection.Forward, 1.5f, 3f);

            Assert.That(lane, Is.EqualTo(new LaneSpec(LaneTypes.Car, LaneDirection.Forward, 1.5f, 3f)));
            Assert.That(lane == new LaneSpec(LaneTypes.Car, LaneDirection.Forward, 1.5f, 3f), Is.True);
            Assert.That(lane != new LaneSpec(LaneTypes.Bus, LaneDirection.Forward, 1.5f, 3f), Is.True);
            Assert.That(lane.Equals((object)lane), Is.True);
            Assert.That(lane.GetHashCode(), Is.EqualTo(new LaneSpec(LaneTypes.Car, LaneDirection.Forward, 1.5f, 3f).GetHashCode()));
            Assert.That(lane.ToString(), Does.Contain("Car"));
            Assert.Throws<ArgumentException>(() => _ = new LaneSpec(LaneTypes.None, LaneDirection.Forward, 0f, 3f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new LaneSpec(LaneTypes.Car, LaneDirection.Forward, 0f, 0f));
        }
    }
}
