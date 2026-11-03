using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Simulation.Network;
using Cities.World.Roads;
using Cities.World.Terrain;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Roads
{
    public sealed class BridgeSupportsTests
    {
        private static readonly List<LaneSpec> TwoLane = LaneProfiles.Road(1, bikeLanes: false, sidewalks: true);

        /// <summary>Terrein langs de x-as: tussen 100 en 200 m ligt het op <c>middle</c>, daarbuiten op 0.</summary>
        private sealed class Terrain : ITerrainHeight
        {
            private readonly float _middle;

            public Terrain(float middle) => _middle = middle;

            public float SurfaceHeight(float2 position) => position.x > 100f && position.x < 200f ? _middle : 0f;
        }

        private static Bezier3 Deck() => Bezier3.Straight(new float3(0f, 0f, 0f), new float3(300f, 0f, 0f));

        [Test]
        public void PillarsStandOnlyWhereTheDeckIsHighEnough()
        {
            var pillars = BridgeSupports.Plan(Deck(), new Terrain(-8f), 30f);

            Assert.That(pillars.Count, Is.EqualTo(3));
            Assert.That(pillars.All(p => p.Base.x > 100f && p.Base.x < 200f), Is.True);
            Assert.That(pillars.All(p => math.abs(p.Height - (8f - RoadMeshBuilder.DeckThickness)) < 1e-3f), Is.True);
            Assert.That(pillars.All(p => p.Base.y == -8f && p.Radius == BridgeSupports.PillarRadius), Is.True);
        }

        [Test]
        public void NoPillarsOnLowGround()
        {
            Assert.That(BridgeSupports.Plan(Deck(), new Terrain(-2f), 30f), Is.Empty);
            Assert.That(BridgeSupports.Plan(Deck(), new Terrain(0f)), Is.Empty);
        }

        [Test]
        public void SmallerSpacingGivesMorePillars()
        {
            var wide = BridgeSupports.Plan(Deck(), new Terrain(-8f), 60f).Count;
            var tight = BridgeSupports.Plan(Deck(), new Terrain(-8f), 15f).Count;
            Assert.That(tight, Is.GreaterThan(wide));
        }

        [Test]
        public void PortalsMarkWhereATunnelEntersAndLeavesTheHill()
        {
            var portals = BridgeSupports.FindPortals(Deck(), new Terrain(8f));

            Assert.That(portals.Count, Is.EqualTo(2));
            Assert.That(portals[0].Position.x, Is.EqualTo(100f).Within(1f));
            Assert.That(portals[0].Into.x, Is.GreaterThan(0.9f), "de eerste mond wijst de tunnel in, vooruit");
            Assert.That(portals[1].Position.x, Is.EqualTo(200f).Within(1f));
            Assert.That(portals[1].Into.x, Is.LessThan(-0.9f), "de tweede mond wijst terug de tunnel in");
        }

        [Test]
        public void NoPortalsOnFlatOrBridgeRoads()
        {
            Assert.That(BridgeSupports.FindPortals(Deck(), new Terrain(0f)), Is.Empty);
            Assert.That(BridgeSupports.FindPortals(Deck(), new Terrain(-8f)), Is.Empty);
        }

        [Test]
        public void PillarMeshHasAnEightSidedTubePerPillar()
        {
            var pillars = BridgeSupports.Plan(Deck(), new Terrain(-8f), 30f);
            var mesh = BridgeSupports.BuildPillars(pillars);

            Assert.That(mesh.Vertices.Count, Is.EqualTo(3 * 16));
            Assert.That(mesh.Triangles[(int)RoadSurface.Curb].Count, Is.EqualTo(3 * 8 * 6));
            Assert.That(mesh.Normals.Count, Is.EqualTo(mesh.Vertices.Count));
            Assert.That(mesh.Uvs.Count, Is.EqualTo(mesh.Vertices.Count));
        }

        [Test]
        public void PortalMeshHasTwoPostsAndALintel()
        {
            var portal = BridgeSupports.FindPortals(Deck(), new Terrain(8f))[0];
            var mesh = BridgeSupports.BuildPortal(portal, TwoLane);

            Assert.That(mesh.Vertices.Count, Is.EqualTo(3 * 5 * 4));
            Assert.That(mesh.Triangles[(int)RoadSurface.Curb].Count, Is.EqualTo(3 * 5 * 6));
            Assert.That(mesh.Vertices.Max(v => v.y), Is.EqualTo(portal.Position.y + BridgeSupports.PortalClearance + BridgeSupports.PortalHeadHeight).Within(1e-3f));
        }

        [Test]
        public void RejectsBadArguments()
        {
            var ground = new Terrain(0f);
            Assert.Throws<ArgumentNullException>(() => BridgeSupports.Plan(Deck(), null));
            Assert.Throws<ArgumentOutOfRangeException>(() => BridgeSupports.Plan(Deck(), ground, 0f));
            Assert.Throws<ArgumentNullException>(() => BridgeSupports.FindPortals(Deck(), null));
            Assert.Throws<ArgumentNullException>(() => BridgeSupports.BuildPillars(null));
            Assert.Throws<ArgumentNullException>(() => BridgeSupports.BuildPortal(default, null));
            Assert.Throws<ArgumentException>(() => BridgeSupports.BuildPortal(default, new List<LaneSpec>()));
        }
    }
}
