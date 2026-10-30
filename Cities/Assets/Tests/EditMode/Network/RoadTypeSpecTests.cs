using System.Collections.Generic;
using System.Linq;
using Cities.Simulation.Network;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Network
{
    public class RoadTypeSpecTests
    {
        private static RoadTypeSpec TwoLane() => new RoadTypeSpec { CarLanes = 1, BikeLanes = true, Sidewalks = true, SpeedLimitKmh = 50f, CostPerMeter = 400, UpkeepPerMeterPerMonth = 2 };

        private static List<string> Errors(RoadTypeSpec spec)
        {
            var errors = new List<string>();
            spec.Validate(errors);
            return errors;
        }

        [Test]
        public void ARealisticTwoLaneRoadIsValid()
        {
            Assert.That(Errors(TwoLane()), Is.Empty);
        }

        [Test]
        public void TheProfileFollowsTheLayout()
        {
            var highway = new RoadTypeSpec { CarLanes = 3, SpeedLimitKmh = 120f };
            var oneWay = new RoadTypeSpec { CarLanes = 1, OneWay = true, Sidewalks = true, SpeedLimitKmh = 30f };
            var bikePath = new RoadTypeSpec { BikeLanes = true, SpeedLimitKmh = 20f };

            Assert.That(TwoLane().Profile().Count, Is.EqualTo(6), "twee rijstroken, twee fietspaden, twee stoepen");
            Assert.That(highway.Profile().Count, Is.EqualTo(6));
            Assert.That(highway.Profile().All(l => (l.Types & LaneTypes.Vehicles) != 0), Is.True, "geen stoep of fietspad");
            Assert.That(oneWay.Profile().Count(l => l.Direction == LaneDirection.Forward), Is.EqualTo(1));
            Assert.That(bikePath.Profile().All(l => l.Types == LaneTypes.Bike), Is.True);
            Assert.That(new RoadTypeSpec { CarLanes = RoadTypeSpec.MaxCarLanes, BikeLanes = true, Sidewalks = true }.Profile().Count, Is.LessThanOrEqualTo(RoadNetwork.MaxLanes));
        }

        [Test]
        public void SpeedIsConvertedToMetersPerSecond()
        {
            Assert.That(TwoLane().SpeedLimitMetersPerSecond, Is.EqualTo(13.889f).Within(1e-3f));
        }

        [Test]
        public void InvalidLayoutsAreReported()
        {
            var empty = TwoLane();
            empty.CarLanes = 0;
            empty.BikeLanes = false;
            empty.Sidewalks = false;
            var oneWayWithoutLanes = new RoadTypeSpec { CarLanes = 0, OneWay = true, Sidewalks = true, SpeedLimitKmh = 30f };
            var tooFast = TwoLane();
            tooFast.SpeedLimitKmh = 200f;
            var tooWide = TwoLane();
            tooWide.CarLanes = 9;
            var free = TwoLane();
            free.CostPerMeter = -1;
            free.UpkeepPerMeterPerMonth = -1;

            Assert.That(Errors(empty), Has.Some.Contains("minstens"));
            Assert.That(Errors(oneWayWithoutLanes), Has.Some.Contains("eenrichtingsweg"));
            Assert.That(Errors(tooFast), Has.Some.Contains(nameof(RoadTypeSpec.SpeedLimitKmh)));
            Assert.That(Errors(tooWide), Has.Some.Contains(nameof(RoadTypeSpec.CarLanes)));
            Assert.That(Errors(free), Has.Count.EqualTo(2));
        }
    }
}
