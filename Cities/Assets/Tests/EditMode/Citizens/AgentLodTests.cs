using System.Collections.Generic;
using Cities.Core.Ids;
using Cities.Simulation.Citizens;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Citizens
{
    public sealed class AgentLodTests
    {
        private sealed class FixedMap : IDestinationMap
        {
            private readonly float _workX;

            public FixedMap(float workX)
            {
                _workX = workX;
            }

            public bool TryGetPosition(in Citizen citizen, Destination destination, out float2 position)
            {
                position = destination == Destination.Home ? new float2(0, 0) : new float2(_workX, 0);
                return destination != Destination.Anywhere;
            }
        }

        private static Citizen Worker() => new Citizen { Age = 30, Work = new EntityId<BuildingKind>(1, 1), Home = new EntityId<BuildingKind>(2, 1) };

        private static Placement Walk(float x) => new Placement(new float2(x, 0), true);

        // Een id waarvan het uur 8 het eerste werkuur is, zodat de reis van huis naar werk precies dan begint.
        private static uint IdStartingWorkAtEight(in Citizen worker)
        {
            for (var id = 0u; id < 1000; id++)
            {
                if (DailySchedule.ActivityAt(worker, id, 0, 7) != DailySchedule.ActivityAt(worker, id, 0, 8)
                    && DailySchedule.ActivityAt(worker, id, 0, 8) == Activity.Work)
                {
                    return id;
                }
            }

            Assert.Fail("Geen werkdag-id gevonden.");
            return 0;
        }

        [Test]
        public void Trajectory_IsContinuousAcrossTheHour()
        {
            var map = new FixedMap(1000);
            var worker = Worker();
            var id = IdStartingWorkAtEight(worker);
            var before = CitizenTrajectory.PlaceAt(map, worker, id, 0, 7.9999f);
            var after = CitizenTrajectory.PlaceAt(map, worker, id, 0, 8.0001f);
            Assert.That(math.distance(before.Position, after.Position), Is.LessThan(1f));
        }

        [Test]
        public void Trajectory_WalksDuringTripAndStandsStillAfter()
        {
            var map = new FixedMap(1000);
            var worker = Worker();
            var id = IdStartingWorkAtEight(worker);
            var mid = CitizenTrajectory.PlaceAt(map, worker, id, 0, 8.25f);
            Assert.That(mid.Walking, Is.True);
            Assert.That(mid.Position.x, Is.EqualTo(500f).Within(1f));
            var arrived = CitizenTrajectory.PlaceAt(map, worker, id, 0, 9.5f);
            Assert.That(arrived.Walking, Is.False);
            Assert.That(arrived.Position.x, Is.EqualTo(1000f));
        }

        [Test]
        public void Trajectory_WrapsMidnightAndSkipsUnknownDestinations()
        {
            var map = new FixedMap(1000);
            Assert.That(CitizenTrajectory.PlaceAt(map, Worker(), 0, 1, 0.2f).Valid, Is.True);
            var homeless = new Citizen { Age = 30 };
            Assert.That(CitizenTrajectory.PlaceAt(map, homeless, 0, 0, 3f).Valid, Is.False);
        }

        [Test]
        public void Trajectory_LongTripIsNotAPedestrian()
        {
            var worker = Worker();
            var id = IdStartingWorkAtEight(worker);
            Assert.That(CitizenTrajectory.PlaceAt(new FixedMap(9000), worker, id, 0, 8.1f).Walking, Is.False);
        }

        [Test]
        public void Refresh_PicksNearWalkersOnly()
        {
            var lod = new AgentLod(100, 150, 10);
            var placements = new List<Placement> { Walk(50), Walk(500), default, new Placement(new float2(10, 0), false) };
            lod.Refresh(float2.zero, placements);
            Assert.That(lod.Detailed, Is.EqualTo(new[] { 0 }));
            Assert.That(lod.StatisticalWalkers, Is.EqualTo(1));
        }

        [Test]
        public void Refresh_HysteresisKeepsAgentUntilFarRadius()
        {
            var lod = new AgentLod(100, 150, 10);
            lod.Refresh(float2.zero, new List<Placement> { Walk(90) });
            lod.Refresh(float2.zero, new List<Placement> { Walk(120) });
            Assert.That(lod.Detailed, Is.EqualTo(new[] { 0 }));
            lod.Refresh(float2.zero, new List<Placement> { Walk(160) });
            Assert.That(lod.Detailed, Is.Empty);
            lod.Refresh(float2.zero, new List<Placement> { Walk(120) });
            Assert.That(lod.Detailed, Is.Empty);
        }

        [Test]
        public void Refresh_CapDropsFurthestAndKeepsOrder()
        {
            var lod = new AgentLod(100, 100, 2);
            lod.Refresh(float2.zero, new List<Placement> { Walk(80), Walk(10), Walk(40), Walk(40) });
            Assert.That(lod.Detailed, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(lod.StatisticalWalkers, Is.EqualTo(2));
        }

        [Test]
        public void Constructor_NormalisesRadiiAndCap()
        {
            var lod = new AgentLod(10, 5, -1);
            Assert.That(lod.MaxAgents, Is.EqualTo(0));
            Assert.That(lod.NearRadius, Is.EqualTo(10f));
            Assert.That(lod.FarRadius, Is.EqualTo(10f));
        }
    }
}
