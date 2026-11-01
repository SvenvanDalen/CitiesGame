using System;
using System.Linq;
using Cities.Simulation.Network;
using Cities.World.Roads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Roads
{
    public class ParkingSlotsTests
    {
        private static readonly Bezier3 Straight100 = Bezier3.Straight(new float3(0f, 0f, 0f), new float3(0f, 0f, 100f));

        [Test]
        public void TheProfileGetsAParkingStripOnBothSidesOutsideTheCarLanes()
        {
            var lanes = LaneProfiles.Road(1, bikeLanes: false, sidewalks: true, parking: true);

            var parking = lanes.Where(l => l.Types == LaneTypes.Parking).ToList();
            Assert.That(parking.Count, Is.EqualTo(2));
            Assert.That(parking.All(l => Math.Abs(l.Width - LaneProfiles.ParkingLaneWidth) < 1e-5f), Is.True);
            Assert.That(parking.Select(l => l.Offset).OrderBy(o => o), Is.EqualTo(new[] { -4.35f, 4.35f }).Within(1e-4f));
        }

        [Test]
        public void ParkingIsOffByDefaultAndNeedsACarLane()
        {
            Assert.That(LaneProfiles.Road(1, bikeLanes: false, sidewalks: true).Any(l => l.Types == LaneTypes.Parking), Is.False);
            Assert.That(LaneProfiles.Road(0, bikeLanes: false, sidewalks: true, parking: true).Any(l => l.Types == LaneTypes.Parking), Is.False);
        }

        [Test]
        public void TheRoadTypeTogglesParkingAndRejectsItWithoutCarLanes()
        {
            var spec = new RoadTypeSpec { CarLanes = 1, Sidewalks = true, Parking = true, SpeedLimitKmh = 30f };
            Assert.That(spec.Profile().Count(l => l.Types == LaneTypes.Parking), Is.EqualTo(2));

            var errors = new System.Collections.Generic.List<string>();
            new RoadTypeSpec { CarLanes = 0, OneWay = true, Sidewalks = true, Parking = true, SpeedLimitKmh = 30f }.Validate(errors);
            Assert.That(errors, Has.Some.Contains("Parkeren"));
        }

        [Test]
        public void ParkingIsNotPartOfTheLaneGraphGroups()
        {
            Assert.That(LaneProfiles.Road(1, false, true, parking: true).Count(l => (l.Types & LaneTypes.Vehicles) != 0), Is.EqualTo(2));
        }

        [Test]
        public void SlotsFillTheUsableStretchOnBothSidesAndKeepTheEndsFree()
        {
            var lanes = LaneProfiles.Road(1, bikeLanes: false, sidewalks: true, parking: true);

            var slots = ParkingSlots.Layout(Straight100, lanes);

            // 100 m - 2 x 10 m marge = 80 m: 14 plekken van 5,5 m per kant.
            Assert.That(slots.Count, Is.EqualTo(28));
            var right = slots.Where(s => s.Position.x > 0f).OrderBy(s => s.Position.z).ToList();
            Assert.That(right.Count, Is.EqualTo(14));
            Assert.That(right[0].Position.z, Is.GreaterThanOrEqualTo(ParkingSlots.EndMargin));
            Assert.That(right[^1].Position.z, Is.LessThanOrEqualTo(100f - ParkingSlots.EndMargin));
            Assert.That(right[1].Position.z - right[0].Position.z, Is.EqualTo(ParkingSlots.SlotLength).Within(0.05f));
            Assert.That(right[0].Position.x, Is.EqualTo(4.35f).Within(1e-3f));
            Assert.That(slots.All(s => Math.Abs(s.Width - LaneProfiles.ParkingLaneWidth) < 1e-5f && Math.Abs(s.Length - ParkingSlots.SlotLength) < 1e-5f), Is.True);
        }

        [Test]
        public void ShortSegmentsAndRoadsWithoutParkingGiveNoSlots()
        {
            var lanes = LaneProfiles.Road(1, bikeLanes: false, sidewalks: true, parking: true);
            var shortRoad = Bezier3.Straight(float3.zero, new float3(0f, 0f, 25f));

            Assert.That(ParkingSlots.Layout(shortRoad, lanes), Is.Empty);
            Assert.That(ParkingSlots.Layout(Straight100, LaneProfiles.Road(1, false, true)), Is.Empty);
        }

        [Test]
        public void SlotsFollowTheCurveAndHeadAlongTheRoad()
        {
            var curve = new Bezier3(new float3(0f, 0f, 0f), new float3(0f, 0f, 40f), new float3(40f, 0f, 80f), new float3(80f, 0f, 80f));
            var slots = ParkingSlots.Layout(curve, LaneProfiles.Road(1, false, true, parking: true));

            Assert.That(slots, Is.Not.Empty);
            foreach (var slot in slots)
            {
                Assert.That(math.length(slot.Heading), Is.EqualTo(1f).Within(1e-3f));
            }

            // De plekken liggen op 4,35 m van de hartlijn, ook in de bocht.
            var distances = slots.Select(s => Enumerable.Range(0, 200).Min(i => math.distance(curve.Position(i / 199f), s.Position)));
            Assert.That(distances.All(d => d > 4f && d < 4.5f), Is.True);
        }

        [Test]
        public void NullLanesAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => ParkingSlots.Layout(Straight100, null));
        }

        [Test]
        public void AParkingStripIsAsphaltWithoutExtraMarkings()
        {
            var plain = RoadMeshBuilder.Build(Straight100, LaneProfiles.Road(1, false, true));
            var parked = RoadMeshBuilder.Build(Straight100, LaneProfiles.Road(1, false, true, parking: true));

            Assert.That(parked.Triangles[(int)RoadSurface.Asphalt].Count, Is.GreaterThan(plain.Triangles[(int)RoadSurface.Asphalt].Count));
            Assert.That(parked.Triangles[(int)RoadSurface.MarkingDashed].Count, Is.EqualTo(plain.Triangles[(int)RoadSurface.MarkingDashed].Count));
        }
    }
}
